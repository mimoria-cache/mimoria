// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Network;

public interface ISocketClient : IAsyncDisposable
{
    public delegate void SocketHandler();

    public event SocketHandler? Connected;
    public event SocketHandler? Disconnected;

    bool IsConnected { get; }

    ValueTask ConnectAsync(string hostnameOrIp, int port, CancellationToken cancellationToken = default);
    ValueTask SendAsync(IByteBuffer byteBuffer, CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(bool force = false, CancellationToken cancellationToken = default);
}
