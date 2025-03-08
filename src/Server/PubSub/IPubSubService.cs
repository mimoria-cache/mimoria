// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.PubSub;

public interface IPubSubService : IDisposable
{
    Task SubscribeAsync(string channel, TcpConnection tcpConnection);
    Task UnsubscribeAsync(string channel, TcpConnection tcpConnection);
    Task UnsubscribeAsync(TcpConnection tcpConnection);
    Task PublishAsync(string channel, MimoriaValue payload);
}
