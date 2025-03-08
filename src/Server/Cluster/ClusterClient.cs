// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Bully;
using Varelen.Mimoria.Server.Cache;

namespace Varelen.Mimoria.Server.Cluster;

public sealed class ClusterClient
{
    private const int DefaultBufferSize = 65535;

    private readonly ILogger<ClusterClient> logger;
    private readonly int id;
    private Socket? socket;
    private readonly BullyAlgorithm bullyAlgorithm;
    private readonly ICache cache;
    private readonly string password;
    private readonly IPEndPoint remoteEndPoint;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource> waitingResponses;

    private readonly PooledByteBuffer buffer = new(DefaultBufferSize);

    private int expectedPacketLength;
    private int receivedBytes;
    private bool connected;
    private uint requestIdCounter;

    public bool Connected => Volatile.Read(ref this.connected);

    public ClusterClient(ILogger<ClusterClient> logger, int id, string ip, int port, BullyAlgorithm bullyAlgorithm, ICache cache, string password)
    {
        this.logger = logger;
        this.id = id;
        this.bullyAlgorithm = bullyAlgorithm;
        this.cache = cache;
        this.password = password;
        this.remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        this.waitingResponses = [];
        this.requestIdCounter = 0;
    }

    public async Task ConnectAsync()
    {
        while (!this.Connected)
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                await this.socket.ConnectAsync(this.remoteEndPoint).WaitAsync(TimeSpan.FromMilliseconds(500));

                this.connected = true;

                using IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.ClusterLogin, requestId: 0);
                byteBuffer.WriteString(this.password);
                byteBuffer.WriteInt(this.id);
                byteBuffer.EndPacket();

                await this.SendAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

                _ = this.ReceiveAsync();
            }
            catch (Exception)
            {
                this.socket.Close();
            }
        }

        this.logger.LogInformation("Connected to node at '{RemoteEndPoint}'", remoteEndPoint);
    }

    private async Task ReceiveAsync()
    {
        try
        {
            // Large buffer because of possible batch packets
            var buffer = GC.AllocateArray<byte>(length: DefaultBufferSize, pinned: true);

            while (this.Connected)
            {
                int received = await this.socket!.ReceiveAsync(buffer.AsMemory());
                if (received == 0)
                {
                    this.Disconnect();
                    return;
                }

                this.expectedPacketLength = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan());
                this.receivedBytes = received - 4;
                this.buffer.WriteBytes(buffer.AsSpan(4, received - 4));

                while (this.receivedBytes < this.expectedPacketLength)
                {
                    int bytesToReceive = Math.Min(this.expectedPacketLength - this.receivedBytes, buffer.Length);
                    received = await this.socket.ReceiveAsync(buffer.AsMemory(0, bytesToReceive), SocketFlags.None);
                    if (received == 0)
                    {
                        this.Disconnect();
                        return;
                    }

                    this.receivedBytes += received;
                    this.buffer.WriteBytes(buffer.AsSpan(0, received));
                }

                using IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
                byteBuffer.WriteBytes(this.buffer.Bytes.AsSpan(0, this.expectedPacketLength));

                var operation = (Operation)byteBuffer.ReadByte();
                uint requestId = byteBuffer.ReadUInt();

                switch (operation)
                {
                    case Operation.ElectionMessage:
                        {
                            this.logger.LogTrace("Received election message, sending alive message back with current leader '{LeaderId}'", this.bullyAlgorithm.Leader);

                            using var aliveBuffer = PooledByteBuffer.FromPool(Operation.AliveMessage, requestId: 0);
                            aliveBuffer.WriteInt(this.bullyAlgorithm.Leader);
                            aliveBuffer.EndPacket();

                            await this.SendAsync(aliveBuffer.Bytes.AsMemory(0, aliveBuffer.Size));
                            break;
                        }
                    case Operation.VictoryMessage:
                        {
                            int leaderId = byteBuffer.ReadInt();

                            await this.bullyAlgorithm.HandleVictoryAsync(leaderId);
                            break;
                        }
                    case Operation.HeartbeatMessage:
                        {
                            int leaderId = byteBuffer.ReadInt();
                            this.bullyAlgorithm.HandleHeartbeat(leaderId);
                            break;
                        }
                    case Operation.Batch:
                        {
                            uint count = byteBuffer.ReadVarUInt();
                            for (int i = 0; i < count; i++)
                            {
                                var batchOperation = (Operation)byteBuffer.ReadByte();
                                switch (batchOperation)
                                {
                                    case Operation.SetString:
                                        {
                                            string key = byteBuffer.ReadString()!;
                                            string? value = byteBuffer.ReadString();
                                            uint ttlMilliseconds = byteBuffer.ReadUInt();

                                            await this.cache.SetStringAsync(key, value, ttlMilliseconds);
                                            break;
                                        }
                                    case Operation.SetObjectBinary:
                                        break;
                                    case Operation.AddList:
                                        break;
                                    case Operation.RemoveList:
                                        break;
                                    case Operation.Delete:
                                        break;
                                    case Operation.SetBytes:
                                        break;
                                    case Operation.SetCounter:
                                        break;
                                    case Operation.IncrementCounter:
                                        break;
                                    case Operation.Bulk:
                                        break;
                                    case Operation.SetMapValue:
                                        break;
                                    case Operation.SetMap:
                                        break;
                                    default:
                                        break;
                                }
                            }

                            using var batchBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
                            batchBuffer.EndPacket();

                            await this.SendAsync(batchBuffer.Bytes.AsMemory(0, batchBuffer.Size));
                            break;
                        }
                    case Operation.Sync:
                        {
                            this.logger.LogDebug("Received sync with '{ByteCount}' bytes", byteBuffer.Size);

                            this.cache.Deserialize(byteBuffer);

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
            this.Disconnect();
        }
        catch (Exception exception)
        {
            this.Disconnect();

            this.logger.LogError(exception, "Unexpected error while receiving");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask SendAsync(Memory<byte> buffer)
        => this.socket!.SendAllAsync(buffer);

    public async ValueTask SendAndWaitForResponseAsync(uint requestId, IByteBuffer byteBuffer)
    {
        try
        {
            var taskCompletionSource = new TaskCompletionSource();
            bool added = this.waitingResponses.TryAdd(requestId, taskCompletionSource);
            Debug.Assert(added, "Task completion was not added to dictionary");

            await this.SendAsync(byteBuffer.Bytes.AsMemory(0, byteBuffer.Size));

            await taskCompletionSource.Task;
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint IncrementRequestId()
        => Interlocked.Increment(ref this.requestIdCounter);

    public void Disconnect(bool reconnect = true)
    {
        if (!Interlocked.Exchange(ref this.connected, false))
        {
            return;
        }

        this.logger.LogInformation("Disconnected from node at '{RemoteEndPoint}'", this.remoteEndPoint);

        try
        {
            this.socket?.Shutdown(SocketShutdown.Both);
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // Ignored
        }
        finally
        {
            this.socket?.Close();
        }

        if (reconnect)
        {
            this.buffer.Clear();
            _ = this.ConnectAsync();
        }
        else
        {
            this.buffer.Dispose();
        }
    }

    public void Close()
        => this.Disconnect(reconnect: false);
}
