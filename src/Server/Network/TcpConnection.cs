// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net.Sockets;

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Server.Network;

public sealed class TcpConnection
{
    public Socket Socket { get; set; }
    public bool Authenticated { get; set; }

    public ValueTask SendAsync(IByteBuffer byteBuffer)
    {
        throw new NotImplementedException();
    }
}
