// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Client.ConsistentHash;

namespace Varelen.Mimoria.Client;

public sealed class ShardedMimoriaClient : IShardedMimoriaClient
{
    private readonly Dictionary<Guid, IMimoriaClient> idMimoriaClients = [];
    private readonly IReadOnlyList<IMimoriaClient> mimoriaClients;
    private readonly IConsistentHashing consistentHashing;

    public Guid? ServerId => null;

    public IReadOnlyList<IMimoriaClient> CacheClients => mimoriaClients;

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
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        return await mimoriaClient.GetStringAsync(key, cancellationToken);
    }

    public async Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        await mimoriaClient.SetStringAsync(key, value, ttl, cancellationToken);
    }

    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        await foreach (string s in mimoriaClient.GetListEnumerableAsync(key, cancellationToken))
        {
            yield return s;
        }
    }

    public async Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        return await mimoriaClient.GetListAsync(key, cancellationToken);
    }

    public async Task AddListAsync(string key, string value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        await mimoriaClient.AddListAsync(key, value, ttl, cancellationToken);
    }

    public async Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        await mimoriaClient.RemoveListAsync(key, value, cancellationToken);
    }

    public async Task<bool> ContainsList(string key, string value, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        return await mimoriaClient.ContainsList(key, value, cancellationToken);
    }

    public async Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        await mimoriaClient.SetObjectBinaryAsync(key, binarySerializable, ttl, cancellationToken);
    }

    public async Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        return await mimoriaClient.GetObjectBinaryAsync<T>(key, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        return await mimoriaClient.ExistsAsync(key, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        await mimoriaClient.DeleteAsync(key, cancellationToken);
    }

    public async Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) where T : new()
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        return await mimoriaClient.GetObjectJsonAsync<T>(key, jsonSerializerOptions, cancellationToken);
    }

    public async Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default) where T : new()
    {
        IMimoriaClient mimoriaClient = this.GetCacheClient(key);
        await mimoriaClient.SetObjectJsonAsync<T>(key, t, jsonSerializerOptions, ttl, cancellationToken);
    }

    public async Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
        => await this.GetStatsAsync(0, cancellationToken);

    public async Task<Stats> GetStatsAsync(int index, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.mimoriaClients[index];
        return await mimoriaClient.GetStatsAsync(cancellationToken);
    }

    public async Task<Stats> GetStatsAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        IMimoriaClient? mimoriaClient = this.mimoriaClients.FirstOrDefault(mc => mc.ServerId == serverId);
        return mimoriaClient is not null
            ? await mimoriaClient.GetStatsAsync(cancellationToken)
            : throw new ArgumentNullException(nameof(serverId));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IMimoriaClient GetCacheClient(string key)
    {
        Guid serverId = this.consistentHashing.GetServerId(key);
        return this.idMimoriaClients[serverId];
    }
}
