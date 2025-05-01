// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core;

/// <summary>
/// Represents the comparison type for key deletion.
/// </summary>
public enum Comparison : byte
{
    /// <summary>
    /// Delete keys that start with the specified pattern (case-sensitive).
    /// </summary>
    StartsWith = 0,
    /// <summary>
    /// Delete keys that end with the specified pattern (case-sensitive).
    /// </summary>
    EndsWith = 1,
    /// <summary>
    /// Delete keys that contain the specified pattern (case-sensitive).
    /// </summary>
    Contains = 2
}
