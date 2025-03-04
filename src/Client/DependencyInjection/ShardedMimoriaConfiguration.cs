// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;

namespace Varelen.Mimoria.Client.DependencyInjection;

public sealed class ShardedMimoriaConfiguration
{
    public string Password { get; set; } = string.Empty;
    public List<IPEndPoint> IPEndPoints { get; set; } = new List<IPEndPoint>();
}
