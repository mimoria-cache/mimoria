// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core.Buffer;

public interface IByteBuffer : IDisposable, IEquatable<IByteBuffer>
{
    int Size { get; }
    byte[] Bytes { get; }

    int WriteIndex { get; set; }

    void WriteByte(byte value);
    void WriteUInt(uint value);
    void WriteVarUInt(uint value);
    void WriteULong(ulong value);
    unsafe void WriteFloat(float value);
    void WriteGuid(in Guid guid);
    void WriteString(string? value);
    void WriteBytes(ReadOnlySpan<byte> source);

    byte ReadByte();
    uint ReadUInt();
    uint ReadVarUInt();
    ulong ReadULong();
    unsafe float ReadFloat();
    Guid ReadGuid();
    string? ReadString();
    void ReadBytes(Span<byte> destination);

    void Retain();

    void EndPacket();

    void Clear();
}
