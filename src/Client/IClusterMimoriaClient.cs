// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

/// <summary>
/// The interface for a Mimoria client that can connect to a cluster of servers.
/// </summary>
public interface IClusterMimoriaClient : IMimoriaClient
{
    /// <summary>
    /// Returns the list of Mimoria clients in the cluster.
    /// </summary>
    public IReadOnlyList<IMimoriaClient> MimoriaClients { get; }

    /// <summary>
    /// Gets the server id of the current primary.
    /// </summary>
    new int? ServerId { get; }

    /// <summary>
    /// Returns true if we are connected to all servers.
    /// </summary>
    new bool IsConnected { get; }

    /// <summary>
    /// Is not supported for the cluster client.
    /// </summary>
    new bool IsPrimary { get; internal set; }

    /// <summary>
    /// Gets a string value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the string value to retrieve.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the string value.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<string?> GetStringAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list at the key without allocating a new list instance.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The async enumerable enumerating the values.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    IAsyncEnumerable<string> GetListEnumerableAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list to retrieve.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of string values.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<ImmutableList<string>> GetListAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a value exists in the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list to check.</param>
    /// <param name="value">The value to check for existence.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the value exists in the list.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<bool> ContainsListAsync(string key, string value, bool preferSecondary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an object of type <typeparamref name="T"/> in binary format associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve.</typeparam>
    /// <param name="key">The key of the object to retrieve.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the object of type <typeparamref name="T"/>.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<T?> GetObjectBinaryAsync<T>(string key, bool preferSecondary, CancellationToken cancellationToken = default) where T : IBinarySerializable, new();

    /// <summary>
    /// Gets an object of type <typeparamref name="T"/> in JSON format associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the object to retrieve.</typeparam>
    /// <param name="key">The key of the object to retrieve.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="jsonSerializerOptions">The JSON serializer options.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the object of type <typeparamref name="T"/>.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<T?> GetObjectJsonAsync<T>(string key, bool preferSecondary, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a byte array associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the byte array to retrieve.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the byte array.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<byte[]?> GetBytesAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value from a map associated with the specified key and sub-key.
    /// </summary>
    /// <param name="key">The key of the map.</param>
    /// <param name="subKey">The sub-key of the value to retrieve.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the map value.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool preferSecondary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a map associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the map to retrieve.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the map.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists on the server.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    /// <param name="preferSecondary">Whether to prefer a secondary node.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A value task that represents the asynchronous operation. The task result contains a boolean indicating whether the key exists.</returns>
    /// <exception cref="TimeoutException">The operation has timed out, even after retries.</exception>
    ValueTask<bool> ExistsAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);
}
