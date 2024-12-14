// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.PubSub;

public interface IPubSubService : IDisposable
{
    void Subscribe(string channel, TcpConnection tcpConnection);
    void Unsubscribe(string channel, TcpConnection tcpConnection);
    void Unsubscribe(TcpConnection tcpConnection);
    ValueTask PublishAsync(string channel, MimoriaValue payload);
}
