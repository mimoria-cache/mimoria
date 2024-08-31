// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Retry;

public interface IRetryPolicy
{
    public Task<T> ExecuteAsync<T>(Func<Task<T>> function, CancellationToken cancellationToken = default);
}
