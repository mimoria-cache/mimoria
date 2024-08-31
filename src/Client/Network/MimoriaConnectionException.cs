// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.Network;

public sealed class MimoriaConnectionException : Exception
{
    public MimoriaConnectionException()
    {
    }

    public MimoriaConnectionException(string? message)
        : base(message)
    {
    }

    public MimoriaConnectionException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
