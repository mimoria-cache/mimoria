// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.PubSub;

public interface IPubSubService : IDisposable
{
    Task SubscribeAsync(string channel, ITcpConnection tcpConnection);
    ValueTask UnsubscribeAsync(string channel, ITcpConnection tcpConnection);
    Task UnsubscribeAsync(ITcpConnection tcpConnection);
    ValueTask PublishAsync(string channel, MimoriaValue payload);
}
