// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.Protocol;

public interface IMimoriaSocketServer : ISocketServer
{
    public delegate Task TcpConnectionEvent(TcpConnection tcpConnection);

    public event TcpConnectionEvent? Disconnected;

    void SetOperationHandlers(Dictionary<Operation, Func<uint, TcpConnection, IByteBuffer, ValueTask>> operationHandlers);
}
