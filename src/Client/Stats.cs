// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

public readonly struct Stats
{
    /// <summary>
    /// The server uptime in seconds.
    /// </summary>
    public uint Uptime { get; init; }

    /// <summary>
    /// The current active TCP connections (clients) to the server.
    /// </summary>
    public ulong Connections { get; init; }

    /// <summary>
    /// The total entries (keys) in the cache.
    /// </summary>
    public ulong CacheSize { get; init; }

    /// <summary>
    /// The total cache hits since startup.
    /// </summary>
    public ulong CacheHits { get; init; }

    /// <summary>
    /// The total cache misses since startup.
    /// </summary>
    public ulong CacheMisses { get; init; }

    /// <summary>
    /// <para>The cache hit ratio rounded to two decimals places or how efficient the cache is used.</para>
    /// 
    /// <para>Calculated with the following formula:<br/>
    /// total cache hits / (total cache hits + total cache misses)</para>
    ///
    /// Can be converted to a percentage by multiplying with 100.
    /// </summary>
    public float CacheHitRatio { get; init; }

    public override string? ToString()
        => $"Uptime: {this.Uptime}, Connections: {this.Connections}, CacheSize: {this.CacheSize}, CacheHits: {this.CacheHits}, CacheMisses: {this.CacheMisses}, CacheHitRatio: {this.CacheHitRatio} ({this.CacheHitRatio * 100}%)";
}
