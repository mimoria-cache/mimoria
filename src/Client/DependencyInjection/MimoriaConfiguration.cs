// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.DependencyInjection;

/// <summary>
/// Represents the configuration settings for connecting to a Mimoria server.
/// </summary>
public sealed class MimoriaConfiguration
{
    /// <summary>
    /// Gets or sets the IP address of the Mimoria server.
    /// 
    /// Default is "127.0.0.1".
    /// </summary>
    public string Ip { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the port number of the Mimoria server.
    /// 
    /// Default is 6565.
    /// </summary>
    public ushort Port { get; set; } = 6565;

    /// <summary>
    /// Gets or sets the password for authenticating with the Mimoria server.
    /// 
    /// Default is an empty string.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
