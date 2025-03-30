// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Client.Retry;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.DependencyInjection;

/// <summary>
/// Represents the configuration settings for connecting to a Mimoria server.
/// </summary>
public sealed class MimoriaConfiguration
{
    /// <summary>
    /// Gets or sets the host of the Mimoria server to connect to (can be an IP or a DNS name).
    /// 
    /// Default is "127.0.0.1".
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

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

    /// <summary>
    /// Gets or sets the operation timeout for operations with the Mimoria server.
    /// 
    /// Default is 250 milliseconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets or sets the retry policy for connecting to the Mimoria server.
    /// 
    /// Default is an exponential retry policy with an initial delay of 1000 milliseconds, a maximum of 4 retries.
    /// </summary>
    public IRetryPolicy ConnectRetryPolicy { get; set; } = new ExponentialRetryPolicy(initialDelay: 1000, maxRetries: 4, typeof(TimeoutException));

    /// <summary>
    /// Gets or sets the retry policy for operations with the Mimoria server.
    /// 
    /// Default is an exponential retry policy with an initial delay of 1000 milliseconds, a maximum of 4 retries.
    /// </summary>
    public IRetryPolicy<IByteBuffer> OperationRetryPolicy { get; set; } = new ExponentialRetryPolicy<IByteBuffer>(initialDelay: 1000, maxRetries: 4, typeof(TimeoutException));
}
