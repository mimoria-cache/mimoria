// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Network;

public interface ISocketServer
{
    public ulong Connections { get; }

    void Start(string ip, ushort port, ushort backlog = 50);
    void Stop();
}
