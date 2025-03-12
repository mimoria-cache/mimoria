// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Retry;

public interface IRetryPolicy
{
    public Task ExecuteAsync(Func<Task> function, CancellationToken cancellationToken = default);
}

public interface IRetryPolicy<T>
{
    public Task<T> ExecuteAsync(Func<Task<T>> function, CancellationToken cancellationToken = default);
}
