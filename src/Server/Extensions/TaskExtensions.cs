// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Server.Extensions;

public static class TaskExtensions
{
    /// <summary>
    /// Waits for the task to complete within the specified timeout and throws a <see cref="TimeoutException"/> with the specified message if it times out.
    /// </summary>
    /// <param name="timeout">The timeout as a <see cref="TimeSpan"/>.</param>
    /// <param name="timeoutMessage">The timeout message.</param>
    /// <returns></returns>
    /// <exception cref="TimeoutException">If the operation timed out.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
