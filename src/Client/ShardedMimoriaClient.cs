// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Client.ConsistentHash;
using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

/// <summary>
/// An implementation of a sharded Mimoria client that can connect to multiple servers.
/// </summary>
public sealed class ShardedMimoriaClient : IShardedMimoriaClient
{
    private readonly Dictionary<int, IMimoriaClient> idMimoriaClients;
    private readonly IReadOnlyList<IMimoriaClient> mimoriaClients;
    private readonly IConsistentHashing consistentHashing;

    /// <inheritdoc />
    public IReadOnlyList<IMimoriaClient> MimoriaClients => this.mimoriaClients;

    /// <summary>
    /// Not supported.
    /// </summary>
    public int? ServerId => throw new NotSupportedException();

    /// <summary>
    /// Not supported.
    /// </summary>
    public bool IsConnected => throw new NotSupportedException();

    /// <summary>
    /// Not supported.
    /// </summary>
    public bool IsPrimary { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <summary>
    /// Creates a new instance of the <see cref="ShardedMimoriaClient"/> class.
    /// </summary>
    /// <param name="password">The password of the mimoria servers.</param>
    /// <param name="serverEndpoints">The server IP endpoints.</param>
    /// <exception cref="ArgumentException">If less than two server endpoints are provided.</exception>
    public ShardedMimoriaClient(string password, params ServerEndpoint[] serverEndpoints)
        : this(new ConsistentHashing(new Murmur3Hasher()), password, serverEndpoints)
    {

    }

    /// <summary>
    /// Creates a new instance of the <see cref="ShardedMimoriaClient"/> class.
    /// </summary>
    /// <param name="consistentHashing">The consistent hashing implementation to use.</param>
    /// <param name="password">The password of the mimoria servers.</param>
    /// <param name="serverEndpoints">The server endpoints.</param>
    /// <exception cref="ArgumentException">If less than two server endpoints are provided.</exception>
    public ShardedMimoriaClient(IConsistentHashing consistentHashing, string password, params ServerEndpoint[] serverEndpoints)
    {
        if (serverEndpoints.Length < 2)
        {
            throw new ArgumentException("At least two server endpoints required", nameof(serverEndpoints));
        }

        this.idMimoriaClients = new Dictionary<int, IMimoriaClient>();
        this.consistentHashing = consistentHashing;
        this.mimoriaClients = serverEndpoints
            .Select(serverEndpoint => new MimoriaClient(serverEndpoint.Host, serverEndpoint.Port, password))
            .ToList();
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            await mimoriaClient.ConnectAsync(cancellationToken);

            this.idMimoriaClients.Add(mimoriaClient.ServerId!.Value, mimoriaClient);
            this.consistentHashing.AddServerId(mimoriaClient.ServerId.Value);
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            await mimoriaClient.DisconnectAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetStringAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetStringAsync(string key, string? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetStringAsync(key, value, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await foreach (string value in mimoriaClient.GetListEnumerableAsync(key, cancellationToken))
        {
            yield return value;
        }
    }

    /// <inheritdoc />
    public async Task<ImmutableList<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetListAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.AddListAsync(key, value, ttl, valueTtl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveListAsync(string key, string value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.RemoveListAsync(key, value, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.ContainsListAsync(key, value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetObjectBinaryAsync(key, binarySerializable, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetObjectBinaryAsync<T>(key, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.ExistsAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.DeleteAsync(key, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetObjectJsonAsync<T>(key, jsonSerializerOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetObjectJsonAsync<T>(key, t, jsonSerializerOptions, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return await mimoriaClient.GetBytesAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        await mimoriaClient.SetBytesAsync(key, value, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
        => await this.GetStatsAsync(0, cancellationToken);

    /// <inheritdoc />
    public async Task<Stats> GetStatsAsync(int serverId, CancellationToken cancellationToken = default)
    {
        if (!this.idMimoriaClients.TryGetValue(serverId, out IMimoriaClient? mimoriaClient))
        {
            throw new ArgumentOutOfRangeException(nameof(serverId));
        }

        return await mimoriaClient.GetStatsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task SetCounterAsync(string key, long value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.SetCounterAsync(key, value, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> IncrementCounterAsync(string key, long increment, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.IncrementCounterAsync(key, increment, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> DecrementCounterAsync(string key, long decrement, bool fireAndForget = false, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, -decrement, fireAndForget, cancellationToken);

    /// <inheritdoc />
    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, increment: 0, fireAndForget: false, cancellationToken);

    /// <inheritdoc />
    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.GetMapValueAsync(key, subKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.SetMapValueAsync(key, subKey, subValue, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.GetMapAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(key);
        return mimoriaClient.SetMapAsync(key, map, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(channel);
        return mimoriaClient.SubscribeAsync(channel, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(channel);
        return mimoriaClient.UnsubscribeAsync(channel, cancellationToken);
    }

    /// <inheritdoc />
    public Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetMimoriaClient(channel);
        return mimoriaClient.PublishAsync(channel, payload, cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await this.DisconnectAsync();
}
