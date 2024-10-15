// SPDX-FileCopyrightText: 2024 varelen
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

    string? GetString(string key);
    void SetString(string key, string? value, uint ttlMilliseconds);

    void SetBytes(string key, byte[]? bytes, uint ttlMilliseconds);
    byte[]? GetBytes(string key);

    IEnumerable<string> GetList(string key);
    void AddList(string key, string value, uint ttlMilliseconds);
    void RemoveList(string key, string value);
    bool ContainsList(string key, string value);

    void SetCounter(string key, long value);
    long IncrementCounter(string key, long increment);

    MimoriaValue GetMapValue(string key, string subKey);
    void SetMapValue(string key, string subKey, MimoriaValue value, uint ttlMilliseconds);

    Dictionary<string, MimoriaValue> GetMap(string key);
    void SetMap(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds);

    bool Exists(string key);

    void Delete(string key);
}
