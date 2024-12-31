// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Core;

public static class SocketExtensions
{
    /// <summary>
    /// Keeps sending data until all data from the buffer was sent.
    /// </summary>
    /// <param name="socket">The socket instance.</param>
    /// <param name="buffer">The buffer to send.</param>
    /// <param name="cancellationToken">The optional cancellation token.</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask SendAllAsync(this Socket socket, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int offset = 0;
        int total = buffer.Length;

        do
        {
            int sent = await socket.SendAsync(buffer[offset..total], SocketFlags.None, cancellationToken);
            offset += sent;
        }
        while (offset < total);
    }
}
