// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core.Network;
using Varelen.Mimoria.Server.Cache;

namespace Varelen.Mimoria.Server.Cluster;

public sealed class ClusterConnection
{
    private const int DefaultBufferSize = 8192;

    public delegate void ClusterConnectionEvent(ClusterConnection clusterConnection);
    public delegate void MessageEvent(int leader);

    private readonly ILogger<ClusterConnection> logger;
    private readonly ClusterServer clusterServer;
    private readonly Socket socket;
    private readonly ICache cache;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<IByteBuffer?>> waitingResponses;
    private readonly LengthPrefixedPacketReader lengthPrefixedPacketReader;

    private bool connected;
    private bool authenticated;
    private uint requestIdCounter;

    public int Id { get; private set; }

    public bool Connected => Volatile.Read(ref this.connected);

    public EndPoint RemoteEndPoint => this.socket.RemoteEndPoint!;

    public event ClusterConnectionEvent? Authenticated;
    public event MessageEvent? AliveReceived;

    public ClusterConnection(ILogger<ClusterConnection> logger, ClusterServer clusterServer, Socket socket, ICache cache)
    {
        this.logger = logger;
        this.Id = -1;
        this.socket = socket;
        this.cache = cache;
        this.clusterServer = clusterServer;
        this.connected = true;
        this.authenticated = false;
        this.waitingResponses = [];
        this.requestIdCounter = 0;
        this.lengthPrefixedPacketReader = new LengthPrefixedPacketReader(ProtocolDefaults.LengthPrefixLength);
    }

    public async Task ReceiveAsync()
    {
        try
        {
            var receiveBuffer = new byte[DefaultBufferSize];
            while (this.Connected)
            {
                int received = await this.socket.ReceiveAsync(receiveBuffer.AsMemory());
                if (received == 0)
                {
                    this.Disconnect();
                    return;
                }

                foreach (IByteBuffer byteBuffer in this.lengthPrefixedPacketReader.TryRead(receiveBuffer, received))
                {
                    try
                    {
                        await this.HandlePacketReceivedAsync(byteBuffer);
                    }
                    finally
                    {
                        byteBuffer.Dispose();
                    }
                }
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignore
        }
        finally
        {
            this.Disconnect();
        }
    }

    /// <summary>
    /// Only allow other operations than <see cref="Operation.ClusterLogin"/> if authenticated.
    /// </summary>
    /// <param name="operation">The operation to check.</param>
    /// <returns>true, if authenticated or the operation is <see cref="Operation.ClusterLogin"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAuthenticated(Operation operation)
        => this.authenticated || operation == Operation.ClusterLogin;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task HandlePacketReceivedAsync(IByteBuffer byteBuffer)
    {
        var operation = (Operation)byteBuffer.ReadByte();
        var requestId = byteBuffer.ReadUInt();

        // Ignoring fire and forget
        _ = byteBuffer.ReadByte();

        if (!this.IsAuthenticated(operation))
        {
            this.logger.LogWarning("Cluster connection '{RemoteEndPoint}' tried to send operation '{Operation}' without being authenticated", this.RemoteEndPoint, operation);

            this.Disconnect();
            return;
        }

        switch (operation)
        {
            case Operation.ClusterLogin:
                string receivedPassword = byteBuffer.ReadString()!;
                if (!receivedPassword.Equals(this.clusterServer.password, StringComparison.Ordinal))
                {
                    this.logger.LogWarning("Cluster connection '{RemoteEndPoint}' tried to login with wrong password", this.RemoteEndPoint);

                    this.Disconnect();
                    return;
                }

                this.Id = byteBuffer.ReadInt();

                this.authenticated = true;
                this.Authenticated?.Invoke(this);
                break;
            case Operation.AliveMessage:
                int leader = byteBuffer.ReadInt();
                this.AliveReceived?.Invoke(leader);
                break;
            case Operation.VictoryMessage:
                if (this.waitingResponses.TryRemove(requestId, out TaskCompletionSource<IByteBuffer?>? victoryTaskComplectionSource))
                {
                    // TODO: Hmm.. The 'byteBuffer' livetime is currently handled outside of this method
                    var buffer = PooledByteBuffer.FromPool();
                    buffer.WriteBool(byteBuffer.ReadBool());
                    buffer.WriteInt(byteBuffer.ReadInt());

                    victoryTaskComplectionSource.SetResult(buffer);
                }
                break;
            case Operation.Batch:
                if (this.waitingResponses.TryRemove(requestId, out TaskCompletionSource<IByteBuffer?>? batchTaskComplectionSource))
                {
                    batchTaskComplectionSource.SetResult(null);
                }
                break;
            case Operation.SyncResponse:
                if (this.waitingResponses.TryRemove(requestId, out TaskCompletionSource<IByteBuffer?>? syncTaskComplectionSource))
                {
                    this.cache.Deserialize(byteBuffer);

                    syncTaskComplectionSource.SetResult(null);
                }
                break;
            case Operation.SyncRequest:
                this.logger.LogDebug("Primary sync request from '{RemoteEndPoint}' with request id '{RequestId}'", this.RemoteEndPoint, requestId);

                var syncResponseBuffer = PooledByteBuffer.FromPool(Operation.SyncResponse, requestId);

                await this.cache.SerializeAsync(syncResponseBuffer);

                syncResponseBuffer.EndPacket();

                await this.SendAsync(syncResponseBuffer);
                break;
        }
    }

    public async ValueTask SendAsync(IByteBuffer byteBuffer)
    {
        try
        {
            await this.socket.SendAllAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            this.Disconnect();
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    public async ValueTask<IByteBuffer?> SendAndWaitForResponseAsync(uint requestId, IByteBuffer byteBuffer)
    {
        try
        {
            var taskCompletionSource = new TaskCompletionSource<IByteBuffer?>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool added = this.waitingResponses.TryAdd(requestId, taskCompletionSource);
            Debug.Assert(added, "Task completion was not added to dictionary");

            await this.socket.SendAllAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

            return await taskCompletionSource.Task;
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    public void Disconnect()
    {
        if (!Interlocked.Exchange(ref this.connected, false))
        {
            return;
        }

        this.authenticated = false;

        try
        {
            this.socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignore
        }
        finally
        {
            this.socket.Close();
            this.lengthPrefixedPacketReader.Dispose();

            this.clusterServer.HandleConnectionDisconnect(this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint IncrementRequestId()
        => Interlocked.Increment(ref this.requestIdCounter);

    public override bool Equals(object? obj)
        => obj is ClusterConnection connection &&
            this.Id.Equals(connection.Id);

    public override int GetHashCode()
        => HashCode.Combine(this.Id);
}
