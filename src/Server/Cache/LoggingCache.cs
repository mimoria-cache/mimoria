// SPDX-FileCopyrightText: 2024 varelen
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
        this.cache = cache;
        this.logger = logger;
    }

    public string? GetString(string key)
    {
        this.logger.LogInformation("GetString: '{Key}'", key);
        return this.cache.GetString(key);
    }

    public void SetString(string key, string? value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetString: '{Key}'='{Value}' (ttl='{Ttl}')", key, value ?? "null", ttlMilliseconds);
        this.cache.SetString(key, value, ttlMilliseconds);
    }

    public void SetBytes(string key, byte[]? bytes, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetBytes: '{Key}'='{Value}' (ttl='{Ttl}')", key, Convert.ToHexString(bytes ?? []), ttlMilliseconds);
        this.cache.SetBytes(key, bytes, ttlMilliseconds);
    }

    public byte[]? GetBytes(string key)
    {
        this.logger.LogInformation("GetBytes: '{Key}'", key);
        return this.cache.GetBytes(key);
    }

    public IEnumerable<string> GetList(string key)
    {
        this.logger.LogInformation("GetList: '{Key}'", key);
        return this.cache.GetList(key);
    }

    public void AddList(string key, string value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("AddList: '{Key}'='{Value}' (ttl='{Ttl}')", key, value, ttlMilliseconds);
        this.cache.AddList(key, value, ttlMilliseconds);
    }

    public void RemoveList(string key, string value)
    {
        this.logger.LogInformation("RemoveList: '{Key}'='{Value}'", key, value);
        this.cache.RemoveList(key, value);
    }

    public bool ContainsList(string key, string value)
    {
        this.logger.LogInformation("ContainsList: '{Key}'='{Value}'", key, value);
        return this.cache.ContainsList(key, value);
    }

    public void SetCounter(string key, long value)
    {
        this.logger.LogInformation("SetCounter: '{Key}'='{Value}'", key, value);
        this.cache.SetCounter(key, value);
    }

    public long IncrementCounter(string key, long increment)
    {
        this.logger.LogInformation("IncrementCounter: '{Key}' + '{Increment}'", key, increment);
        return this.cache.IncrementCounter(key, increment);
    }

    public MimoriaValue GetMapValue(string key, string subKey)
    {
        this.logger.LogInformation("GetMapValue: '{Key}' -> '{SubKey}'", key, subKey);
        return this.cache.GetMapValue(key, subKey);
    }

    public void SetMapValue(string key, string subKey, MimoriaValue value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetMapValue: '{Key}' -> '{SubKey}'='{Value}' (ttl='{Ttl}')", key, subKey, value, ttlMilliseconds);
        this.cache.SetMapValue(key, subKey, value, ttlMilliseconds);
    }

    public Dictionary<string, MimoriaValue> GetMap(string key)
    {
        this.logger.LogInformation("GetMap: '{Key}'", key);
        return this.cache.GetMap(key);
    }

    public void SetMap(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetMap: '{Key}' = {Count} (tt='{Ttl}')", key, map.Count, ttlMilliseconds);
        this.cache.SetMap(key, map, ttlMilliseconds);
    }

    public bool Exists(string key)
    {
        this.logger.LogInformation("Exists: '{Key}'", key);
        return this.cache.Exists(key);
    }

    public void Delete(string key)
    {
        this.logger.LogInformation("Delete: '{Key}'", key);
        this.cache.Delete(key);
    }

    public void Dispose()
    {
        this.cache.Dispose();
        GC.SuppressFinalize(this);
    }
}
