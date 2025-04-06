// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Extensions;

public static class TaskExtensions
{
    public static async Task WaitAsync(this Task @this, TimeSpan timeout, string timeoutMessage)
    {
        try
        {
            await @this.WaitAsync(timeout);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(timeoutMessage, exception);
        }
    }
}
