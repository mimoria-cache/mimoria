// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Exceptions;

/// <summary>
/// Thrown if no secondary node is available.
/// </summary>
public sealed class NoSecondaryAvailableException : InvalidOperationException
{
    public NoSecondaryAvailableException()
    {
    }

    public NoSecondaryAvailableException(string? message) : base(message)
    {
    }

    public NoSecondaryAvailableException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
