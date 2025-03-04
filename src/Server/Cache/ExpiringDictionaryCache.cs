// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Cache.Locking;
using Varelen.Mimoria.Server.PubSub;

namespace Varelen.Mimoria.Server.Cache;

/// <summary>
/// An implementation of an async expiring in-memory key-value cache that is thread-safe.
/// </summary>
public sealed class ExpiringDictionaryCache : ICache
{
    // Prime number to favor the dictionary implementation
    private const int InitialCacheSize = 1009;
    private const int InitialLocksCacheSize = 1009;

    private const int InfiniteTimeToLive = 0;

    private readonly ILogger<ExpiringDictionaryCache> logger;
    private readonly IPubSubService pubSubService;
    private readonly ConcurrentDictionary<string, Entry<object?>> cache;
    private readonly AutoRemovingAsyncKeyedLocking autoRemovingAsyncKeyedLocking;

    private readonly PeriodicTimer? periodicTimer;
    private readonly TimeSpan expireCheckInterval;

    private ulong hits;
    private ulong misses;
    private ulong expiredKeys;

    public ulong Size => (ulong)this.cache.Count;

    public ulong Hits => Interlocked.Read(ref this.hits);

    public ulong Misses => Interlocked.Read(ref this.misses);

    public float HitRatio => this.Hits + this.Misses > 0 ? (float)Math.Round((float)this.Hits / (this.Hits + this.Misses), digits: 2) : 0;

    public ulong ExpiredKeys => Interlocked.Read(ref this.expiredKeys);

