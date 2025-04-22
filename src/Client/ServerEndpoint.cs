// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

/// <summary>
/// Represents a server endpoint with a host and port.
/// </summary>
public sealed class ServerEndpoint
{
    /// <summary>
    /// Gets or sets the host name or IP address of the server.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the port number of the server.
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerEndpoint"/> class with the specified host name / IP and port.
    /// </summary>
    /// <param name="host">The host name or IP.</param>
    /// <param name="port">The port.</param>
    public ServerEndpoint(string host, ushort port)
    {
        this.Host = host;
        this.Port = port;
    }
}
