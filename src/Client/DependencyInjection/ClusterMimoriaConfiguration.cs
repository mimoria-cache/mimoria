// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.DependencyInjection;

/// <summary>
/// Represents the configuration settings for connecting to a Mimoria cluster.
/// </summary>
public sealed class ClusterMimoriaConfiguration
{
    /// <summary>
    /// Gets or sets the password for authenticating with the Mimoria servers.
    /// 
    /// Default is an empty string.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of server endpoints for the cluster Mimoria servers.
    /// 
    /// Default is an empty list.
    /// </summary>
    public List<ServerEndpoint> Endpoints { get; set; } = new List<ServerEndpoint>();
}
