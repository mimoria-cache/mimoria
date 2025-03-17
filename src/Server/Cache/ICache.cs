// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Cache.Locking;

namespace Varelen.Mimoria.Server.Cache;

public interface ICache : IDisposable
{
    public ulong Size { get; }
    public ulong Hits { get; }
    public ulong Misses { get; }
    public float HitRatio { get; }
    public ulong ExpiredKeys { get; }
    public AutoRemovingAsyncKeyedLocking AutoRemovingAsyncKeyedLocking { get; }

    Task<string?> GetStringAsync(string key, bool takeLock = true);
    Task SetStringAsync(string key, string? value, uint ttlMilliseconds, bool takeLock = true);

    Task SetBytesAsync(string key, byte[]? bytes, uint ttlMilliseconds, bool takeLock = true);
    Task<byte[]?> GetBytesAsync(string key, bool takeLock = true);

    IAsyncEnumerable<string> GetListAsync(string key, bool takeLock = true);
    Task AddListAsync(string key, string value, uint ttlMilliseconds, uint maxCount, bool takeLock = true);
    Task RemoveListAsync(string key, string value, bool takeLock = true);
    Task<bool> ContainsListAsync(string key, string value, bool takeLock = true);

    Task SetCounterAsync(string key, long value, bool takeLock = true);
    Task<long> IncrementCounterAsync(string key, long increment, bool takeLock = true);

    Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool takeLock = true);
    Task SetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds, uint maxCount, bool takeLock = true);

    Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool takeLock = true);
    Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds, bool takeLock = true);

    Task<bool> ExistsAsync(string key, bool takeLock = true);

    Task DeleteAsync(string key, bool takeLock = true);

    void Serialize(IByteBuffer byteBuffer);

    void Deserialize(IByteBuffer byteBuffer);
}
