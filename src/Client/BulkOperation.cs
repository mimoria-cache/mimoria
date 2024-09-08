// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client;

public sealed class BulkOperation : IBulkOperation, IDisposable
{
    private readonly IByteBuffer byteBuffer;
    private readonly MimoriaClient mimoriaClient;
    private uint operationCount;

    internal IByteBuffer ByteBuffer => this.byteBuffer;
    internal uint OperationCount => this.operationCount;

    internal BulkOperation(MimoriaClient mimoriaClient)
    {
        this.mimoriaClient = mimoriaClient;
        this.byteBuffer = PooledByteBuffer.FromPool();
        this.operationCount = 0;
    }

    public void GetString(string key)
    {
        this.byteBuffer.WriteByte((byte)Operation.GetString);
        this.byteBuffer.WriteString(key);

        this.operationCount++;
    }

    public void SetString(string key, string value, TimeSpan ttl = default)
    {
        this.byteBuffer.WriteByte((byte)Operation.SetString);
        this.byteBuffer.WriteString(key);
        this.byteBuffer.WriteString(value);
        this.byteBuffer.WriteUInt((uint)ttl.TotalMilliseconds);

        this.operationCount++;
    }

    public Task<List<object?>> ExecuteAsync(CancellationToken cancellationToken = default)
        => this.mimoriaClient.ExecuteBulkAsync(this, cancellationToken);

    public void Dispose()
    {
        this.byteBuffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
