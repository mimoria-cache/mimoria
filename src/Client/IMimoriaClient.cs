// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Text.Json;

namespace Varelen.Mimoria.Client;

public interface IMimoriaClient
{
    /// <summary>
    /// <para>Gets the globally unique id of the server connected to.</para>
    /// 
    /// This is only set after the client successfully connected and authenticated.
    /// </summary>
    Guid? ServerId { get; }

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
    Task AddListAsync(string key, string value, TimeSpan ttl = default, CancellationToken cancellationToken = default);
    Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<bool> ContainsList(string key, string value, CancellationToken cancellationToken = default);

    Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new();
    Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) where T : new();
    Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default) where T : new();

    Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default);
    Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default);
}
