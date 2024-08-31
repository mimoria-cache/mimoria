// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Retry;

public sealed class LinearRetryPolicy : RetryPolicyBase
{
    private readonly int initialDelay;

    public LinearRetryPolicy(int initialDelay, byte maxRetries, params Type[] transientExceptions)
        : base(maxRetries, transientExceptions)
    {
        this.initialDelay = initialDelay;
    }

    public override int GetDelay(byte currentRetry)
        => currentRetry * this.initialDelay;
}
