// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Cache.Locking;

namespace Varelen.Mimoria.Server.Cache;

public class LoggingCache : ICache
{
    private readonly ILogger<LoggingCache> logger;
    private readonly ICache cache;

    public ulong Size => this.cache.Size;

    public ulong Hits => this.cache.Hits;

    public ulong Misses => this.cache.Misses;

    public float HitRatio => this.cache.HitRatio;

    public ulong ExpiredKeys => this.cache.ExpiredKeys;

    public AutoRemovingAsyncKeyedLocking AutoRemovingAsyncKeyedLocking => this.cache.AutoRemovingAsyncKeyedLocking;

    public LoggingCache(ILogger<LoggingCache> logger, ICache cache)
    {
        this.logger = logger;
        this.cache = cache;
    }

    public Task<string?> GetStringAsync(string key, bool takeLock = true)
    {
        this.logger.LogInformation("GetString: '{Key}'", key);
        return this.cache.GetStringAsync(key, takeLock);
    }

    public async Task SetStringAsync(string key, string? value, uint ttlMilliseconds, bool takeLock = true)
    {
        this.logger.LogInformation("SetString: '{Key}'='{Value}' (ttl='{Ttl}')", key, value ?? "null", ttlMilliseconds);
        await this.cache.SetStringAsync(key, value, ttlMilliseconds, takeLock);
    }

    public async Task SetBytesAsync(string key, byte[]? bytes, uint ttlMilliseconds, bool takeLock = true)
    {
        this.logger.LogInformation("SetBytes: '{Key}'='{Value}' (ttl='{Ttl}')", key, Convert.ToHexString(bytes ?? []), ttlMilliseconds);
        await this.cache.SetBytesAsync(key, bytes, ttlMilliseconds, takeLock);
    }

    public Task<byte[]?> GetBytesAsync(string key, bool takeLock = true)
    {
        this.logger.LogInformation("GetBytes: '{Key}'", key);
        return this.cache.GetBytesAsync(key, takeLock);
    }

    public IAsyncEnumerable<string> GetListAsync(string key, bool takeLock = true)
    {
        this.logger.LogInformation("GetList: '{Key}'", key);
        return this.cache.GetListAsync(key, takeLock);
    }

    public async Task AddListAsync(string key, string value, uint ttlMilliseconds, uint maxCount, bool takeLock = true)
    {
        this.logger.LogInformation("AddList: '{Key}'='{Value}' (ttl='{Ttl}')", key, value, ttlMilliseconds);
        await this.cache.AddListAsync(key, value, ttlMilliseconds, maxCount, takeLock);
    }

    public async Task RemoveListAsync(string key, string value, bool takeLock = true)
    {
        this.logger.LogInformation("RemoveList: '{Key}'='{Value}'", key, value);
        await this.cache.RemoveListAsync(key, value, takeLock);
    }

    public Task<bool> ContainsListAsync(string key, string value, bool takeLock = true)
    {
        this.logger.LogInformation("ContainsList: '{Key}'='{Value}'", key, value);
        return this.cache.ContainsListAsync(key, value, takeLock);
    }

    public async Task SetCounterAsync(string key, long value, bool takeLock = true)
    {
        this.logger.LogInformation("SetCounter: '{Key}'='{Value}'", key, value);
        await this.cache.SetCounterAsync(key, value, takeLock);
    }

    public Task<long> IncrementCounterAsync(string key, long increment, bool takeLock = true)
    {
        this.logger.LogInformation("IncrementCounter: '{Key}' + '{Increment}'", key, increment);
        return this.cache.IncrementCounterAsync(key, increment, takeLock);
    }

    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool takeLock = true)
    {
        this.logger.LogInformation("GetMapValue: '{Key}' -> '{SubKey}'", key, subKey);
        return this.cache.GetMapValueAsync(key, subKey, takeLock);
    }

    public async Task SetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds, bool takeLock = true)
    {
        this.logger.LogInformation("SetMapValue: '{Key}' -> '{SubKey}'='{Value}' (ttl='{Ttl}')", key, subKey, value, ttlMilliseconds);
        await this.cache.SetMapValueAsync(key, subKey, value, ttlMilliseconds, takeLock);
    }

    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool takeLock = true)
    {
        this.logger.LogInformation("GetMap: '{Key}'", key);
        return this.cache.GetMapAsync(key, takeLock);
    }

    public async Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds, bool takeLock = true)
    {
        this.logger.LogInformation("SetMap: '{Key}' = {Count} (tt='{Ttl}')", key, map.Count, ttlMilliseconds);
        await this.cache.SetMapAsync(key, map, ttlMilliseconds, takeLock);
    }

    public Task<bool> ExistsAsync(string key, bool takeLock = true)
    {
        this.logger.LogInformation("Exists: '{Key}'", key);
        return this.cache.ExistsAsync(key, takeLock);
    }

    public async Task DeleteAsync(string key, bool takeLock = true)
    {
        this.logger.LogInformation("Delete: '{Key}'", key);
        await this.cache.DeleteAsync(key, takeLock);
    }

    public void Serialize(IByteBuffer byteBuffer)
    {
        this.cache.Serialize(byteBuffer);
    }

    public void Deserialize(IByteBuffer byteBuffer)
    {
        this.cache.Deserialize(byteBuffer);
    }

    public void Dispose()
    {
        this.cache.Dispose();
        GC.SuppressFinalize(this);
    }
}
