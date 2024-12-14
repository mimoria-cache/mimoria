// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Client;

public class ShardedBulkOperation : IBulkOperation, IDisposable
{
    private readonly ShardedMimoriaClient shardedMimoriaClient;
    private readonly Dictionary<Guid, BulkOperation> bulkOperations;

    internal Dictionary<Guid, BulkOperation> BulkOperations => this.bulkOperations;

    internal ShardedBulkOperation(ShardedMimoriaClient shardedMimoriaClient)
    {
        this.shardedMimoriaClient = shardedMimoriaClient;
        this.bulkOperations = new Dictionary<Guid, BulkOperation>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BulkOperation GetOrAddBulkOperation(string key)
    {
        Guid serverId = this.shardedMimoriaClient.GetServerId(key);

        if (this.bulkOperations.TryGetValue(serverId, out BulkOperation? byteBuffer))
        {
            return byteBuffer;
        }

        var newBulkOperation = new BulkOperation(this.shardedMimoriaClient.GetMimoriaClient(serverId));
        this.bulkOperations.Add(serverId, newBulkOperation);
        return newBulkOperation;
    }

    public void GetString(string key)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);

        bulkOperation.GetString(key);
    }

    public void SetString(string key, string value, TimeSpan ttl = default)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);

        bulkOperation.SetString(key, value, ttl);
    }

    public void Exists(string key)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);

        bulkOperation.Exists(key);
    }

    public void Delete(string key)
    {
        BulkOperation bulkOperation = this.GetOrAddBulkOperation(key);

        bulkOperation.Delete(key);
    }

    public Task<List<object?>> ExecuteAsync(CancellationToken cancellationToken = default)
        => ShardedMimoriaClient.ExecuteBulkAsync(this, cancellationToken);

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
