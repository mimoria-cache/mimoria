// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Server.Network;

public interface ITcpConnection
{
    ulong Id { get; }

    ValueTask SendAsync(IByteBuffer byteBuffer);
}
