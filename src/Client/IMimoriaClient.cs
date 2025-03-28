﻿// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

public interface IMimoriaClient : IAsyncDisposable
{
    /// <summary>
    /// <para>Gets the globally unique id of the server connected to.</para>
    /// 
    /// This is only set after the client successfully connected and authenticated.
    /// </summary>
    int? ServerId { get; }

    /// <summary>
    /// Returns true if we are connected to the server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Only usable when connected to a cluster using the cluster client.
    /// </summary>
    bool IsPrimary { get; internal set; }

    /// <summary>
    /// Connects to the remote Mimoria instance.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the connect attempt.</param>
    /// <exception cref="ArgumentException">If the password provided is longer than 24 characters.</exception>
    /// <returns>A task that completes when the connection to the Mimoria instance is established.</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the remote Mimoria instance.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the disconnect attempt.</param>
    /// <returns></returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
    Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list at the key without allocating a new list instance.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    IAsyncEnumerable<string> GetListEnumerableAsync(string key, CancellationToken cancellationToken = default);
    Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default);
    Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, CancellationToken cancellationToken = default);
    Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default);

    Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new();
    Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default);
    Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default);
    Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the counter value for the key to the value.
    /// Overrides any value previously stored at key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the set counter operation.</param>
    /// <returns></returns>
    Task SetCounterAsync(string key, long value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the counter value for the key by the given increment amount.
    /// </summary>
    /// <param name="key">The key where the counter is stored.</param>
    /// <param name="increment">The increment amount.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new value.</returns>
    Task<long> IncrementCounterAsync(string key, long increment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrements the counter value for the key by the given decrement amount.
    /// </summary>
    /// <param name="key">The key where the counter is stored.</param>
    /// <param name="increment">The decrement amount.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new value.</returns>
    Task<long> DecrementCounterAsync(string key, long decrement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the counter value for the key.
    /// 
    /// Shortcut for IncrementCounterAsync(key, increment: 0).
    /// </summary>
    /// <param name="key">The key where the counter is stored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new value.</returns>
    Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default);

    Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken = default);

    Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default);

    Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default);

    Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default);

    IBulkOperation Bulk();
}
