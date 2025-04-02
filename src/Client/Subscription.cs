// SPDX-FileCopyrightText: 2024 varelen
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
    public delegate void OnPayloadEvent(MimoriaValue payload);

    /// <summary>
    /// Occurs when a payload is received on the channel.
    /// </summary>
    public event OnPayloadEvent? Payload;

    /// <summary>
    /// Raises the <see cref="Payload"/> event with the given payload.
    /// </summary>
    /// <param name="payload">The payload.</param>
    public void OnPayload(MimoriaValue payload)
        => this.Payload?.Invoke(payload);
}
