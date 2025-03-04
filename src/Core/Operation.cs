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
    SetBytes = 13,
    /// <summary>
    /// Sets a counter value for a key.
    /// </summary>
    SetCounter = 14,
    /// <summary>
    /// Increments a counter by a given value for a key.
    /// </summary>
    IncrementCounter = 15,
    /// <summary>
    /// Executes multiple operations in one request.
    /// </summary>
    Bulk = 16,
    /// <summary>
    /// Gets a map value of a map under the given key.
    /// </summary>
    GetMapValue = 17,
    /// <summary>
    /// Sets a map value of a map under the given key.
    /// </summary>
    SetMapValue = 18,
    /// <summary>
    /// Gets the entire map under the given key.
    /// </summary>
    GetMap = 19,
    /// <summary>
    /// Sets the entire map under the given key.
    /// </summary>
    SetMap = 20,
    /// <summary>
    /// Subscribes to a channel.
    /// </summary>
    Subscribe = 21,
    /// <summary>
    /// Unsubscribes from a channel.
    /// </summary>
    Unsubscribe = 22,
    /// <summary>
    /// Published a payload to a channel.
    /// </summary>
    Publish = 23,

    // Cluster
    ClusterLogin = 249,
    ElectionMessage = 250,
    AliveMessage = 251,
    VictoryMessage = 252,
    HeartbeatMessage = 253,
    Batch = 254,
    Sync = 255
}
