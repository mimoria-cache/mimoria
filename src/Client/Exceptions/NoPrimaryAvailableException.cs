// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Exceptions;

/// <summary>
/// Thrown if no primary node is available.
/// </summary>
public sealed class NoPrimaryAvailableException : InvalidOperationException
{
    public NoPrimaryAvailableException()
    {
    }

    public NoPrimaryAvailableException(string? message) : base(message)
    {
    }

    public NoPrimaryAvailableException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
