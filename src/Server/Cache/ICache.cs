// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Cache;

public interface ICache
{
    public ulong Size { get; }
    public ulong Hits { get; }
    public ulong Misses { get; }
    public float HitRatio { get; }

    string? GetString(string key);
    void SetString(string key, string? value, uint ttlMilliseconds);

    void SetBytes(string key, byte[]? bytes, uint ttlMilliseconds);
    byte[]? GetBytes(string key);

    IEnumerable<string> GetList(string key);
    void AddList(string key, string value, uint ttlMilliseconds);
    void RemoveList(string key, string value);

    bool Exists(string key);

    void Delete(string key);
}
