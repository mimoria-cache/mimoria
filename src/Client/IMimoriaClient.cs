// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

/// <summary>
/// Represents a client that communicates with a Mimoria server.
/// </summary>
public interface IMimoriaClient : IAsyncDisposable
{
    /// <summary>
    /// Gets the globally unique id of the server connected to.
    /// This is only set after the client successfully connected and authenticated.
    /// </summary>
    int? ServerId { get; }

    /// <summary>
    /// Returns true if the client is connected to the server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Indicates whether the client is connected to a primary server in a cluster.
    /// Only usable when connected to a cluster using the cluster client.
    /// </summary>
    bool IsPrimary { get; internal set; }

    /// <summary>
    /// Connects to the remote Mimoria instance.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the connect attempt.</param>
    /// <returns>A task that completes when the connection to the Mimoria instance is established.</returns>
    /// <exception cref="ArgumentException">Thrown if the password provided is longer than 4096 characters.</exception>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the remote Mimoria instance.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the disconnect attempt.</param>
    /// <returns>A task that completes when the disconnection from the Mimoria instance is finished.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a string value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the string value to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the string value.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a string value for the specified key.
    /// </summary>
    /// <param name="key">The key of the string value to set.</param>
    /// <param name="value">The string value to set.</param>
    /// <param name="ttl">The time-to-live for the key-value pair.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list at the specified key without allocating a new list instance.
    /// </summary>
    /// <param name="key">The key of the list to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>An asynchronous enumerable of string values in the list.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    IAsyncEnumerable<string> GetListEnumerableAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of string values.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a value to the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list to add the value to.</param>
    /// <param name="value">The value to add to the list.</param>
    /// <param name="ttl">The time-to-live for the key.</param>
    /// <param name="valueTtl">The time-to-live for the value.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list to remove the value from.</param>
    /// <param name="value">The value to remove from the list.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a value exists in the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list to check.</param>
    /// <param name="value">The value to check for existence.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the value exists in the list.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an object of type <typeparamref name="T"/> in binary format associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve.</typeparam>
    /// <param name="key">The key of the object to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the object of type <typeparamref name="T"/>.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new();

    /// <summary>
    /// Sets an object in binary format for the specified key.
    /// </summary>
    /// <param name="key">The key of the object to set.</param>
    /// <param name="binarySerializable">The object to set.</param>
    /// <param name="ttl">The time-to-live for the key-value pair.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an object of type <typeparamref name="T"/> in JSON format associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve.</typeparam>
    /// <param name="key">The key of the object to retrieve.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the object of type <typeparamref name="T"/>.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets an object in JSON format for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the object to set.</typeparam>
    /// <param name="key">The key of the object to set.</param>
    /// <param name="t">The object to set.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options.</param>
    /// <param name="ttl">The time-to-live for the key-value pair.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a byte array associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the byte array to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the byte array.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a byte array for the specified key.
    /// </summary>
    /// <param name="key">The key of the byte array to set.</param>
    /// <param name="value">The byte array to set.</param>
    /// <param name="ttl">The time-to-live for the key-value pair.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the counter value for the specified key.
    /// Overrides any value previously stored at the key.
    /// </summary>
    /// <param name="key">The key of the counter to set.</param>
    /// <param name="value">The value to set the counter to.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task SetCounterAsync(string key, long value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the counter value for the specified key by the given increment amount.
    /// </summary>
    /// <param name="key">The key of the counter to increment.</param>
    /// <param name="increment">The increment amount.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the new counter value.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<long> IncrementCounterAsync(string key, long increment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrements the counter value for the specified key by the given decrement amount.
    /// </summary>
    /// <param name="key">The key of the counter to decrement.</param>
    /// <param name="decrement">The decrement amount.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the new counter value.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<long> DecrementCounterAsync(string key, long decrement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the counter value for the specified key.
    /// Shortcut for IncrementCounterAsync(key, increment: 0).
    /// </summary>
    /// <param name="key">The key of the counter to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the counter value.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from a map associated with the specified key and sub-key.
    /// </summary>
    /// <param name="key">The key of the map.</param>
    /// <param name="subKey">The sub-key of the value to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the map value.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in a map for the specified key and sub-key.
    /// </summary>
    /// <param name="key">The key of the map.</param>
    /// <param name="subKey">The sub-key of the value to set.</param>
    /// <param name="subValue">The value to set.</param>
    /// <param name="ttl">The time-to-live for the key-value pair.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a map associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the map to retrieve.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the map.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a map for the specified key.
    /// </summary>
    /// <param name="key">The key of the map to set.</param>
    /// <param name="map">The map to set.</param>
    /// <param name="ttl">The time-to-live for the key-value pair.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in the server.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A value task that represents the asynchronous operation. The task result contains a boolean indicating whether the key exists.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a key from the server.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the server statistics.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the server statistics.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a specified channel.
    /// </summary>
    /// <param name="channel">The name of the channel to subscribe to.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the subscription object.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a specified channel.
    /// </summary>
    /// <param name="channel">The name of the channel to unsubscribe from.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a payload to a specified channel.
    /// </summary>
    /// <param name="channel">The name of the channel to publish to.</param>
    /// <param name="payload">The payload to publish.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a bulk operation.
    /// </summary>
    /// <returns>An object representing the bulk operation.</returns>
    IBulkOperation Bulk();
}
