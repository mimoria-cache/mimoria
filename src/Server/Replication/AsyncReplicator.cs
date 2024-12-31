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
    private readonly ConcurrentQueue<IByteBuffer> buffers;
    private readonly SemaphoreSlim buffersSemaphore;
    private readonly PeriodicTimer flushTimer;

    public AsyncReplicator(ClusterServer clusterServer, TimeSpan flushInterval)
    {
        this.clusterServer = clusterServer;
        this.buffers = new ConcurrentQueue<IByteBuffer>();
        this.buffersSemaphore = new SemaphoreSlim(initialCount: 1);
        this.flushTimer = new PeriodicTimer(flushInterval);

        _ = this.StartFlushingAsync();
    }

    private async Task StartFlushingAsync()
    {
        while (await this.flushTimer.WaitForNextTickAsync())
        {
            await this.buffersSemaphore.WaitAsync();

            try
            {
                if (this.buffers.IsEmpty)
                {
                    continue;
                }

                // TODO: Is 'Task.WhenAll' still okay if you have many secondary nodes?
                await Task.WhenAll(this.clusterServer.Clients.Select(clusterConnection =>
                {
                    uint requestId = clusterConnection.Value.IncrementRequestId();

                    IByteBuffer batch = PooledByteBuffer.FromPool(Operation.Batch, requestId);

                    batch.WriteVarUInt((uint)this.buffers.Count);

                    // TODO: Write buffers only once and reuse it?
                    foreach (IByteBuffer buffer in this.buffers)
                    {
                        batch.WriteBytes(buffer.Bytes.AsSpan(0, buffer.Size));
                    }

                    batch.EndPacket();

                    return clusterConnection.Value.SendAndWaitForResponseAsync(requestId, batch).AsTask();
                }));

                this.ClearBuffers();
            }
            finally
            {
                this.buffersSemaphore.Release();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearBuffers()
    {
        while (this.buffers.TryDequeue(out IByteBuffer? buffer))
        {
            buffer.Dispose();
        }
    }

    public async ValueTask ReplicateSetStringAsync(string key, string? value, uint ttlMilliseconds)
    {
        await this.buffersSemaphore.WaitAsync();

        try
        {
            IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteByte((byte)Operation.SetString);
            byteBuffer.WriteString(key);
            byteBuffer.WriteString(value);
            byteBuffer.WriteUInt(ttlMilliseconds);

            this.buffers.Enqueue(byteBuffer);
        }
        finally
        {
            this.buffersSemaphore.Release();
        }
    }

    public void Dispose()
    {
        this.flushTimer.Dispose();
        this.buffersSemaphore.Dispose();

        // TODO: How to make sure remaining buffers are replicated, or ignore it?
        this.ClearBuffers();
    }
}
