// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

namespace Varelen.Mimoria.Server.Cache;

public class LoggingCache : ICache
{
    private readonly ILogger<LoggingCache> logger;
    private readonly ICache cache;

    public ulong Size => this.cache.Size;

    public ulong Hits => this.cache.Hits;

    public ulong Misses => this.cache.Misses;

    public float HitRatio => this.cache.HitRatio;

    public LoggingCache(ILogger<LoggingCache> logger, ICache cache)
    {
        this.cache = cache;
        this.logger = logger;
    }

    public string? GetString(string key)
    {
        this.logger.LogInformation("GetString: {Key}", key);
        return cache.GetString(key);
    }

    public void SetString(string key, string? value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetString: {Key}={Value} (ttl={Ttl})", key, value ?? "null", ttlMilliseconds);
        cache.SetString(key, value, ttlMilliseconds);
    }

    public void SetBytes(string key, byte[]? bytes, uint ttlMilliseconds)
    {
        this.logger.LogInformation("SetBytes: {Key}={Value} (ttl={Ttl})", key, Convert.ToHexString(bytes ?? []), ttlMilliseconds);
        cache.SetBytes(key, bytes, ttlMilliseconds);
    }

    public byte[]? GetBytes(string key)
    {
        this.logger.LogInformation("GetBytes: {Key}", key);
        return cache.GetBytes(key);
    }

    public IEnumerable<string> GetList(string key)
    {
        this.logger.LogInformation("GetList: {Key}", key);
        return cache.GetList(key);
    }

    public void AddList(string key, string value, uint ttlMilliseconds)
    {
        this.logger.LogInformation("AddList: {Key}={Value} (ttl={Ttl})", key, value, ttlMilliseconds);
        cache.AddList(key, value, ttlMilliseconds);
    }

    public void RemoveList(string key, string value)
    {
        this.logger.LogInformation("RemoveList: {Key}={Value}", key, value);
        cache.RemoveList(key, value);
    }

    public bool ContainsList(string key, string value)
    {
        this.logger.LogInformation("ContainsList: {Key}={Value}", key, value);
        return cache.ContainsList(key, value);
    }

    public void SetCounter(string key, long value)
    {
        this.logger.LogInformation("SetCounter: {Key}={Value}", key, value);
        cache.SetCounter(key, value);
    }

    public long IncrementCounter(string key, long increment)
    {
        this.logger.LogInformation("IncrementCounter: {Key} + {Increment}", key, increment);
        return cache.IncrementCounter(key, increment);
    }

    public bool Exists(string key)
    {
        this.logger.LogInformation("Exists: {Key}", key);
        return cache.Exists(key);
    }

    public void Delete(string key)
    {
        this.logger.LogInformation("Delete: {Key}", key);
        cache.Delete(key);
    }
}
