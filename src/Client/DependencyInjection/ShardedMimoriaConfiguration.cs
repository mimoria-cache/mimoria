// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.DependencyInjection;

/// <summary>
/// Represents the configuration settings for connecting to a sharded Mimoria server.
/// </summary>
public sealed class ShardedMimoriaConfiguration
{
    /// <summary>
    /// Gets or sets the password for authenticating with the Mimoria servers.
    /// 
    /// Default is an empty string.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of server endpoints for the sharded Mimoria servers.
    /// 
    /// Default is an empty list.
    /// </summary>
    public List<ServerEndpoint> Endpoints { get; set; } = new List<ServerEndpoint>();
}
