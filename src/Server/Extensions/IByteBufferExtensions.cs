// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Server.Extensions;

public static class IByteBufferExtensions
{
    /// <summary>
    /// Reads a required key string and throws if it's null.
    /// </summary>
    /// <param name="this">The <see cref="IByteBuffer"/>.</param>
    /// <returns>The string key.</returns>
    /// <exception cref="ArgumentException">If the string key is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ReadRequiredKey(this IByteBuffer @this)
    {
        string? key = @this.ReadStringPooled();
        
        return key is null
            ? throw new ArgumentException("A key cannot be null")
            : key;
    }
}
