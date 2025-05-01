// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using CommunityToolkit.HighPerformance.Buffers;

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Core;

/// <summary>
/// Contains the well-known channels used for pub/sub.
/// </summary>
public static class Channels
{
    /// <summary>
    /// The length of the placeholder in the template strings.
    /// </summary>
    public const int PlaceholderLength = 3;

    /// <summary>
    /// The maximum length of the buffer used for stack allocation.
    /// </summary>
    public const int MaxStackAllocBufferLength = 256;

    /// <summary>
    /// Published to if a key expired. Payload is the key (string) which expired.
    /// </summary>
    public const string KeyExpiration = "__expiration";

    /// <summary>
    /// Published to if a key is deleted. Payload is the key (string) which was deleted.
    /// </summary>
    public const string KeyDeletion = "__deletion";

    /// <summary>
    /// Published to if the cache was cleared. Payload is a null <see cref="MimoriaValue"/>.
    /// </summary>
    public const string Clear = "__clear";

    /// <summary>
    /// Published to if the primary server changed. Payload is the id (int) of the new primary server.
    /// </summary>
    public const string PrimaryChanged = "__primary:changed";

    /// <summary>
    /// Published to if a list item was added. Payload is the value (string) added to the list.
    /// </summary>
    public const string ListAddedTemplate = "__key:{0}:list:added";

    /// <summary>
    /// Returns the channel for when a list item was added to the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list.</param>
    /// <returns>The channel for when a list item was added to the list at the specified key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ForListAdded(string key)
        => GetFullChannel(ListAddedTemplate, key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetFullChannel(string channelTemplate, string key)
    {
        int totalLength = channelTemplate.Length - PlaceholderLength + key.Length;
        
        char[]? rentedBuffer = null;

        Span<char> buffer = totalLength <= MaxStackAllocBufferLength
            ? stackalloc char[totalLength]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(totalLength));

        try
        {
            int templatePlaceholderIndex = channelTemplate.AsSpan().IndexOf("{0}");

            channelTemplate.AsSpan(0, templatePlaceholderIndex).CopyTo(buffer);

            key.AsSpan().CopyTo(buffer[templatePlaceholderIndex..]);

            channelTemplate.AsSpan(templatePlaceholderIndex + PlaceholderLength).CopyTo(buffer[(templatePlaceholderIndex + key.Length)..]);

            return StringPool.Shared.GetOrAdd(buffer[..totalLength]);
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }
}
