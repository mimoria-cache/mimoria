// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

public interface IBulkOperation
{
    void GetString(string key);
    void SetString(string key, string value, TimeSpan ttl = default);
    void IncrementCounter(string key, long increment = 1);
    void Exists(string key);
    void Delete(string key);
    Task<List<object?>> ExecuteAsync(CancellationToken cancellationToken = default);
}
