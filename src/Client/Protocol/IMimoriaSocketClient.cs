// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Client.Network;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Protocol;

public interface IMimoriaSocketClient : ISocketClient
{
    Task<IByteBuffer> SendAndWaitForResponseAsync(uint requestId, IByteBuffer buffer, CancellationToken cancellationToken = default);

    ValueTask SendAndForgetAsync(IByteBuffer buffer, CancellationToken cancellationToken = default);

    (Subscription, bool alreadySubscribed) Subscribe(string channel);

    bool Unsubscribe(string channel);
}
