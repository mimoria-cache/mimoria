// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core;

public static class Channels
{
    /// <summary>
    /// Published to if a key expired. Payload is the key (string) which expired.
    /// </summary>
    public const string KeyExpiration = "__expiration";

    /// <summary>
    /// Published to if the primary server changed. Payload is the id (int) of the new primary server.
    /// </summary>
    public const string PrimaryChanged = "__primary:changed";
}
