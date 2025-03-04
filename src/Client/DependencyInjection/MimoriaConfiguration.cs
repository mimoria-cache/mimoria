// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.DependencyInjection;

public sealed class MimoriaConfiguration
{
    public string Ip { get; set; } = "127.0.0.1";
    public ushort Port { get; set; } = 6565;
    public string Password { get; set; } = string.Empty;
}
