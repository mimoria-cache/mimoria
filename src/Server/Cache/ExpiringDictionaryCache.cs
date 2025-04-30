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
using Varelen.Mimoria.Server.Metrics;
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

    public const int InfiniteTimeToLive = 0;

    private readonly ILogger<ExpiringDictionaryCache> logger;
    private readonly IMimoriaMetrics metrics;
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

    public ExpiringDictionaryCache(ILogger<ExpiringDictionaryCache> logger, IMimoriaMetrics metrics, IPubSubService pubSubService, TimeSpan expireCheckInterval)
    {
        this.logger = logger;
        this.metrics = metrics;
        this.pubSubService = pubSubService;
        this.expireCheckInterval = expireCheckInterval;
        this.cache = new ConcurrentDictionary<string, Entry<object?>>(concurrencyLevel: Environment.ProcessorCount, capacity: InitialCacheSize, StringComparer.Ordinal);
        this.autoRemovingAsyncKeyedLocking = new AutoRemovingAsyncKeyedLocking(InitialLocksCacheSize);
        this.hits = 0;
        this.misses = 0;
        this.expiredKeys = 0;

        if (expireCheckInterval != TimeSpan.Zero)
        {
            this.periodicTimer = new PeriodicTimer(this.expireCheckInterval);
            _ = Task.Run(this.PeriodicallyClearExpiredKeysAsync);
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

    public async Task<ByteString?> GetStringAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return null;
        }

        if (entry!.Value is not null && entry.Value is not ByteString)
        {
            throw new ArgumentException($"Value stored under '{key}' is not a string");
        }

        this.IncrementHits();

        return Unsafe.As<ByteString?>(entry.Value);
    }

    public async Task SetStringAsync(string key, ByteString? value, uint ttlMilliseconds, bool takeLock = true)
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

        if (entry!.Value is not null && entry.Value is not byte[])
        {
            throw new ArgumentException($"Value stored under '{key}' is not bytes");
        }

        this.IncrementHits();

        return Unsafe.As<byte[]?>(entry.Value);
    }

    public async IAsyncEnumerable<ByteString> GetListAsync(string key, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            yield break;
        }

        var list = entry!.Value as ExpiringList<ByteString>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

        this.IncrementHits();

        // TODO: Try to clear expired with every get?

        foreach (var (value, _) in list.Get())
        {
            yield return value;
        }
    }

    public async Task AddListAsync(string key, ByteString value, uint ttlMilliseconds, uint valueTtlMilliseconds, uint maxCount, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        if (!this.cache.TryGetValue(key, out Entry<object?>? entry))
        {
            this.cache[key] = new Entry<object?>(new ExpiringList<ByteString>(value, valueTtlMilliseconds), ttlMilliseconds);
            
            await this.pubSubService.PublishAsync(Channels.ForListAdded(key), value);
            
            return;
        }

        var list = entry!.Value as ExpiringList<ByteString>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

        if (list.Count + 1 > maxCount)
        {
            throw new ArgumentException($"List under key '{key}' has reached its maximum count of '{maxCount}'");
        }

        list.Add(value, valueTtlMilliseconds);

        await this.pubSubService.PublishAsync(Channels.ForListAdded(key), value);
    }

    public async Task RemoveListAsync(string key, ByteString value, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return;
        }

        var list = entry!.Value as ExpiringList<ByteString>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a list");

        list.Remove(value);

        // TODO: Auto removal configurable?
        if (list.Count == 0)
        {
            await this.DeleteInternalAsync(key);
        }
    }

    public async Task<bool> ContainsListAsync(string key, ByteString value, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            return false;
        }

        var list = entry!.Value as ExpiringList<ByteString>
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

    public async Task SetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds, uint maxCount, bool takeLock = true)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);
        
        var (expired, entry) = await this.TryGetAndHandleExpireAsync(key);
        if (expired)
        {
            this.cache[key] = new Entry<object?>(new Dictionary<string, MimoriaValue>(StringComparer.Ordinal) { { subKey, value } }, InfiniteTimeToLive);
            return;
        }

        this.IncrementHits();

        var map = entry!.Value as Dictionary<string, MimoriaValue>
            ?? throw new ArgumentException($"Value stored under '{key}' is not a map");

        int countBefore = map.Count;

        map[subKey] = value;

        if (map.Count > countBefore && map.Count > maxCount)
        {
            bool removed = map.Remove(subKey);
            Debug.Assert(removed, "Subkey was not removed from map as it reached max allowed count");

            throw new ArgumentException($"Map under key '{key}' has reached its maximum count of '{maxCount}'");
        }
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

        await this.DeleteInternalAsync(key);
    }

    public async Task<ulong> DeleteAsync(string pattern, Comparison comparison, bool takeLock = true)
    {
        ulong deleted = 0;

        switch (comparison)
        {
            case Comparison.StartsWith:
                foreach (string key in this.cache.Keys)
                {
                    if (key.AsSpan().StartsWith(pattern.AsSpan(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

                    if (this.cache.TryRemove(key, out _))
                    {
                        deleted++;
                    }
                }

                break;
            case Comparison.EndsWith:
                foreach (string key in this.cache.Keys)
                {
                    if (key.AsSpan().EndsWith(pattern.AsSpan(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

                    if (this.cache.TryRemove(key, out _))
                    {
                        deleted++;
                    }
                }

                break;
            case Comparison.Contains:
                foreach (string key in this.cache.Keys)
                {
                    if (key.AsSpan().Contains(pattern.AsSpan(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

                    if (this.cache.TryRemove(key, out _))
                    {
                        deleted++;
                    }
                }

                break;
        }

        this.logger.LogDebug("Deleted '{Count}' keys matching pattern '{Pattern}' with comparison '{Comparison}'", deleted, pattern, comparison);
        return deleted;
    }

    public async Task ClearAsync(bool takeLock = true)
    {
        foreach (string key in this.cache.Keys)
        {
            using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key, takeLock);

            _ = this.cache.Remove(key, out _);
        }

        await this.pubSubService.PublishAsync(Channels.Clear, MimoriaValue.Null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask DeleteInternalAsync(string key)
    {
        Debug.Assert(this.autoRemovingAsyncKeyedLocking.HasActiveLock(key), $"We do not have a lock for key '{key}'");

        bool removed = this.cache.Remove(key, out _);
        if (!removed)
        {
            // Don't publish a key deletion again
            return ValueTask.CompletedTask;
        }

        return this.pubSubService.PublishAsync(Channels.KeyDeletion, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementHits()
    {
        Interlocked.Increment(ref this.hits);
        this.metrics.IncrementCacheHits();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementMisses()
    {
        Interlocked.Increment(ref this.misses);
        this.metrics.IncrementCacheMisses();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementExpiredKeys()
    {
        Interlocked.Increment(ref this.expiredKeys);
        this.metrics.IncrementCacheExpiredKeys();
    }

    private async Task PeriodicallyClearExpiredKeysAsync()
    {
        Debug.Assert(this.periodicTimer is not null, "Periodic timer is null");

        try
        {
            while (await this.periodicTimer!.WaitForNextTickAsync())
            {
                int keysDeleted = 0;
                int listValuesDeleted = 0;
                foreach (string key in this.cache.Keys)
                {
                    using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key);

                    if (!this.cache.TryGetValue(key, out Entry<object?>? entry))
                    {
                        continue;
                    }

                    if (!entry.Expired)
                    {
                        if (entry.Value is ExpiringList<string> list)
                        {
                            listValuesDeleted += list.RemoveExpired();
                        }

                        continue;
                    }

                    this.cache.Remove(key, out _);

                    keysDeleted++;
                    this.IncrementExpiredKeys();

                    await this.pubSubService.PublishAsync(Channels.KeyExpiration, key);
                }

                this.logger.LogInformation("Finished deleting '{TotalDeleted}' expired keys and '{TotalListValuesDeleted}' expired list values", keysDeleted, listValuesDeleted);
            }
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Error during periodically cleanup of expired keys");
        }
    }

    public async ValueTask SerializeAsync(IByteBuffer byteBuffer)
    {
        if (this.cache.IsEmpty)
        {
            return;
        }

        byteBuffer.WriteVarUInt((uint)this.cache.Count);

        foreach (var (key, entry) in this.cache)
        {
            using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(key);

            byteBuffer.WriteString(key);

            switch (entry.Value)
            {
                case byte[] bytes:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.Bytes);
                        byteBuffer.WriteVarUInt((uint)bytes.Length);
                        byteBuffer.WriteBytes(bytes);
                        break;
                    }
                case long l:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.Counter);
                        byteBuffer.WriteLong(l);
                        break;
                    }
                case ByteString s:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.String);
                        byteBuffer.WriteByteString(s);
                        break;
                    }
                case ExpiringList<ByteString> list:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.List);
                        byteBuffer.WriteVarUInt((uint)list.Count);
                        foreach (var (value, valueTtl) in list.Get())
                        {
                            byteBuffer.WriteByteString(value);
                            byteBuffer.WriteVarUInt(valueTtl);
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
                                    var bytes = (byte[])mapValue.Value!;
                                    byteBuffer.WriteVarUInt((uint)bytes.Length);
                                    byteBuffer.WriteBytes(bytes.AsSpan());
                                    break;
                                case MimoriaValue.ValueType.String:
                                    byteBuffer.WriteByteString((ByteString)mapValue.Value!);
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
                case null:
                    {
                        byteBuffer.WriteByte((byte)CacheValueType.Null);
                        break;
                    }
                default:
                    break;
            }

            byteBuffer.WriteVarUInt(entry.TtlMilliseconds);
        }

        this.logger.LogInformation("Serialized '{Count}' keys", this.cache.Count);
    }

    public void Deserialize(IByteBuffer byteBuffer)
    {
        if (byteBuffer.Size <= 6)
        {
            return;
        }

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
                    obj = byteBuffer.ReadByteString();
                    break;
                case CacheValueType.List:
                    uint listCount = byteBuffer.ReadVarUInt();
                    var list = new ExpiringList<ByteString>(capacity: (int)listCount);

                    for (int j = 0; j < listCount; j++)
                    {
                        list.Add(value: byteBuffer.ReadByteString()!, ttl: byteBuffer.ReadVarUInt());
                    }

                    obj = list;
                    break;
                case CacheValueType.Map:
                    uint mapCount = byteBuffer.ReadVarUInt();

                    var dictionary = new Dictionary<string, MimoriaValue>(capacity: (int)mapCount, StringComparer.Ordinal);

                    for (int j = 0; j < mapCount; j++)
                    {
                        string mapKey = byteBuffer.ReadString()!;
                        MimoriaValue mapValue;

                        var mapValueType = (MimoriaValue.ValueType)byteBuffer.ReadByte();

                        switch (mapValueType)
                        {
                            case MimoriaValue.ValueType.Null:
                                mapValue = MimoriaValue.Null;
                                break;
                            case MimoriaValue.ValueType.Bytes:
                                uint bytesLength = byteBuffer.ReadVarUInt();
                                var bytesValue = new byte[bytesLength];
                                byteBuffer.ReadBytes(bytesValue);
                                mapValue = bytesValue;
                                break;
                            case MimoriaValue.ValueType.String:
                                mapValue = byteBuffer.ReadByteString();
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
                case CacheValueType.Bytes:
                    uint length = byteBuffer.ReadVarUInt();
                    var bytes = new byte[length];
                    byteBuffer.ReadBytes(bytes);
                    obj = bytes;
                    break;
                default:
                    break;
            }

            uint ttl = byteBuffer.ReadVarUInt();

            bool added = this.cache.TryAdd(key, new Entry<object?>(obj, ttl));
            Debug.Assert(added, $"Resynced key '{key}' was not added to cache");
        }

        this.logger.LogInformation("Deserialized '{Count}' keys", count);
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

        internal DateTime Added { get; } = DateTime.UtcNow;

        internal uint TtlMilliseconds { get; set; }

        internal bool Expired => this.TtlMilliseconds != InfiniteTimeToLive && (DateTime.UtcNow - this.Added).TotalMilliseconds >= this.TtlMilliseconds;

        public Entry(TValue value, uint ttlMilliseconds)
        {
            this.Value = value;
            this.TtlMilliseconds = ttlMilliseconds;
        }
    }
}
