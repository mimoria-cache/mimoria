// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

/// <summary>
/// Represents a bulk operation that can be executed against a Mimoria server.
/// </summary>
public interface IBulkOperation
{
    /// <summary>
    /// Adds a get string operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the string value to retrieve.</param>
    void GetString(string key);

    /// <summary>
    /// Adds a set string operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the string value to set.</param>
    /// <param name="value">The string value to set.</param>
    /// <param name="ttl">The time-to-live for the key-value pair.</param>
    void SetString(string key, string value, TimeSpan ttl = default);

    /// <summary>
    /// Adds an increment counter operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the counter to increment.</param>
    /// <param name="increment">The increment amount. Default is 1.</param>
    void IncrementCounter(string key, long increment = 1);

    /// <summary>
    /// Adds an exists operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    void Exists(string key);

    /// <summary>
    /// Adds a delete operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key to delete.</param>
    void Delete(string key);

    /// <summary>
    /// Executes all the operations in the bulk operation asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of results for each operation in the bulk operation (in the same order the methods were called).</returns>
    Task<List<object?>> ExecuteAsync(CancellationToken cancellationToken = default);
}
