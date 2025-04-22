// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

/// <summary>
/// A cluster client implementation that lazily establishes a connection to the Mimoria servers.
/// </summary>
public sealed class LazyConnectingClusterMimoriaClient : LazyConnectingMimoriaClient, IClusterMimoriaClient
{
    private readonly IClusterMimoriaClient clusterMimoriaClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyConnectingClusterMimoriaClient"/> class.
    /// </summary>
    /// <param name="clusterMimoriaClient">The underlying cluster Mimoria client to delegate operations to.</param>
    public LazyConnectingClusterMimoriaClient(IClusterMimoriaClient clusterMimoriaClient)
        : base(clusterMimoriaClient)
    {
        this.clusterMimoriaClient = clusterMimoriaClient;
    }

    /// <inheritdoc />
    public IReadOnlyList<IMimoriaClient> MimoriaClients => this.clusterMimoriaClient.MimoriaClients;

    /// <inheritdoc />
    public async Task<bool> ContainsListAsync(string key, string value, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.ContainsListAsync(key, value, preferSecondary, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.ExistsAsync(key, preferSecondary, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetBytesAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.GetBytesAsync(key, preferSecondary, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetListAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.GetListAsync(key, preferSecondary, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, bool preferSecondary, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        await foreach (var value in this.clusterMimoriaClient.GetListEnumerableAsync(key, preferSecondary, cancellationToken))
        {
            yield return value;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.GetMapAsync(key, preferSecondary, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.GetMapValueAsync(key, subKey, preferSecondary, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectBinaryAsync<T>(string key, bool preferSecondary, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.GetObjectBinaryAsync<T>(key, preferSecondary, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectJsonAsync<T>(string key, bool preferSecondary, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.GetObjectJsonAsync<T>(key, preferSecondary, jsonSerializerOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.clusterMimoriaClient.GetStringAsync(key, preferSecondary, cancellationToken);
    }
}
