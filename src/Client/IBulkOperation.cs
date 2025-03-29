// SPDX-FileCopyrightText: 2025 varelen
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
    /// Adds a get list operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the list to retrieve.</param>
    /// <param name="value">The value to add to the list.</param>
    /// <param name="ttl">The time-to-live for the key.</param>
    /// <param name="valueTtl">The time-to-live for the value.</param>
    void AddList(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default);

    /// <summary>
    /// Adds a remove list operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the list to remove from.</param>
    /// <param name="value">The value to remove from the list.</param>
    void RemoveList(string key, string value);

    /// <summary>
    /// Adds a get list operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the list to retrieve.</param>
    void GetList(string key);

    /// <summary>
    /// Adds a contains list operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the list to check.</param>
    /// <param name="value">The value to check for in the list.</param>
    void ContainsList(string key, string value);

    /// <summary>
    /// Adds an increment counter operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the counter to increment.</param>
    /// <param name="increment">The increment amount. Default is 1.</param>
    void IncrementCounter(string key, long increment = 1);

    /// <summary>
    /// Adds a set counter operation to the bulk operation.
    /// </summary>
    /// <param name="key">The key of the counter to set.</param>
    /// <param name="value">The value to set the counter to.</param>
    void SetCounter(string key, long value);

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
