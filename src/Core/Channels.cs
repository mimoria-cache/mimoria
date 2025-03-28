// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Core;

/// <summary>
/// Contains the well-known channels used for pub/sub.
/// </summary>
public static class Channels
{
    /// <summary>
    /// Published to if a key expired. Payload is the key (string) which expired.
    /// </summary>
    public const string KeyExpiration = "__expiration";

    /// <summary>
    /// Published to if a key is deleted. Payload is the key (string) which was deleted.
    /// </summary>
    public const string KeyDeletion = "__deletion";

    /// <summary>
    /// Published to if the primary server changed. Payload is the id (int) of the new primary server.
    /// </summary>
    public const string PrimaryChanged = "__primary:changed";

    /// <summary>
    /// Published to if a list item was added. Payload is the value (string) added to the list.
    /// </summary>
    public const string ListAddedTemplate = "__key:{0}:list:added";

    // TODO: Cache results, it could be used quite often on the server?#
    /// <summary>
    /// Returns the channel for when a list item was added to the list at the specified key.
    /// </summary>
    /// <param name="key">The key of the list.</param>
    /// <returns>The channel for when a list item was added to the list at the specified key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ForListAdded(string key)
        => string.Format(ListAddedTemplate, key);
}
