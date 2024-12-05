// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Core.Buffer;

public interface IByteBuffer : IDisposable, IEquatable<IByteBuffer>
{
    int Size { get; }
    byte[] Bytes { get; }

    int WriteIndex { get; set; }

    void WriteBool(bool value);
    void WriteByte(byte value);
    void WriteUInt(uint value);
    void WriteInt(int value);
    void WriteVarUInt(uint value);
    void WriteLong(long value);
    void WriteULong(ulong value);
    unsafe void WriteFloat(float value);
    void WriteDouble(double value);
    void WriteGuid(in Guid guid);
    void WriteDateTimeUtc(in DateTime dateTime);
    void WriteDateOnly(in DateOnly dateOnly);
    void WriteTimeOnly(in TimeOnly timeOnly);
    void WriteString(string? value);
    void WriteBytes(ReadOnlySpan<byte> source);
    void WriteValue(MimoriaValue value);

    bool ReadBool();
    byte ReadByte();
    uint ReadUInt();
    int ReadInt();
    uint ReadVarUInt();
    long ReadLong();
    ulong ReadULong();
    unsafe float ReadFloat();
    double ReadDouble();
    Guid ReadGuid();
    DateTime ReadDateTimeUtc();
    DateOnly ReadDateOnly();
    TimeOnly ReadTimeOnly();
    string? ReadString();
    void ReadBytes(Span<byte> destination);
    MimoriaValue ReadValue();

    void Retain();

    void EndPacket();

    void Clear();
}
