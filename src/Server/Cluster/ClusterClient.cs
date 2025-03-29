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
    private readonly LengthPrefixedPacketReader lengthPrefixedPacketReader;

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
        this.lengthPrefixedPacketReader = new LengthPrefixedPacketReader(ProtocolDefaults.LengthPrefixLength);
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

                foreach (IByteBuffer byteBuffer in this.lengthPrefixedPacketReader.TryRead(buffer, received))
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
            this.Disconnect();
        }
        catch (Exception exception)
        {
            this.Disconnect();

            this.logger.LogError(exception, "Unexpected error while receiving");
        }
    }

    private async Task HandlePacketReceivedAsync(IByteBuffer byteBuffer)
    {
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
                                    uint ttlMilliseconds = byteBuffer.ReadVarUInt();

                                    await this.cache.SetStringAsync(key, value, ttlMilliseconds);
                                    break;
                                }
                            case Operation.SetObjectBinary:
                                break;
                            case Operation.AddList:
                                {
                                    string key = byteBuffer.ReadString()!;
                                    string value = byteBuffer.ReadString()!;
                                    uint ttlMilliseconds = byteBuffer.ReadVarUInt();
                                    uint valueTtlMilliseconds = byteBuffer.ReadVarUInt();
                                    
                                    await this.cache.AddListAsync(key, value, ttlMilliseconds, valueTtlMilliseconds, ProtocolDefaults.MaxListCount);
                                    break;
                                }
                            case Operation.RemoveList:
                                {
                                    string key = byteBuffer.ReadString()!;
                                    string value = byteBuffer.ReadString()!;

                                    await this.cache.RemoveListAsync(key, value);
                                    break;
                                }
                            case Operation.Delete:
                                {
                                    string key = byteBuffer.ReadString()!;
                                    await this.cache.DeleteAsync(key);
                                    break;
                                }
                            case Operation.SetBytes:
                                {
                                    string key = byteBuffer.ReadString()!;
                                    uint valueLength = byteBuffer.ReadVarUInt();

                                    if (valueLength > ProtocolDefaults.MaxByteArrayLength)
                                    {
                                        throw new ArgumentException($"Read bytes length '{valueLength}' exceeded max allowed length '{ProtocolDefaults.MaxByteArrayLength}'");
                                    }

                                    if (valueLength > 0)
                                    {
                                        byte[] value = new byte[valueLength];
                                        byteBuffer.ReadBytes(value.AsSpan());

                                        uint ttlMilliseconds = byteBuffer.ReadVarUInt();
                                        await this.cache.SetBytesAsync(key, value, ttlMilliseconds);
                                    }
                                    else
                                    {
                                        uint ttlMilliseconds = byteBuffer.ReadVarUInt();
                                        await this.cache.SetBytesAsync(key, null, ttlMilliseconds);
                                    }
                                    break;
                                }
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask SendAsync(Memory<byte> buffer)
        => this.socket!.SendAllAsync(buffer);

    public async ValueTask SendAndWaitForResponseAsync(uint requestId, IByteBuffer byteBuffer)
    {
        try
        {
            var taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
            this.lengthPrefixedPacketReader.Reset();
            _ = this.ConnectAsync();
        }
        else
        {
            this.lengthPrefixedPacketReader.Dispose();
        }
    }

    public void Close()
        => this.Disconnect(reconnect: false);
}
