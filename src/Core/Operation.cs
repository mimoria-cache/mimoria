// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core;

public enum Operation : byte
{
    /// <summary>
    /// Used to authenticate with a password before further operations are accepted.
    /// </summary>
    Login = 0,
    /// <summary>
    /// Gets a string value based on a key.
    /// </summary>
    GetString = 1,
    /// <summary>
    /// Sets a string value for a key.
    /// </summary>
    SetString = 2,
    /// <summary>
    /// Sets a binary serialized object value for a key.
    /// </summary>
    SetObjectBinary = 3,
    /// <summary>
    /// Gets a binary serialized object value based on a key.
    /// </summary>
    GetObjectBinary = 4,
    /// <summary>
    /// Gets the list stored at a key.
    /// </summary>
    GetList = 5,
    /// <summary>
    /// Adds an element to the list stored at a key.
    /// </summary>
    AddList = 6,
    /// <summary>
    /// Removes an element from a list stored at a key.
    /// </summary>
    RemoveList = 7,
    /// <summary>
    /// Gets a byte flag indicating if a value in a list exists.
    /// </summary>
    ContainsList = 8,
    /// <summary>
    /// Gets a byte flag indicating if a key exists.
    /// </summary>
    Exists = 9,
    /// <summary>
    /// Deletes the value stored at a key.
    /// </summary>
    Delete = 10,
    /// <summary>
    /// Gets the server stats which contains information about uptime, connections and cache details (size, hits, misses, hit ratio).
    /// </summary>
    GetStats = 11,
    /// <summary>
    /// Gets a byte array based on a key.
    /// </summary>
    GetBytes = 12,
    /// <summary>
    /// Sets a byte array for a key.
    /// </summary>
    SetBytes = 13
}
