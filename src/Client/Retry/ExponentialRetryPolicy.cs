// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Retry;

public sealed class ExponentialRetryPolicy : RetryPolicyBase
{
    private readonly int initialDelay;

    public ExponentialRetryPolicy(int initialDelay, byte maxRetries, params Type[] transientExceptions)
        : base(maxRetries, transientExceptions)
    {
        this.initialDelay = initialDelay;
    }

    public override int GetDelay(byte currentRetry)
        => (int)(Math.Pow(2, currentRetry - 1) * this.initialDelay);
}

public sealed class ExponentialRetryPolicy<T> : RetryPolicyBase<T>
{
    private readonly int initialDelay;

    public ExponentialRetryPolicy(int initialDelay, byte maxRetries, params Type[] transientExceptions)
        : base(maxRetries, transientExceptions)
    {
        this.initialDelay = initialDelay;
    }

    public override int GetDelay(byte currentRetry)
        => (int)(Math.Pow(2, currentRetry - 1) * this.initialDelay);
}
