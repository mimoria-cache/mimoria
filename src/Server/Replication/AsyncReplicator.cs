// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Diagnostics;
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

                Debug.Assert(this.operationsBuffers.IsEmpty, "Operations buffers are not empty");
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

    public async ValueTask ReplicateSetStringAsync(string key, ByteString? value, uint ttlMilliseconds)
    {
        await this.operationsBuffersSemaphore.WaitAsync();

        try
        {
            IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteByte((byte)Operation.SetString);
            byteBuffer.WriteString(key);
            byteBuffer.WriteByteString(value);
            byteBuffer.WriteVarUInt(ttlMilliseconds);

            this.operationsBuffers.Enqueue(byteBuffer);
        }
        finally
        {
            this.operationsBuffersSemaphore.Release();
        }
    }

    public async ValueTask ReplicateSetBytesAsync(string key, byte[]? value, uint ttlMilliseconds)
    {
        await this.operationsBuffersSemaphore.WaitAsync();

        uint valueLength = value is not null ? (uint)value.Length : 0;

        try
        {
            IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteByte((byte)Operation.SetBytes);
            byteBuffer.WriteString(key);
            byteBuffer.WriteVarUInt(valueLength);
            if (valueLength > 0)
            {
                byteBuffer.WriteBytes(value.AsSpan());
            }
            byteBuffer.WriteVarUInt(ttlMilliseconds);

            this.operationsBuffers.Enqueue(byteBuffer);
        }
        finally
        {
            this.operationsBuffersSemaphore.Release();
        }
    }

    public async ValueTask ReplicateAddListAsync(string key, ByteString? value, uint ttlMilliseconds, uint valueTtlMilliseconds)
    {
        await this.operationsBuffersSemaphore.WaitAsync();

        try
        {
            IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteByte((byte)Operation.AddList);
            byteBuffer.WriteString(key);
            byteBuffer.WriteByteString(value);
            byteBuffer.WriteVarUInt(ttlMilliseconds);
            byteBuffer.WriteVarUInt(valueTtlMilliseconds);
            
            this.operationsBuffers.Enqueue(byteBuffer);
        }
        finally
        {
            this.operationsBuffersSemaphore.Release();
        }
    }

    public async ValueTask ReplicateRemoveListAsync(string key, ByteString value)
    {
        await this.operationsBuffersSemaphore.WaitAsync();

        try
        {
            IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteByte((byte)Operation.RemoveList);
            byteBuffer.WriteString(key);
            byteBuffer.WriteByteString(value);

            this.operationsBuffers.Enqueue(byteBuffer);
        }
        finally
        {
            this.operationsBuffersSemaphore.Release();
        }
    }

    public async ValueTask ReplicateDeleteAsync(string key)
    {
        await this.operationsBuffersSemaphore.WaitAsync();
        
        try
        {
            IByteBuffer byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteByte((byte)Operation.Delete);
            byteBuffer.WriteString(key);

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
