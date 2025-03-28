// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core.Buffer;

/// <summary>
/// Represents a buffer for reading and writing bytes.
/// </summary>
public interface IByteBuffer : IDisposable, IEquatable<IByteBuffer>
{
    /// <summary>
    /// Gets the size of the buffer.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets the bytes in the buffer.
    /// </summary>
    byte[] Bytes { get; }

    /// <summary>
    /// Gets or sets the write index in the buffer.
    /// </summary>
    int WriteIndex { get; set; }

    /// <summary>
    /// Writes a boolean value to the buffer.
    /// </summary>
    /// <param name="value">The boolean value to write.</param>
    void WriteBool(bool value);

    /// <summary>
    /// Writes a byte value to the buffer.
    /// </summary>
    /// <param name="value">The byte value to write.</param>
    void WriteByte(byte value);

    /// <summary>
    /// Writes an unsigned integer value to the buffer.
    /// </summary>
    /// <param name="value">The unsigned integer value to write.</param>
    void WriteUInt(uint value);

    /// <summary>
    /// Writes an integer value to the buffer.
    /// </summary>
    /// <param name="value">The integer value to write.</param>
    void WriteInt(int value);

    /// <summary>
    /// Writes a variable-length unsigned integer value to the buffer.
    /// </summary>
    /// <param name="value">The variable-length unsigned integer value to write.</param>
    void WriteVarUInt(uint value);

    /// <summary>
    /// Writes a long value to the buffer.
    /// </summary>
    /// <param name="value">The long value to write.</param>
    void WriteLong(long value);

    /// <summary>
    /// Writes an unsigned long value to the buffer.
    /// </summary>
    /// <param name="value">The unsigned long value to write.</param>
    void WriteULong(ulong value);

    /// <summary>
    /// Writes a float value to the buffer.
    /// </summary>
    /// <param name="value">The float value to write.</param>
    unsafe void WriteFloat(float value);

    /// <summary>
    /// Writes a double value to the buffer.
    /// </summary>
    /// <param name="value">The double value to write.</param>
    void WriteDouble(double value);

    /// <summary>
    /// Writes a GUID to the buffer.
    /// </summary>
    /// <param name="guid">The GUID to write.</param>
    void WriteGuid(in Guid guid);

    /// <summary>
    /// Writes a UTC DateTime value to the buffer.
    /// </summary>
    /// <param name="dateTime">The DateTime value to write.</param>
    void WriteDateTimeUtc(in DateTime dateTime);

    /// <summary>
    /// Writes a DateOnly value to the buffer.
    /// </summary>
    /// <param name="dateOnly">The DateOnly value to write.</param>
    void WriteDateOnly(in DateOnly dateOnly);

    /// <summary>
    /// Writes a TimeOnly value to the buffer.
    /// </summary>
    /// <param name="timeOnly">The TimeOnly value to write.</param>
    void WriteTimeOnly(in TimeOnly timeOnly);

    /// <summary>
    /// Writes a nullable string value to the buffer.
    /// </summary>
    /// <param name="value">The string value to write.</param>
    void WriteString(string? value);

    /// <summary>
    /// Writes a span of bytes to the buffer.
    /// </summary>
    /// <param name="source">The span of bytes to write.</param>
    void WriteBytes(ReadOnlySpan<byte> source);

    /// <summary>
    /// Writes a MimoriaValue to the buffer.
    /// </summary>
    /// <param name="value">The MimoriaValue to write.</param>
    void WriteValue(MimoriaValue value);

    /// <summary>
    /// Reads a boolean value from the buffer.
    /// </summary>
    /// <returns>The boolean value read from the buffer.</returns>
    bool ReadBool();

    /// <summary>
    /// Reads a byte value from the buffer.
    /// </summary>
    /// <returns>The byte value read from the buffer.</returns>
    byte ReadByte();

    /// <summary>
    /// Reads an unsigned integer value from the buffer.
    /// </summary>
    /// <returns>The unsigned integer value read from the buffer.</returns>
    uint ReadUInt();

    /// <summary>
    /// Reads an integer value from the buffer.
    /// </summary>
    /// <returns>The integer value read from the buffer.</returns>
    int ReadInt();

    /// <summary>
    /// Reads a variable-length unsigned integer value from the buffer.
    /// </summary>
    /// <returns>The variable-length unsigned integer value read from the buffer.</returns>
    uint ReadVarUInt();

    /// <summary>
    /// Reads a long value from the buffer.
    /// </summary>
    /// <returns>The long value read from the buffer.</returns>
    long ReadLong();

    /// <summary>
    /// Reads an unsigned long value from the buffer.
    /// </summary>
    /// <returns>The unsigned long value read from the buffer.</returns>
    ulong ReadULong();

    /// <summary>
    /// Reads a float value from the buffer.
    /// </summary>
    /// <returns>The float value read from the buffer.</returns>
    unsafe float ReadFloat();

    /// <summary>
    /// Reads a double value from the buffer.
    /// </summary>
    /// <returns>The double value read from the buffer.</returns>
    double ReadDouble();

    /// <summary>
    /// Reads a GUID from the buffer.
    /// </summary>
    /// <returns>The GUID read from the buffer.</returns>
    Guid ReadGuid();

    /// <summary>
    /// Reads a UTC DateTime value from the buffer.
    /// </summary>
    /// <returns>The DateTime value read from the buffer.</returns>
    DateTime ReadDateTimeUtc();

    /// <summary>
    /// Reads a DateOnly value from the buffer.
    /// </summary>
    /// <returns>The DateOnly value read from the buffer.</returns>
    DateOnly ReadDateOnly();

    /// <summary>
    /// Reads a TimeOnly value from the buffer.
    /// </summary>
    /// <returns>The TimeOnly value read from the buffer.</returns>
    TimeOnly ReadTimeOnly();

    /// <summary>
    /// Reads a nullable string value from the buffer.
    /// </summary>
    /// <returns>The string value read from the buffer.</returns>
    string? ReadString();

    /// <summary>
    /// Reads a span of bytes from the buffer.
    /// </summary>
    /// <param name="destination">The span of bytes to read into.</param>
    void ReadBytes(Span<byte> destination);

    /// <summary>
    /// Reads a MimoriaValue from the buffer.
    /// </summary>
    /// <returns>The MimoriaValue read from the buffer.</returns>
    MimoriaValue ReadValue();

    /// <summary>
    /// Retains the buffer, preventing it from being disposed.
    /// </summary>
    void Retain();

    /// <summary>
    /// Marks the end of the packet in the buffer.
    /// </summary>
    void EndPacket();

    /// <summary>
    /// Clears the buffer.
    /// </summary>
    void Clear();
}
