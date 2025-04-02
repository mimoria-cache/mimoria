// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client;

/// <summary>
/// The interface for binary serializable objects.
/// </summary>
public interface IBinarySerializable
{
    /// <summary>
    /// Serializes the object to a byte buffer.
    /// </summary>
    /// <param name="byteBuffer">The byte buffer to write to.</param>
    void Serialize(IByteBuffer byteBuffer);

    /// <summary>
    /// Deserializes the object from a byte buffer.
    /// </summary>
    /// <param name="byteBuffer">The byte buffer to read from.</param>
    void Deserialize(IByteBuffer byteBuffer);
}
