// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Options;

public class ServerOptions
{
    public string Ip { get; set; } = "127.0.0.1";
    public ushort Port { get; set; } = 6565;
    public ushort Backlog { get; set; } = 50;
    public string Password { get; set; } = "";
}
