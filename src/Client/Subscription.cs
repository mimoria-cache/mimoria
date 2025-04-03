// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

/// <summary>
/// Represents a subscription to a channel.
/// </summary>
public sealed class Subscription
{
    /// <summary>
    /// Represents the method that will handle the payload event.
    /// </summary>
    /// <param name="payload">The payload.</param>
    public delegate ValueTask OnPayloadEventAsync(MimoriaValue payload);

    /// <summary>
    /// Occurs when a payload is received on the channel.
    /// </summary>
    public event OnPayloadEventAsync? Payload;

    /// <summary>
    /// Raises the <see cref="Payload"/> event with the given payload.
    /// </summary>
    /// <param name="payload">The payload.</param>
    public async ValueTask OnPayloadAsync(MimoriaValue payload)
    {
        if (this.Payload is not null)
        {
            foreach (OnPayloadEventAsync handler in this.Payload.GetInvocationList().Cast<OnPayloadEventAsync>())
            {
                await handler(payload);
            }
        }
    }
}
