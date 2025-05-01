// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

/// <summary>
/// A client implementation that lazily establishes a connection to a Mimoria server.
/// </summary>
public class LazyConnectingMimoriaClient : IMimoriaClient
{
    private readonly IMimoriaClient mimoriaClient;
    private readonly SemaphoreSlim connectionSemaphore = new(initialCount: 1, maxCount: 1);
    
    private bool connected;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyConnectingMimoriaClient"/> class.
    /// </summary>
    /// <param name="mimoriaClient">The underlying Mimoria client to delegate operations to.</param>
    public LazyConnectingMimoriaClient(IMimoriaClient mimoriaClient)
        => this.mimoriaClient = mimoriaClient;

    /// <inheritdoc />
    public int? ServerId => this.mimoriaClient.ServerId;

    /// <inheritdoc />
    public bool IsConnected => this.connected;

    /// <inheritdoc />
    public bool IsPrimary
    {
        get => this.mimoriaClient.IsPrimary;
        set => this.mimoriaClient.IsPrimary = value;
    }

    /// <summary>
    /// Ensures that the client is connected to the Mimoria server.
    /// </summary>
    /// <returns>A task that completes when the connection is established.</returns>
    protected async ValueTask EnsureConnectedAsync()
    {
        if (this.connected)
        {
            return;
        }

        await this.connectionSemaphore.WaitAsync();

        try
        {
            if (this.connected)
            {
                return;
            }

            await this.mimoriaClient.ConnectAsync();

            this.connected = true;
        }
        finally
        {
            this.connectionSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
        => await this.EnsureConnectedAsync();

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!this.connected)
        {
            return;
        }

        await this.mimoriaClient.DisconnectAsync(cancellationToken);
        
        this.connected = false;
    }

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetStringAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetStringAsync(string key, string? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.SetStringAsync(key, value, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await foreach (var value in this.mimoriaClient.GetListEnumerableAsync(key, cancellationToken))
        {
            yield return value;
        }
    }

    /// <inheritdoc />
    public async Task<ImmutableList<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetListAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.AddListAsync(key, value, ttl, valueTtl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveListAsync(string key, string value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.RemoveListAsync(key, value, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.ContainsListAsync(key, value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetObjectBinaryAsync<T>(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.SetObjectBinaryAsync(key, binarySerializable, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetObjectJsonAsync<T>(key, jsonSerializerOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.SetObjectJsonAsync(key, t, jsonSerializerOptions, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetBytesAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.SetBytesAsync(key, value, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.SetCounterAsync(key, value, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, long increment, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.IncrementCounterAsync(key, increment, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> DecrementCounterAsync(string key, long decrement, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.DecrementCounterAsync(key, decrement, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetCounterAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetMapValueAsync(key, subKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.SetMapValueAsync(key, subKey, subValue, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetMapAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.SetMapAsync(key, map, ttl, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.ExistsAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.DeleteAsync(key, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ulong> DeleteAsync(string pattern, Comparison comparison, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.DeleteAsync(pattern, comparison, fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ClearAsync(bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.ClearAsync(fireAndForget, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.GetStatsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        return await this.mimoriaClient.SubscribeAsync(channel, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.UnsubscribeAsync(channel, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
    {
        await this.EnsureConnectedAsync();

        await this.mimoriaClient.PublishAsync(channel, payload, cancellationToken);
    }

    /// <inheritdoc />
    public IBulkOperation Bulk()
    {
        // TODO: Change to BulkAsync?
#pragma warning disable CA2012 // Use ValueTasks correctly
        this.EnsureConnectedAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012 // Use ValueTasks correctly

        return this.mimoriaClient.Bulk();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        this.connectionSemaphore.Dispose();
        await this.mimoriaClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