    public AutoRemovingAsyncKeyedLocking AutoRemovingAsyncKeyedLocking
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.autoRemovingAsyncKeyedLocking;
    }

    public ExpiringDictionaryCache(ILogger<ExpiringDictionaryCache> logger, IPubSubService pubSubService, TimeSpan expireCheckInterval)
    {
        this.pubSubService = pubSubService;
        this.logger = logger;
        this.expireCheckInterval = expireCheckInterval;
        this.cache = new ConcurrentDictionary<string, Entry<object?>>(concurrencyLevel: Environment.ProcessorCount, capacity: InitialCacheSize);
        this.autoRemovingAsyncKeyedLocking = new AutoRemovingAsyncKeyedLocking(InitialLocksCacheSize);
        this.hits = 0;
        this.misses = 0;
        this.expiredKeys = 0;

        if (expireCheckInterval != TimeSpan.Zero)
        {
            this.periodicTimer = new PeriodicTimer(this.expireCheckInterval);
            _ = Task.Run(async () => await this.PeriodicallyClearExpiredKeysAsync());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<(bool Expired, Entry<object?>? Entry)> TryGetAndHandleExpireAsync(string key)
    {
        this.autoRemovingAsyncKeyedLocking.DebugAssertKeyHasReleaserLock(key);

        if (!this.cache.TryGetValue(key, out Entry<object?>? foundEntry))
        {
            this.IncrementMisses();
            return (Expired: true, Entry: null);
        }

        if (!foundEntry.Expired)
        {
            return (Expired: false, Entry: foundEntry);
        }

        bool removed = this.cache.Remove(key, out _);
        // TODO: Review if there is an edge case and removed might be false?
        // We have the lock, and we got an entry, so nobody could possible have removed it in the meantime?
        Debug.Assert(removed, "Expired key was not removed");

        this.IncrementMisses();
        this.IncrementExpiredKeys();

        await this.pubSubService.PublishAsync(Channels.KeyExpiration, key);

        return (Expired: true, Entry: null);
    }

    public async Task<string?> GetStringAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return null;
        }

        var value = entry!.Value as string
            ?? throw new ArgumentException($"Value stored under '{key}' is not a string");

        this.IncrementHits();

        return value;
    }

    public async Task SetStringAsync(string key, string? value, uint ttlMilliseconds, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        this.cache[key] = new Entry<object?>(value, ttlMilliseconds);
    }

    public async Task SetBytesAsync(string key, byte[]? bytes, uint ttlMilliseconds, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        this.cache[key] = new Entry<object?>(bytes, ttlMilliseconds);
    }

    public async Task<byte[]?> GetBytesAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return null;
        }

        var bytes = entry!.Value as byte[]
            ?? throw new ArgumentException($"Value stored under '{key}' is not bytes");

        this.IncrementHits();

        return bytes;
    }

    public async IAsyncEnumerable<string> GetListAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            yield break;
        }

        var list = entry!.Value as List<string>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

        this.IncrementHits();

        foreach (string item in list)
        {
            yield return item;
        }
    }

    public async Task AddListAsync(string key, string value, uint ttlMilliseconds, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        if (!this.cache.TryGetValue(key, out Entry<object?>? entry))
        {
            this.cache[key] = new Entry<object?>(new List<string> { value }, ttlMilliseconds);
            return;
        }

        var list = entry!.Value as List<string>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

        list.Add(value);
    }

    public async Task RemoveListAsync(string key, string value, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return;
        }

        var list = entry!.Value as List<string>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

        list.Remove(value);

        // TODO: Auto removal configurable?
        if (list.Count == 0)
        {
            this.cache.Remove(key, out _);
        }
    }

    public async Task<bool> ContainsListAsync(string key, string value, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return false;
        }

        var list = entry!.Value as List<string>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

        this.IncrementHits();

        return list.Contains(value);
    }

    public async Task SetCounterAsync(string key, long value, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        this.cache[key] = new Entry<object?>(value, InfiniteTimeToLive);
    }

    public async Task<long> IncrementCounterAsync(string key, long increment, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        if (!this.cache.TryGetValue(key, out Entry<object?>? entry))
        {
            this.IncrementMisses();

            this.cache[key] = new Entry<object?>(increment, InfiniteTimeToLive);
            return increment;
        }

        this.IncrementHits();

        var counterValue = entry.Value as long?
            ?? throw new ArgumentException($"Value stored under '{key}' is not a counter");

        long newCounterValue = counterValue + increment;

        entry.Value = newCounterValue;

        return newCounterValue;
    }

    public async Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return MimoriaValue.Null;
        }

        this.IncrementHits();

        var map = entry!.Value as Dictionary<string, MimoriaValue>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a map");

        if (!map.TryGetValue(subKey, out MimoriaValue value))
        {
            return MimoriaValue.Null;
        }

        return value;
    }

    public async Task SetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);
        
        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            this.cache[key] = new Entry<object?>(new Dictionary<string, MimoriaValue>() { { subKey, value } }, InfiniteTimeToLive);
            return;
        }

        this.IncrementHits();

        var map = entry!.Value as Dictionary<string, MimoriaValue>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a map");

        map[subKey] = value;
    }

    public async Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);
        
        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return [];
        }

        this.IncrementHits();

        return entry!.Value as Dictionary<string, MimoriaValue>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a map");
    }

    public async Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);
        
        this.cache[key] = new Entry<object?>(map, ttlMilliseconds);
    }

    public async Task<bool> ExistsAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        return this.cache.ContainsKey(key);
    }

    public async Task DeleteAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        _ = this.cache.Remove(key, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementHits()
        => Interlocked.Increment(ref this.hits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementMisses()
        => Interlocked.Increment(ref this.misses);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementExpiredKeys()
        => Interlocked.Increment(ref this.expiredKeys);

    private async Task PeriodicallyClearExpiredKeysAsync()
    {
        Debug.Assert(this.periodicTimer is not null, "Periodic timer is null");

        try
        {
            while (await this.periodicTimer!.WaitForNextTickAsync())
            {
                int keysDeleted = 0;
                foreach (string key in this.cache.Keys)
                {
                    using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key);

                    if (!this.cache.TryGetValue(key, out Entry<object?>? entry))
                    {
                        continue;
                    }

                    if (!entry.Expired)
                    {
                        continue;
                    }

                    this.cache.Remove(key, out _);

                    keysDeleted++;
                    this.IncrementExpiredKeys();

                    await this.pubSubService.PublishAsync(Channels.KeyExpiration, key);
                }

                this.logger.LogInformation("Finished deleting '{TotalDeleted}' expired keys", keysDeleted);
            }
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Error during periodically cleanup of expired keys");
        }
    }

    public void Serialize(IByteBuffer byteBuffer)
    {
        byteBuffer.WriteVarUInt((uint)this.cache.Count);
        foreach (var (key, entry) in this.cache)
        {
            byteBuffer.WriteString(key);

            switch (entry.Value)
            {
                case long l:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.Counter);
                        byteBuffer.WriteLong(l);
                        break;
                    }
                case string s:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.String);
                        byteBuffer.WriteString(s);
                        break;
                    }
                case List<string> list:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.List);
                        byteBuffer.WriteVarUInt((uint)list.Count);
                        foreach (string item in list)
                        {
                            byteBuffer.WriteString(item);
                        }
                        break;
                    }
                case Dictionary<string, MimoriaValue> map:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.Map);
                        byteBuffer.WriteVarUInt((uint)map.Count);
                        foreach (var (mapKey, mapValue) in map)
                        {
                            byteBuffer.WriteString(mapKey);
                            byteBuffer.WriteByte((byte)mapValue.Type);

                            switch (mapValue.Type)
                            {
                                case MimoriaValue.ValueType.Null:
                                    byteBuffer.WriteByte(0);
                                    break;
                                case MimoriaValue.ValueType.Bytes:
                                    byteBuffer.WriteBytes((byte[])mapValue.Value!);
                                    break;
                                case MimoriaValue.ValueType.String:
                                    byteBuffer.WriteString((string?)mapValue.Value);
                                    break;
                                case MimoriaValue.ValueType.Int:
                                    byteBuffer.WriteInt((int)mapValue.Value!);
                                    break;
                                case MimoriaValue.ValueType.Long:
                                    byteBuffer.WriteLong((long)mapValue.Value!);
                                    break;
                                case MimoriaValue.ValueType.Double:
                                    byteBuffer.WriteDouble((double)mapValue.Value!);
                                    break;
                                case MimoriaValue.ValueType.Bool:
                                    byteBuffer.WriteBool((bool)mapValue.Value!);
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    }
                default:
                    break;
            }

            byteBuffer.WriteVarUInt(entry.TtlMilliseconds);
        }
    }

    public void Deserialize(IByteBuffer byteBuffer)
    {
        uint count = byteBuffer.ReadVarUInt();

        // TODO: Allocate the cache dictionary with the given capacity

        for (uint i = 0; i < count; i++)
        {
            string key = byteBuffer.ReadString()!;

            var valueType = (CacheValueType)byteBuffer.ReadByte();
            object? obj = null;

            switch (valueType)
            {
                case CacheValueType.Null:
                    obj = null;
                    break;
                case CacheValueType.String:
                    obj = byteBuffer.ReadString();
                    break;
                case CacheValueType.List:
                    uint listCount = byteBuffer.ReadVarUInt();
                    var list = new List<string>(capacity: (int)listCount);

                    for (int j = 0; j < listCount; j++)
                    {
                        list.Add(byteBuffer.ReadString()!);
                    }

                    obj = list;
                    break;
                case CacheValueType.Map:
                    uint mapCount = byteBuffer.ReadVarUInt();

                    var dictionary = new Dictionary<string, MimoriaValue>(capacity: (int)mapCount);

                    for (int j = 0; j < mapCount; j++)
                    {
                        string mapKey = byteBuffer.ReadString()!;
                        var mapValue = MimoriaValue.Null;

                        var mapValueType = (MimoriaValue.ValueType)byteBuffer.ReadByte();

                        switch (mapValueType)
                        {
                            case MimoriaValue.ValueType.Null:
                                mapValue = MimoriaValue.Null;
                                break;
                            case MimoriaValue.ValueType.Bytes:
                                break;
                            case MimoriaValue.ValueType.String:
                                mapValue = byteBuffer.ReadString();
                                break;
                            case MimoriaValue.ValueType.Int:
                                mapValue = byteBuffer.ReadInt();
                                break;
                            case MimoriaValue.ValueType.Long:
                                mapValue = byteBuffer.ReadLong();
                                break;
                            case MimoriaValue.ValueType.Double:
                                mapValue = byteBuffer.ReadDouble();
                                break;
                            case MimoriaValue.ValueType.Bool:
                                mapValue = byteBuffer.ReadBool();
                                break;
                            default:
                                mapValue = MimoriaValue.Null;
                                break;
                        }

                        dictionary[mapKey] = mapValue;
                    }

                    obj = dictionary;
                    break;
                case CacheValueType.Counter:
                    obj = byteBuffer.ReadLong();
                    break;
                default:
                    break;
            }

            uint ttl = byteBuffer.ReadVarUInt();

            bool added = this.cache.TryAdd(key, new Entry<object?>(obj, ttl));
            Debug.Assert(added, $"Resynced key '{key}' was not added to cache");
        }
    }

    public void Dispose()
    {
        this.cache.Clear();
        this.periodicTimer?.Dispose();
        this.autoRemovingAsyncKeyedLocking.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class Entry<TValue>
    {
        internal TValue Value { get; set; }

        internal DateTime Added { get; set; } = DateTime.UtcNow;

        internal uint TtlMilliseconds { get; set; }

        internal bool Expired => this.TtlMilliseconds != InfiniteTimeToLive && (DateTime.UtcNow - this.Added).TotalMilliseconds >= this.TtlMilliseconds;

        public Entry(TValue value, uint ttlMilliseconds)
        {
            this.Value = value;
            this.TtlMilliseconds = ttlMilliseconds;
        }
    }
}
