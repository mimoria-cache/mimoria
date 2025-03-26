// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Client.ConsistentHash;
using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

public sealed class ShardedMimoriaClient : IShardedMimoriaClient
{
    private readonly Dictionary<int, IMimoriaClient> idMimoriaClients;
    private readonly IReadOnlyList<IMimoriaClient> mimoriaClients;
    private readonly IConsistentHashing consistentHashing;

    public IReadOnlyList<IMimoriaClient> MimoriaClients => this.mimoriaClients;

    public int? ServerId => throw new NotSupportedException();

    public bool IsConnected => throw new NotSupportedException();

    public bool IsPrimary { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public ShardedMimoriaClient(string password, params IPEndPoint[] ipEndPoints)
        : this(new ConsistentHashing(new Murmur3Hasher()), password, ipEndPoints)
    {

    }

    public ShardedMimoriaClient(IConsistentHashing consistentHashing, string password, params IPEndPoint[] ipEndPoints)
    {
        if (ipEndPoints.Length < 2)
        {
            throw new ArgumentException("At least two endpoints required", nameof(ipEndPoints));
        }

        this.idMimoriaClients = new Dictionary<int, IMimoriaClient>();
        this.consistentHashing = consistentHashing;
        this.mimoriaClients = ipEndPoints
            .Select(ipEndPoint => new MimoriaClient(ipEndPoint.Address.ToString(), (ushort)ipEndPoint.Port, password))
            .ToList();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            await mimoriaClient.ConnectAsync(cancellationToken);

            this.idMimoriaClients.Add(mimoriaClient.ServerId!.Value, mimoriaClient);
            this.consistentHashing.AddServerId(mimoriaClient.ServerId.Value);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            await mimoriaClient.DisconnectAsync(cancellationToken);
        }
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetStringAsync(key, cancellationToken);
    }

    public async Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetStringAsync(key, value, ttl, cancellationToken);
    }

    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await foreach (string s in mimoriaClient.GetListEnumerableAsync(key, cancellationToken))
        {
            yield return s;
        }
    }

    public async Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetListAsync(key, cancellationToken);
    }

    public async Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.AddListAsync(key, value, ttl, valueTtl, cancellationToken);
    }

    public async Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.RemoveListAsync(key, value, cancellationToken);
    }

    public async Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.ContainsListAsync(key, value, cancellationToken);
    }

    public async Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetObjectBinaryAsync(key, binarySerializable, ttl, cancellationToken);
    }

    public async Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetObjectBinaryAsync<T>(key, cancellationToken);
    }

    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.ExistsAsync(key, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.DeleteAsync(key, cancellationToken);
    }

    public async Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetObjectJsonAsync<T>(key, jsonSerializerOptions, cancellationToken);
    }

    public async Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetObjectJsonAsync<T>(key, t, jsonSerializerOptions, ttl, cancellationToken);
    }

    public async Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetBytesAsync(key, cancellationToken);
    }

    public async Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetBytesAsync(key, value, ttl, cancellationToken);
    }

    public async Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
        => await this.GetStatsAsync(0, cancellationToken);

    public async Task<Stats> GetStatsAsync(int serverId, CancellationToken cancellationToken = default)
    {
        if (!this.idMimoriaClients.TryGetValue(serverId, out IMimoriaClient? mimoriaClient))
        {
            throw new ArgumentOutOfRangeException(nameof(serverId));
        }

        return await mimoriaClient.GetStatsAsync(cancellationToken);
    }

    public Task SetCounterAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.SetCounterAsync(key, value, cancellationToken);
    }

    public Task<long> IncrementCounterAsync(string key, long increment, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.IncrementCounterAsync(key, increment, cancellationToken);
    }

    public Task<long> DecrementCounterAsync(string key, long decrement, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, -decrement, cancellationToken);

    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, increment: 0, cancellationToken);

    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.GetMapValueAsync(key, subKey, cancellationToken);
    }

    public Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.SetMapValueAsync(key, subKey, subValue, ttl, cancellationToken);
    }

    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.GetMapAsync(key, cancellationToken);
    }

    public Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.SetMapAsync(key, map, ttl, cancellationToken);
    }

    public Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(channel);
        return mimoriaClient.SubscribeAsync(channel, cancellationToken);
    }

    public Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(channel);
        return mimoriaClient.UnsubscribeAsync(channel, cancellationToken);
    }

    public Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(channel);
        return mimoriaClient.PublishAsync(channel, payload, cancellationToken);
    }

    public IBulkOperation Bulk()
        => new ShardedBulkOperation(this);

    internal static async Task<List<object?>> ExecuteBulkAsync(ShardedBulkOperation shardedBulkOperation, CancellationToken cancellationToken = default)
    {
        var responses = new List<object?>();
        foreach (var (_, bulkOperation) in shardedBulkOperation.BulkOperations)
        {
            List<object?> response = await bulkOperation.ExecuteAsync(cancellationToken);
            responses.AddRange(response);
        }
        return responses;
    }

    internal MimoriaClient GetMimoriaClient(int serverId)
        => (MimoriaClient)this.idMimoriaClients[serverId];

    internal int GetServerId(string key)
        => this.consistentHashing.GetServerId(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IMimoriaClient GetMimoriaClient(string key)
    {
        int serverId = this.consistentHashing.GetServerId(key);
        return this.idMimoriaClients[serverId];
    }

    public async ValueTask DisposeAsync()
        => await this.DisconnectAsync();
}
