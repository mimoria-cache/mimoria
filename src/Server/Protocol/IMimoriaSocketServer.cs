// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.Protocol;

public interface IMimoriaSocketServer : ISocketServer
{
    void SetOperationHandler(Operation operation, Func<uint, TcpConnection, IByteBuffer, ValueTask> handler);
}
