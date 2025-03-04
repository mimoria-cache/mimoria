// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

public interface IClusterMimoriaClient : IMimoriaClient
{
    Task<string?> GetStringAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the list at the key without allocating a new list instance.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    IAsyncEnumerable<string> GetListEnumerableAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);
    
    Task<List<string>> GetListAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);
    
    Task<bool> ContainsListAsync(string key, string value, bool preferSecondary, CancellationToken cancellationToken = default);

    Task<T?> GetObjectBinaryAsync<T>(string key, bool preferSecondary, CancellationToken cancellationToken = default) where T : IBinarySerializable, new();

    Task<T?> GetObjectJsonAsync<T>(string key, bool preferSecondary, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default);

    Task<byte[]?> GetBytesAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);

    Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool preferSecondary, CancellationToken cancellationToken = default);

    Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);

    ValueTask<bool> ExistsAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default);
}
