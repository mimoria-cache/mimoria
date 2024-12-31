// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Server.Cluster;

public sealed class ClusterConnection
{
    private const int DefaultBufferSize = 8192;

    public delegate void ClusterConnectionEvent(ClusterConnection clusterConnection);
    public delegate void MessageEvent(int leader);

    private readonly ClusterServer clusterServer;
    private readonly Socket socket;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource> waitingResponses;
    private readonly IByteBuffer buffer;
    private bool connected;
    private uint requestIdCounter;
    private int expectedPacketLength;
    private int receivedBytes;

    public int Id { get; private set; }

    public bool Connected => Volatile.Read(ref this.connected);

    public EndPoint RemoteEndPoint => this.socket.RemoteEndPoint!;

    public event ClusterConnectionEvent? Authenticated;
    public event MessageEvent? AliveReceived;

    public ClusterConnection(int id, ClusterServer clusterServer, Socket socket)
    {
        this.Id = id;
        this.socket = socket;
        this.clusterServer = clusterServer;
        this.connected = true;
        this.waitingResponses = [];
        this.requestIdCounter = 0;
        // TODO: Larger byte buffer so it does not need to resize
        this.buffer = PooledByteBuffer.FromPool();
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

                this.expectedPacketLength = BinaryPrimitives.ReadInt32BigEndian(receiveBuffer);
                this.receivedBytes = received - 4;
                this.buffer.WriteBytes(receiveBuffer.AsSpan(4, received - 4));

                while (this.receivedBytes < this.expectedPacketLength)
                {
                    int bytesToReceive = Math.Min(this.expectedPacketLength - this.receivedBytes, receiveBuffer.Length);
                    received = await this.socket.ReceiveAsync(receiveBuffer.AsMemory(0, bytesToReceive), SocketFlags.None);
                    if (received == 0)
                    {
                        this.Disconnect();
                        return;
                    }

                    this.receivedBytes += received;
                    this.buffer.WriteBytes(receiveBuffer.AsSpan(0, received));
                }

                using IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
                byteBuffer.WriteBytes(this.buffer.Bytes.AsSpan(0, this.expectedPacketLength));

                var operation = (Operation)byteBuffer.ReadByte();
                var requestId = byteBuffer.ReadUInt();

                switch (operation)
                {
                    case Operation.ClusterLogin:
                        this.Id = byteBuffer.ReadInt();

                        this.Authenticated?.Invoke(this);
                        break;
                    case Operation.AliveMessage:
                        {
                            int leader = byteBuffer.ReadInt();
                            this.AliveReceived?.Invoke(leader);
                            break;
                        }
                    case Operation.Batch:
                        {
                            if (this.waitingResponses.TryRemove(requestId, out TaskCompletionSource? taskComplectionSource))
                            {
                                taskComplectionSource.SetResult();
                            }
                            break;
                        }
                }

                this.buffer.Clear();
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

    public async ValueTask SendAsync(IByteBuffer byteBuffer)
    {
        try
        {
            // TODO: Handle if sent is less than
            _ = await this.socket.SendAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));
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

    public async ValueTask SendAndWaitForResponseAsync(uint requestId, IByteBuffer byteBuffer)
    {
        try
        {
            var taskCompletionSource = new TaskCompletionSource();
            bool added = this.waitingResponses.TryAdd(requestId, taskCompletionSource);
            Debug.Assert(added, "Task completion was not added to dictionary");

            // TODO: Handle if sent is less than
            _ = await this.socket.SendAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

            await taskCompletionSource.Task;
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
            this.buffer.Dispose();

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
