// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Bully;
using Varelen.Mimoria.Server.Cluster;

namespace Varelen.Mimoria.Server.Replication;

public sealed class SyncReplicator : IReplicator
{
    private const int BatchCount = 1;
    
    private readonly ClusterServer clusterServer;
    private readonly IBullyAlgorithm bullyAlgorithm;

    public SyncReplicator(ClusterServer clusterServer, IBullyAlgorithm bullyAlgorithm)
    {
        this.clusterServer = clusterServer;
        this.bullyAlgorithm = bullyAlgorithm;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldReplicate()
        => this.bullyAlgorithm.IsLeader;

    public async ValueTask ReplicateSetStringAsync(string key, ByteString? value, uint ttlMilliseconds)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        // TODO: Is 'Task.WhenAll' still okay if you have many secondary nodes?
        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.SetString);
            byteBuffer.WriteString(key);
            byteBuffer.WriteByteString(value);
            byteBuffer.WriteVarUInt(ttlMilliseconds);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateSetBytesAsync(string key, byte[]? value, uint ttlMilliseconds)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();
            uint valueLength = value is not null ? (uint)value.Length : 0;

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.SetBytes);
            byteBuffer.WriteString(key);
            byteBuffer.WriteVarUInt(valueLength);
            if (valueLength > 0)
            {
                byteBuffer.WriteBytes(value.AsSpan());
            }
            byteBuffer.WriteVarUInt(ttlMilliseconds);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateAddListAsync(string key, ByteString? value, uint ttlMilliseconds, uint valueTtlMilliseconds)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.AddList);
            byteBuffer.WriteString(key);
            byteBuffer.WriteByteString(value);
            byteBuffer.WriteVarUInt(ttlMilliseconds);
            byteBuffer.WriteVarUInt(valueTtlMilliseconds);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateRemoveListAsync(string key, ByteString value)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.RemoveList);
            byteBuffer.WriteString(key);
            byteBuffer.WriteByteString(value);
            byteBuffer.EndPacket();
            
            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateSetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.SetMapValue);
            byteBuffer.WriteString(key);
            byteBuffer.WriteString(subKey);
            byteBuffer.WriteValue(value);
            byteBuffer.WriteVarUInt(ttlMilliseconds);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateSetMapAsync(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.SetMap);
            byteBuffer.WriteString(key);
            byteBuffer.WriteVarUInt((uint)map.Count);
            foreach (var (key, value) in map)
            {
                byteBuffer.WriteString(key);
                byteBuffer.WriteValue(value);
            }
            byteBuffer.WriteVarUInt(ttlMilliseconds);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateSetCounterAsync(string key, long value)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.SetCounter);
            byteBuffer.WriteString(key);
            byteBuffer.WriteLong(value);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateIncrementCounterAsync(string key, long increment)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.IncrementCounter);
            byteBuffer.WriteString(key);
            byteBuffer.WriteLong(increment);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateDeleteAsync(string key)
    {
        if (!this.ShouldReplicate())
        {
            return;
        }

        await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
        {
            uint requestId = clusterConnection.Value.IncrementRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Batch, requestId);
            byteBuffer.WriteVarUInt(BatchCount);
            byteBuffer.WriteByte((byte)Operation.Delete);
            byteBuffer.WriteString(key);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public void Dispose()
    {
        // Nothing to do
    }
}
