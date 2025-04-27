// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server;

public interface IMimoriaServer
{
    Task StartAsync();
    Task StopAsync();
}
