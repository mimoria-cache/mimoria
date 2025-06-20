﻿// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Caching.Memory;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

public sealed class MicrocacheMimoriaClient : IMimoriaClient, IShardedMimoriaClient
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(2);

    private readonly IMimoriaClient mimoriaClient;
    private readonly IMemoryCache memoryCache;
    private readonly TimeSpan expiration;
    private readonly ConcurrentDictionary<string, bool> keys;

    public int? ServerId => this.mimoriaClient.ServerId;

    public bool IsConnected => this.mimoriaClient.IsConnected;

    public bool IsPrimary
    {
        get => this.mimoriaClient.IsPrimary;
        set => this.mimoriaClient.IsPrimary = value;
    }

    public IReadOnlyList<IMimoriaClient> MimoriaClients => this.mimoriaClient is IShardedMimoriaClient s ? s.MimoriaClients : Array.Empty<IMimoriaClient>();

    /// <summary>
    /// Creates a <see cref="MicrocacheMimoriaClient"/> instance with the default expiration of 2 seconds.
    /// </summary>
    /// <param name="ip">The IP of the Mimoria server.</param>
    /// <param name="port">The port of the Mimoria server.</param>
    /// <param name="password">The password of the Mimoria server.</param>
    public MicrocacheMimoriaClient(string ip, ushort port, string password)
        : this(ip, port, password, DefaultExpiration)
    {

    }

    public MicrocacheMimoriaClient(string ip, ushort port, string password, TimeSpan expiration)
        : this(new MimoriaClient(ip, port, password), expiration)
    {

    }

    public MicrocacheMimoriaClient(IMimoriaClient mimoriaClient)
        : this(mimoriaClient, new MemoryCache(new MemoryCacheOptions()), DefaultExpiration)
    {
    }

    public MicrocacheMimoriaClient(IMimoriaClient mimoriaClient, TimeSpan expiration)
        : this(mimoriaClient, new MemoryCache(new MemoryCacheOptions()), expiration)
    {
    }

    public MicrocacheMimoriaClient(IMimoriaClient mimoriaClient, IMemoryCache memoryCache, TimeSpan expiration)
    {
        this.mimoriaClient = mimoriaClient;
        this.memoryCache = memoryCache;
        this.expiration = expiration;
        this.keys = new ConcurrentDictionary<string, bool>();
    }

    public Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? listObject))
        {
            var list = listObject as List<string>
                ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

            list!.Add(value);
        }
        else
        {
            this.memoryCache.Set(key, new List<string>() { value }, absoluteExpirationRelativeToNow: this.expiration);

            _ = this.keys.TryAdd(key, true);
        }

        return this.mimoriaClient.AddListAsync(key, value, ttl, valueTtl, fireAndForget, cancellationToken);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await this.mimoriaClient.ConnectAsync(cancellationToken);

        var expirationSubscription = await this.mimoriaClient.SubscribeAsync(Channels.KeyExpiration, cancellationToken);
        expirationSubscription.Payload += HandleExpirationAsync;
    }

    private ValueTask HandleExpirationAsync(MimoriaValue payload)
    {
        string key = (string)payload!;
        this.memoryCache.Remove(key);
        return ValueTask.CompletedTask;
    }

    public Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? listObject))
        {
            var list = listObject as List<string>
                ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

            if (list.Contains(value))
            {
                return Task.FromResult(true);
            }
        }

        return this.mimoriaClient.ContainsListAsync(key, value, cancellationToken);
    }

    public Task DeleteAsync(string key, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        this.memoryCache.Remove(key);
        _ = this.keys.TryRemove(key, out _);

        return this.mimoriaClient.DeleteAsync(key, fireAndForget, cancellationToken);
    }

    public Task<ulong> DeleteAsync(string pattern, Comparison comparison, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        switch (comparison)
        {
            case Comparison.StartsWith:
                {
                    foreach (var (key, _) in this.keys)
                    {
                        if (!key.AsSpan().StartsWith(pattern.AsSpan(), StringComparison.Ordinal))
                        {
                            continue;
                        }

                        this.memoryCache.Remove(key);
                        _ = this.keys.TryRemove(key, out _);
                    }
                    break;
                }
            case Comparison.EndsWith:
                {
                    foreach (var (key, _) in this.keys)
                    {
                        if (!key.AsSpan().EndsWith(pattern.AsSpan(), StringComparison.Ordinal))
                        {
                            continue;
                        }

                        this.memoryCache.Remove(key);
                        _ = this.keys.TryRemove(key, out _);
                    }
                    break;
                }
            case Comparison.Contains:
                {
                    foreach (var (key, _) in this.keys)
                    {
                        if (!key.AsSpan().Contains(pattern.AsSpan(), StringComparison.Ordinal))
                        {
                            continue;
                        }

                        this.memoryCache.Remove(key);
                        _ = this.keys.TryRemove(key, out _);
                    }
                    break;
                }
            default:
                break;
        }

        return this.mimoriaClient.DeleteAsync(pattern, comparison, fireAndForget, cancellationToken);
    }

    public Task ClearAsync(bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        foreach (var (key, _) in this.keys)
        {
            this.memoryCache.Remove(key);
        }

        this.keys.Clear();

        return this.mimoriaClient.ClearAsync(fireAndForget, cancellationToken);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
        => this.mimoriaClient.DisconnectAsync(cancellationToken);

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out _))
        {
            return ValueTask.FromResult(true);
        }

        return this.mimoriaClient.ExistsAsync(key, cancellationToken);
    }

    public Task<ImmutableList<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? listObject))
        {
            var list = listObject as List<string>
                ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

            return Task.FromResult(list!.ToImmutableList());
        }

        return this.mimoriaClient.GetListAsync(key, cancellationToken);
    }

    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ImmutableList<string> list = await this.GetListAsync(key, cancellationToken);
        foreach (string item in list)
        {
            yield return item;
        }
    }

    public Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        if (this.memoryCache.TryGetValue(key, out object? value))
        {
            return Task.FromResult((T?)value);
        }

        return this.mimoriaClient.GetObjectBinaryAsync<T>(key, cancellationToken);
    }

    public Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? value))
        {
            return Task.FromResult((T?)value);
        }

        return this.mimoriaClient.GetObjectJsonAsync<T>(key, jsonSerializerOptions, cancellationToken);
    }

    public Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
        => this.mimoriaClient.GetStatsAsync(cancellationToken);

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? cachedValueObject))
        {
            var cachedValue = cachedValueObject as string
                ?? throw new ArgumentException($"Value stored under '{key}' is not a string");

            return (string?)cachedValue;
        }

        string? value = await this.mimoriaClient.GetStringAsync(key, cancellationToken);
        
        this.memoryCache.Set(key, value);
        
        return value;
    }

    public Task RemoveListAsync(string key, string value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? listObject))
        {
            var list = listObject as List<string>
                ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

            list!.Remove(value);
        }

        return this.mimoriaClient.RemoveListAsync(key, value, fireAndForget, cancellationToken);
    }

    public Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        this.memoryCache.Set(key, binarySerializable, absoluteExpirationRelativeToNow: this.expiration);
        _ = this.keys.TryAdd(key, true);

        return this.mimoriaClient.SetObjectBinaryAsync(key, binarySerializable, ttl, fireAndForget, cancellationToken);
    }

    public Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        this.memoryCache.Set(key, t, absoluteExpirationRelativeToNow: this.expiration);
        _ = this.keys.TryAdd(key, true);

        return this.mimoriaClient.SetObjectJsonAsync(key, t, jsonSerializerOptions, ttl, fireAndForget, cancellationToken);
    }

    public Task SetStringAsync(string key, string? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        this.memoryCache.Set(key, value, absoluteExpirationRelativeToNow: this.expiration);
        _ = this.keys.TryAdd(key, true);

        return this.mimoriaClient.SetStringAsync(key, value, ttl, fireAndForget, cancellationToken);
    }

    public Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? valueObject))
        {
            var value = valueObject as byte[]
                ?? throw new ArgumentException($"Value stored under '{key}' is not bytes");

            return Task.FromResult<byte[]?>(value);
        }

        return this.mimoriaClient.GetBytesAsync(key, cancellationToken);
    }

    public Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        this.memoryCache.Set(key, value, absoluteExpirationRelativeToNow: this.expiration);
        _ = this.keys.TryAdd(key, true);

        return this.mimoriaClient.SetBytesAsync(key, value, ttl, fireAndForget, cancellationToken);
    }

    public Task SetCounterAsync(string key, long value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        this.memoryCache.Set(key, value, absoluteExpirationRelativeToNow: this.expiration);
        _ = this.keys.TryAdd(key, true);

        return this.mimoriaClient.SetCounterAsync(key, value, fireAndForget, cancellationToken);
    }

    public Task<long> IncrementCounterAsync(string key, long increment, bool fireAndForget = false, CancellationToken cancellationToken = default)
        => this.mimoriaClient.IncrementCounterAsync(key, increment, fireAndForget, cancellationToken);

    public Task<long> DecrementCounterAsync(string key, long decrement, bool fireAndForget = false, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, -decrement, fireAndForget, cancellationToken);

    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, increment: 0, fireAndForget: false, cancellationToken);

    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? mapObject))
        {
            var map = mapObject as Dictionary<string, MimoriaValue>
                ?? throw new ArgumentException($"Value stored under '{key}' is not a map");

            if (map.TryGetValue(subKey, out MimoriaValue value))
            {
                return Task.FromResult(value);
            }
        }

        return this.mimoriaClient.GetMapValueAsync(key, subKey, cancellationToken);
    }

    public Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? mapObject))
        {
            var map = mapObject as Dictionary<string, MimoriaValue>
                ?? throw new ArgumentException($"Value stored under '{key}' is not a map");

            map[subKey] = subValue;
        }
        else
        {
            this.memoryCache.Set(key, new Dictionary<string, MimoriaValue> { { subKey, subValue } }, absoluteExpirationRelativeToNow: this.expiration);

            _ = this.keys.TryAdd(key, true);
        }

        return this.mimoriaClient.SetMapValueAsync(key, subKey, subValue, ttl, fireAndForget, cancellationToken);
    }

    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default)
    {
        if (this.memoryCache.TryGetValue(key, out object? mapObject))
        {
            var map = mapObject as Dictionary<string, MimoriaValue>
                ?? throw new ArgumentException($"Value stored under '{key}' is not a map");

            return Task.FromResult(map);
        }

        return this.mimoriaClient.GetMapAsync(key, cancellationToken);
    }

    public Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        this.memoryCache.Set(key, map, absoluteExpirationRelativeToNow: this.expiration);
        _ = this.keys.TryAdd(key, true);

        return this.mimoriaClient.SetMapAsync(key, map, ttl, fireAndForget, cancellationToken);
    }

    public IBulkOperation Bulk()
        => this.mimoriaClient.Bulk();

    public Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
        => this.mimoriaClient.SubscribeAsync(channel, cancellationToken);

    public Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
        => this.mimoriaClient.UnsubscribeAsync(channel, cancellationToken);

    public Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
        => this.mimoriaClient.PublishAsync(channel, payload, cancellationToken);

    public Task<Stats> GetStatsAsync(int serverId, CancellationToken cancellationToken = default)
    {
        if (this.mimoriaClient is ShardedMimoriaClient s)
        {
            return s.GetStatsAsync(serverId, cancellationToken);
        }

        return Task.FromResult<Stats>(default);
    }

    public async ValueTask DisposeAsync()
        => await this.DisconnectAsync();
}
