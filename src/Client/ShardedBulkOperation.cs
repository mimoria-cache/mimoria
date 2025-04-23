// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Client;

/// <summary>
/// An implementation of a sharded bulk operation that can be executed on a Mimoria server.
/// </summary>
public class ShardedBulkOperation : IBulkOperation, IDisposable
{
    private readonly ShardedMimoriaClient shardedMimoriaClient;
    private readonly Dictionary<int, BulkOperation> bulkOperations;

    internal Dictionary<int, BulkOperation> BulkOperations => this.bulkOperations;

    internal ShardedBulkOperation(ShardedMimoriaClient shardedMimoriaClient)
    {
        this.shardedMimoriaClient = shardedMimoriaClient;
        this.bulkOperations = new Dictionary<int, BulkOperation>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BulkOperation GetOrAddBulkOperation(string key)
    {
        int serverId = this.shardedMimoriaClient.GetServerId(key);

        if (this.bulkOperations.TryGetValue(serverId, out BulkOperation? byteBuffer))
        {
            return byteBuffer;
        }

        var newBulkOperation = new BulkOperation(this.shardedMimoriaClient.GetMimoriaClient(serverId));
        this.bulkOperations.Add(serverId, newBulkOperation);
        return newBulkOperation;
    }

    /// <inheritdoc />
    public void GetString(string key)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.GetString(key);
    }

    /// <inheritdoc />
    public void SetString(string key, string value, TimeSpan ttl = default)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.SetString(key, value, ttl);
    }

    /// <inheritdoc />
    public void AddList(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.AddList(key, value, ttl, valueTtl);
    }

    /// <inheritdoc />
    public void RemoveList(string key, string value)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.RemoveList(key, value);
    }

    /// <inheritdoc />
    public void GetList(string key)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.GetList(key);
    }

    /// <inheritdoc />
    public void ContainsList(string key, string value)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.ContainsList(key, value);
    }

    /// <inheritdoc />
    public void IncrementCounter(string key, long increment = 1)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.IncrementCounter(key, increment);
    }

    /// <inheritdoc />
    public void SetCounter(string key, long value)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.SetCounter(key, value);
    }

    /// <inheritdoc />
    public void Exists(string key)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.Exists(key);
    }

    /// <inheritdoc />
    public void Delete(string key)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);
        bulkOperation.Delete(key);
    }

    /// <inheritdoc />
    public Task<ImmutableList<object?>> ExecuteAsync(bool fireAndForget = false, CancellationToken cancellationToken = default)
        => ShardedMimoriaClient.ExecuteBulkAsync(this, fireAndForget, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (BulkOperation bulkOperation in bulkOperations.Values)
        {
            bulkOperation.Dispose();
        }

        this.bulkOperations.Clear();

        GC.SuppressFinalize(this);
    }
}
