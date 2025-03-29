// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client;

/// <summary>
/// An implementation of a bulk operation that can be executed on a Mimoria server.
/// </summary>
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

    /// <inheritdoc />
    public void GetString(string key)
    {
        this.byteBuffer.WriteByte((byte)Operation.GetString);
        this.byteBuffer.WriteString(key);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void SetString(string key, string value, TimeSpan ttl = default)
    {
        this.byteBuffer.WriteByte((byte)Operation.SetString);
        this.byteBuffer.WriteString(key);
        this.byteBuffer.WriteString(value);
        this.byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void AddList(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default)
    {
        this.byteBuffer.WriteByte((byte)Operation.AddList);
        this.byteBuffer.WriteString(key);
        this.byteBuffer.WriteString(value);
        this.byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);
        this.byteBuffer.WriteVarUInt((uint)valueTtl.TotalMilliseconds);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void RemoveList(string key, string value)
    {
        this.byteBuffer.WriteByte((byte)Operation.RemoveList);
        this.byteBuffer.WriteString(key);
        this.byteBuffer.WriteString(value);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void GetList(string key)
    {
        this.byteBuffer.WriteByte((byte)Operation.GetList);
        this.byteBuffer.WriteString(key);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void ContainsList(string key, string value)
    {
        this.byteBuffer.WriteByte((byte)Operation.ContainsList);
        this.byteBuffer.WriteString(key);
        this.byteBuffer.WriteString(value);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void IncrementCounter(string key, long increment = 1)
    {
        this.byteBuffer.WriteByte((byte)Operation.IncrementCounter);
        this.byteBuffer.WriteString(key);
        this.byteBuffer.WriteLong(increment);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void Exists(string key)
    {
        this.byteBuffer.WriteByte((byte)Operation.Exists);
        this.byteBuffer.WriteString(key);

        this.operationCount++;
    }

    /// <inheritdoc />
    public void Delete(string key)
    {
        this.byteBuffer.WriteByte((byte)Operation.Delete);
        this.byteBuffer.WriteString(key);

        this.operationCount++;
    }

    /// <inheritdoc />
    public Task<List<object?>> ExecuteAsync(CancellationToken cancellationToken = default)
        => this.mimoriaClient.ExecuteBulkAsync(this, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        this.byteBuffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
