// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Exceptions;

/// <summary>
/// Thrown if no primary node is available.
/// </summary>
public sealed class NoPrimaryAvailableException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoPrimaryAvailableException"/> class.
    /// </summary>
    public NoPrimaryAvailableException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoPrimaryAvailableException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NoPrimaryAvailableException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoPrimaryAvailableException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public NoPrimaryAvailableException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
