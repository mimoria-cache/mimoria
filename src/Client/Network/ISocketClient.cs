// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Network;

public interface ISocketClient : IDisposable
{
    public delegate void SocketHandler();

    public event SocketHandler? Connected;
    public event SocketHandler? Disconnected;

    bool IsConnected { get; }

    Task ConnectAsync(string ip, int port, CancellationToken cancellationToken = default);
    Task SendAsync(IByteBuffer byteBuffer, CancellationToken cancellationToken = default);
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);
}
