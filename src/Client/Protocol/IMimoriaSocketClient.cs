// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Client.Network;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Protocol;

/// <summary>
/// Represents a client that communicates with a Mimoria server using sockets.
/// </summary>
public interface IMimoriaSocketClient : ISocketClient
{
    /// <summary>
    /// Gets the collection of channels to which the client is currently subscribed.
    /// </summary>
    ICollection<(string Channel, List<Subscription> Subscriptions)> Subscriptions { get; }

    /// <summary>
    /// Sends a request to the server and waits for a response.
    /// </summary>
    /// <param name="requestId">The unique identifier for the request.</param>
    /// <param name="buffer">The buffer containing the request data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response buffer.</returns>
    Task<IByteBuffer> SendAndWaitForResponseAsync(uint requestId, IByteBuffer buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to the server without waiting for a response.
    /// </summary>
    /// <param name="buffer">The buffer containing the request data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A value task that represents the asynchronous operation.</returns>
    ValueTask SendAndForgetAsync(IByteBuffer buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to the specified channel.
    /// </summary>
    /// <param name="channel">The name of the channel to subscribe to.</param>
    /// <returns>A tuple containing the subscription object and a boolean indicating whether the client was already subscribed to the channel.</returns>
    (Subscription, bool AlreadySubscribed) Subscribe(string channel);

    internal void SubscribeInternal(string channel, List<Subscription> subscriptions);

    /// <summary>
    /// Unsubscribes from the specified channel.
    /// </summary>
    /// <param name="channel">The name of the channel to unsubscribe from.</param>
    /// <returns>True if the client was successfully unsubscribed; otherwise, false.</returns>
    bool Unsubscribe(string channel);
}
