// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Network;

/// <summary>
/// Represents a client that communicates with a server using sockets.
/// </summary>
public interface ISocketClient : IAsyncDisposable
{
    /// <summary>
    /// Represents the method that will handle socket events.
    /// </summary>
    public delegate void SocketHandler();

    /// <summary>
    /// Is triggered when the client is connected to the server.
    /// </summary>
    public event SocketHandler? Connected;

    /// <summary>
    /// Is triggered when the client is disconnected from the server.
    /// </summary>
    public event SocketHandler? Disconnected;

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects the client to the server asynchronously.
    /// </summary>
    /// <param name="hostnameOrIp">The hostname or IP address of the server.</param>
    /// <param name="port">The port number of the server.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A value task that represents the asynchronous operation.</returns>
    ValueTask ConnectAsync(string hostnameOrIp, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends data to the server asynchronously.
    /// </summary>
    /// <param name="byteBuffer">The buffer containing the data to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A value task that represents the asynchronous operation.</returns>
    ValueTask SendAsync(IByteBuffer byteBuffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the client from the server asynchronously.
    /// </summary>
    /// <param name="force">A value indicating whether to force the disconnection (if forced, the client will not reconnect).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A value task that represents the asynchronous operation.</returns>
    ValueTask DisconnectAsync(bool force = false, CancellationToken cancellationToken = default);
}
