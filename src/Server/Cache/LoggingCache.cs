// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using Varelen.Mimoria.Core;

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

    public LoggingCache(ILogger<LoggingCache> logger, ICache cache)
    {
        this.logger = logger;
        this.cache = cache;
    }

    public Task<string?> GetStringAsync(string key)
    {
        this.logger.LogInformation("GetString: '{Key}'", key);
        return this.cache.GetStringAsync(key);
    }

    public async Task SetStringAsync(string key, string? value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetString: '{Key}'='{Value}' (ttl='{Ttl}')", key, value ?? "null", ttlMilliseconds);
        await this.cache.SetStringAsync(key, value, ttlMilliseconds);
    }

    public async Task SetBytesAsync(string key, byte[]? bytes, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetBytes: '{Key}'='{Value}' (ttl='{Ttl}')", key, Convert.ToHexString(bytes ?? []), ttlMilliseconds);
        await this.cache.SetBytesAsync(key, bytes, ttlMilliseconds);
    }

    public Task<byte[]?> GetBytesAsync(string key)
    {
        this.logger.LogInformation("GetBytes: '{Key}'", key);
        return this.cache.GetBytesAsync(key);
    }

    public IAsyncEnumerable<string> GetListAsync(string key)
    {
        this.logger.LogInformation("GetList: '{Key}'", key);
        return this.cache.GetListAsync(key);
    }

    public async Task AddListAsync(string key, string value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("AddList: '{Key}'='{Value}' (ttl='{Ttl}')", key, value, ttlMilliseconds);
        await this.cache.AddListAsync(key, value, ttlMilliseconds);
    }

    public async Task RemoveListAsync(string key, string value)
    {
        this.logger.LogInformation("RemoveList: '{Key}'='{Value}'", key, value);
        await this.cache.RemoveListAsync(key, value);
    }

    public Task<bool> ContainsListAsync(string key, string value)
    {
        this.logger.LogInformation("ContainsList: '{Key}'='{Value}'", key, value);
        return this.cache.ContainsListAsync(key, value);
    }

    public async Task SetCounterAsync(string key, long value)
    {
        this.logger.LogInformation("SetCounter: '{Key}'='{Value}'", key, value);
        await this.cache.SetCounterAsync(key, value);
    }

    public Task<long> IncrementCounterAsync(string key, long increment)
    {
        this.logger.LogInformation("IncrementCounter: '{Key}' + '{Increment}'", key, increment);
        return this.cache.IncrementCounterAsync(key, increment);
    }

    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey)
    {
        this.logger.LogInformation("GetMapValue: '{Key}' -> '{SubKey}'", key, subKey);
        return this.cache.GetMapValueAsync(key, subKey);
    }

    public async Task SetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetMapValue: '{Key}' -> '{SubKey}'='{Value}' (ttl='{Ttl}')", key, subKey, value, ttlMilliseconds);
        await this.cache.SetMapValueAsync(key, subKey, value, ttlMilliseconds);
    }

    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key)
    {
        this.logger.LogInformation("GetMap: '{Key}'", key);
        return this.cache.GetMapAsync(key);
    }

    public async Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetMap: '{Key}' = {Count} (tt='{Ttl}')", key, map.Count, ttlMilliseconds);
        await this.cache.SetMapAsync(key, map, ttlMilliseconds);
    }

    public Task<bool> ExistsAsync(string key)
    {
        this.logger.LogInformation("Exists: '{Key}'", key);
        return this.cache.ExistsAsync(key);
    }

    public async Task DeleteAsync(string key)
    {
        this.logger.LogInformation("Delete: '{Key}'", key);
        await this.cache.DeleteAsync(key);
    }

    public void Dispose()
    {
        this.cache.Dispose();
        GC.SuppressFinalize(this);
    }
}
