// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Retry;

public abstract class RetryPolicyBase : IRetryPolicy
{
    private readonly byte maxRetries;
    private readonly Type[] transientExceptions;

    protected RetryPolicyBase(byte maxRetries, params Type[] transientExceptions)
    {
        this.maxRetries = maxRetries;
        this.transientExceptions = transientExceptions;
    }

    public virtual bool IsTransient(Type exceptionType)
        => this.transientExceptions.Contains(exceptionType);

    public abstract int GetDelay(byte currentRetry);

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> function, CancellationToken cancellationToken = default)
    {
        byte currentRetry = 0;

        while (true)
        {
            try
            {
                return await function.Invoke();
            }
            catch (Exception exception)
            {
                if (currentRetry == this.maxRetries || !this.IsTransient(exception.GetType()))
                {
                    throw;
                }
            }

            await Task.Delay(this.GetDelay(++currentRetry), cancellationToken);
        }
    }
}
