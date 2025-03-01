// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Server.Cache;

public interface ICache : IDisposable
{
    public ulong Size { get; }
    public ulong Hits { get; }
    public ulong Misses { get; }
    public float HitRatio { get; }
    public ulong ExpiredKeys { get; }

    Task<string?> GetStringAsync(string key);
    Task SetStringAsync(string key, string? value, uint ttlMilliseconds);

    Task SetBytesAsync(string key, byte[]? bytes, uint ttlMilliseconds);
    Task<byte[]?> GetBytesAsync(string key);

    IAsyncEnumerable<string> GetListAsync(string key);
    Task AddListAsync(string key, string value, uint ttlMilliseconds);
    Task RemoveListAsync(string key, string value);
    Task<bool> ContainsListAsync(string key, string value);

    Task SetCounterAsync(string key, long value);
    Task<long> IncrementCounterAsync(string key, long increment);

    Task<MimoriaValue> GetMapValueAsync(string key, string subKey);
    Task SetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds);

    Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key);
    Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds);

    Task<bool> ExistsAsync(string key);

    Task DeleteAsync(string key);
}
