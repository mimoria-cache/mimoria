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

    public async ValueTask ReplicateSetStringAsync(string key, string? value, uint ttlMilliseconds)
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
            byteBuffer.WriteString(value);
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

    public async ValueTask ReplicateAddListAsync(string key, string? value, uint ttlMilliseconds, uint valueTtlMilliseconds)
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
            byteBuffer.WriteString(value);
            byteBuffer.WriteVarUInt(ttlMilliseconds);
            byteBuffer.WriteVarUInt(valueTtlMilliseconds);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public async ValueTask ReplicateRemoveListAsync(string key, string value)
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
            byteBuffer.WriteString(value);
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
