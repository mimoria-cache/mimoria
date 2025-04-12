// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Caching.Distributed;

namespace Varelen.Mimoria.Client;

/// <summary>
/// A distributed cache implementation that uses a Mimoria client.
/// </summary>
public sealed class MimoriaDistributedCache : IDistributedCache
{
    private readonly IMimoriaClient mimoriaClient;

    /// <summary>
    /// Creates a new instance of <see cref="MimoriaDistributedCache"/>.
    /// </summary>
    /// <param name="mimoriaClient">The mimoria client to use.</param>
    public MimoriaDistributedCache(IMimoriaClient mimoriaClient)
        => this.mimoriaClient = mimoriaClient;

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        // TODO: Review how to properly wrap this. I mean it's better than .Result but still..
        return this.GetAsync(key).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        => this.mimoriaClient.GetBytesAsync(key, token);

    /// <inheritdoc />
    public void Refresh(string key)
    {
        // TODO: Implement?
    }

    /// <inheritdoc />
    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        // TODO: Implement?
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        // TODO: Review how to properly wrap this. I mean it's better than .Result but still..
        this.RemoveAsync(key).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken token = default)
        => this.mimoriaClient.DeleteAsync(key, fireAndForget: false, token);

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        // TODO: Review how to properly wrap this. I mean it's better than .Result but still..
        this.SetAsync(key, value, options).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        => this.mimoriaClient.SetBytesAsync(key, value, options.SlidingExpiration ?? default, fireAndForget: false, token);
}
