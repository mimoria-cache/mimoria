// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Cluster;

namespace Varelen.Mimoria.Server.Replication;

public sealed class AsyncReplicator : IReplicator
{
    private readonly ClusterServer clusterServer;
    private readonly ConcurrentQueue<IByteBuffer> operationsBuffers;
    private readonly SemaphoreSlim operationsBuffersSemaphore;
    private readonly PeriodicTimer flushTimer;

    public AsyncReplicator(ClusterServer clusterServer, TimeSpan flushInterval)
    {
        this.clusterServer = clusterServer;
        this.operationsBuffers = new ConcurrentQueue<IByteBuffer>();
        this.operationsBuffersSemaphore = new SemaphoreSlim(initialCount: 1);
        this.flushTimer = new PeriodicTimer(flushInterval);

        _ = this.StartFlushingAsync();
    }

    private async Task StartFlushingAsync()
    {
        while (await this.flushTimer.WaitForNextTickAsync())
        {
            await this.operationsBuffersSemaphore.WaitAsync();

            try
            {
                if (this.operationsBuffers.IsEmpty)
                {
                    continue;
                }

                using IByteBuffer operationsBuffer = PooledByteBuffer.FromPool();
                foreach (IByteBuffer buffer in this.operationsBuffers)
                {
                    operationsBuffer.WriteBytes(buffer.Bytes.AsSpan(0, buffer.Size));
                }

                // TODO: Is 'Task.WhenAll' still okay if you have many secondary nodes?
                await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
                {
                    uint requestId = clusterConnection.Value.IncrementRequestId();

                    IByteBuffer batch = PooledByteBuffer.FromPool(Operation.Batch, requestId);

                    batch.WriteVarUInt((uint)this.operationsBuffers.Count);
                    batch.WriteBytes(operationsBuffer.Bytes.AsSpan(0, operationsBuffer.Size));
                    batch.EndPacket();

                    return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, batch).AsTask();
                }));

                this.ClearBuffers();
            }
            finally
            {
                this.operationsBuffersSemaphore.Release();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearBuffers()
    {
        while (this.operationsBuffers.TryDequeue(out IByteBuffer? buffer))
        {
            buffer.Dispose();
        }
    }

    public async ValueTask ReplicateSetStringAsync(string key, string? value, uint ttlMilliseconds)
    {
        await this.operationsBuffersSemaphore.WaitAsync();

        try
        {
            IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteByte((byte)Operation.SetString);
            byteBuffer.WriteString(key);
            byteBuffer.WriteString(value);
            byteBuffer.WriteUInt(ttlMilliseconds);

            this.operationsBuffers.Enqueue(byteBuffer);
        }
        finally
        {
            this.operationsBuffersSemaphore.Release();
        }
    }

    public void Dispose()
    {
        this.flushTimer.Dispose();
        this.operationsBuffersSemaphore.Dispose();

        // TODO: How to make sure remaining buffers are replicated, or ignore it?
        this.ClearBuffers();
    }
}
