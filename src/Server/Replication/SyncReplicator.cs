// SPDX-FileCopyrightText: 2024 varelen
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
            byteBuffer.WriteVarUInt(1);
            byteBuffer.WriteByte((byte)Operation.SetString);
            byteBuffer.WriteString(key);
            byteBuffer.WriteString(value);
            byteBuffer.WriteUInt(ttlMilliseconds);
            byteBuffer.EndPacket();

            return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, byteBuffer).AsTask();
        }));
    }

    public void Dispose()
    {
        // Nothing to do
    }
}
