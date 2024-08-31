// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Network;

public sealed class MimoriaErrorStatusCodeException : Exception
{
    public MimoriaErrorStatusCodeException()
    {
    }

    public MimoriaErrorStatusCodeException(string? message)
        : base(message)
    {
    }

    public MimoriaErrorStatusCodeException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
