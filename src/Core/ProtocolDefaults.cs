// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core;

/// <summary>
/// Contains the default values for the Mimoria protocol.
/// </summary>
public static class ProtocolDefaults
{
    /// <summary>
    /// The length of the prefix used to indicate the length of a message.
    /// </summary>
    public const int LengthPrefixLength = 4;

    /// <summary>
    /// The maximum allowed length for a password.
    /// </summary>
    public const int MaxPasswordLength = 4096;

    /// <summary>
    /// The maximum string size in bytes.
    /// </summary>
    public const uint MaxStringSizeBytes = 128_000_000;

    /// <summary>
    /// The maximum allowed length for a byte array.
    /// </summary>
    public const int MaxByteArrayLength = 128_000_000;

    /// <summary>
    /// The maximum allowed count for a map.
    /// </summary>
    public const int MaxMapCount = 1_000_000;

    /// <summary>
    /// The maximum allowed count for a list.
    /// </summary>
    public const int MaxListCount = 1_000_000;
}
