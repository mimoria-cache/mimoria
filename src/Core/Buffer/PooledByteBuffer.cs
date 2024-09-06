// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.ObjectPool;

using System.Buffers;
#if DEBUG
using System.Diagnostics;
#endif
using System.Runtime.CompilerServices;
using System.Text;

namespace Varelen.Mimoria.Core.Buffer;

public sealed class PooledByteBuffer : IByteBuffer
{
    public const int DefaultBufferSize = 512;
    public const byte BufferGrowFactor = 2;
    public const uint MaxStringSizeBytes = 128_000_000;
    public const uint MaxByteArraySizeBytes = 128_000_000;
    private const byte GuidByteSize = 16;

    private static readonly DefaultObjectPool<PooledByteBuffer> Pool = new(new PooledByteBufferPooledObjectPolicy());

    private byte[] buffer;
    private int readIndex;
    private int writeIndex;

    private uint referenceCount;

    public int Size => writeIndex;
    public byte[] Bytes => buffer;
    public int WriteIndex
    {
        get => this.writeIndex;
        set => this.writeIndex = value;
    }
    public uint ReferenceCount => this.referenceCount;

#if DEBUG
    private readonly string allocatedStackTrace;
#endif

    public PooledByteBuffer()
    {
        this.referenceCount = 1;
        this.buffer = new byte[DefaultBufferSize];
#if DEBUG
        this.allocatedStackTrace = new StackTrace().ToString();
#endif
    }

    public PooledByteBuffer(Operation operation)
        : this()
    {
        this.WriteUInt(0);
        this.WriteByte((byte)operation);
    }

    public PooledByteBuffer(Operation operation, uint requestId)
        : this()
    {
        this.WriteUInt(0);
        this.WriteByte((byte)operation);
        this.WriteUInt(requestId);
    }

    public PooledByteBuffer(Operation operation, uint requestId, StatusCode statusCode)
        : this()
    {
        this.WriteUInt(0);
        this.WriteByte((byte)operation);
        this.WriteUInt(requestId);
        this.WriteByte((byte)statusCode);
    }

#if DEBUG
    ~PooledByteBuffer()
    {
        if (this.referenceCount > 0)
        {
            throw new InvalidOperationException($"Destructor/Finalizer of PooledByteBuffer (allocated here '{this.allocatedStackTrace}') called but still has a non zero reference count of '{this.referenceCount}' :(");
        }
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureBufferSize(int sizeToAdd)
    {
        int newSize = this.writeIndex + sizeToAdd;
        if (newSize <= this.buffer.Length)
        {
            return;
        }

        int newRealSize = this.buffer.Length * BufferGrowFactor;
        while (newSize > newRealSize)
        {
            newRealSize *= BufferGrowFactor;
        }

        byte[] bytes = ArrayPool<byte>.Shared.Rent(newRealSize);
        this.buffer.AsSpan(0, this.writeIndex).CopyTo(bytes);
        ArrayPool<byte>.Shared.Return(this.buffer);

        this.buffer = bytes;
    }

    public void WriteByte(byte value)
    {
        this.EnsureBufferSize(1);

        this.buffer[this.writeIndex++] = value;
    }

    public void WriteUInt(uint value)
    {
        this.EnsureBufferSize(4);

        this.buffer[this.writeIndex++] = (byte)value;
        this.buffer[this.writeIndex++] = (byte)(value >> 8);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x10);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x18);
    }

    // Based on the source code of the BinaryWriter.Write7BitEncodedInt() method from the .NET Foundation.
    public void WriteVarUInt(uint value)
    {
        // Instead of using this.WriteByte() multiple times below
        // we directly write to the buffer and ensure the worst case varuint length only once here
        this.EnsureBufferSize(5);

        while (value > 0x7F)
        {
            this.buffer[this.writeIndex++] = (byte)(value | 0x80);
            value >>= 7;
        }
        this.buffer[this.writeIndex++] = (byte)value;
    }

    public void WriteULong(ulong value)
    {
        this.EnsureBufferSize(8);

        this.buffer[this.writeIndex++] = (byte)value;
        this.buffer[this.writeIndex++] = (byte)(value >> 8);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x10);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x18);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x20);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x28);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x30);
        this.buffer[this.writeIndex++] = (byte)(value >> 0x38);
    }

    public unsafe void WriteFloat(float value)
    {
        this.EnsureBufferSize(4);

        uint tmp = *(uint*)&value;

        this.buffer[this.writeIndex++] = (byte)tmp;
        this.buffer[this.writeIndex++] = (byte)(tmp >> 8);
        this.buffer[this.writeIndex++] = (byte)(tmp >> 0x10);
        this.buffer[this.writeIndex++] = (byte)(tmp >> 0x18);
    }

    public void WriteGuid(in Guid guid)
    {
        this.EnsureBufferSize(GuidByteSize);

        guid.TryWriteBytes(this.buffer.AsSpan(this.writeIndex, GuidByteSize));

        this.writeIndex += GuidByteSize;
    }

    public void WriteString(string? value)
    {
        if (value is null)
        {
            this.WriteVarUInt(0);
            return;
        }

        byte[] data = ArrayPool<byte>.Shared.Rent(value.Length);

        try
        {
            int written = Encoding.UTF8.GetBytes(value, data);
            if (written > MaxStringSizeBytes)
            {
                throw new ArgumentException($"Written string value length {data.Length} exceeded max allowed length {MaxStringSizeBytes}");
            }

            this.WriteVarUInt((uint)written);
            this.WriteBytes(data.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    public void WriteBytes(ReadOnlySpan<byte> source)
    {
        this.EnsureBufferSize(source.Length);

        // TODO: Limit bytes size to something reasonable (for example 256MB or similar)
        Span<byte> destination = this.buffer.AsSpan(this.writeIndex, source.Length);
        source.CopyTo(destination);

        this.writeIndex += source.Length;
    }

    public byte ReadByte()
        => this.buffer[this.readIndex++];

    public uint ReadUInt()
    {
        uint value = (uint)(this.buffer[this.readIndex++] |
            (this.buffer[this.readIndex++] << 8) |
            (this.buffer[this.readIndex++] << 0x10) |
            (this.buffer[this.readIndex++] << 0x18));

        return value;
    }

    // Based on the source code of the BinaryReader.Read7BitEncodedInt() method from the .NET Foundation.
    public uint ReadVarUInt()
    {
        uint value = 0;
        byte shift = 0;
        byte currentByte;
        do
        {
            currentByte = this.buffer[this.readIndex++];
            value |= (uint)(currentByte & 0x7F) << shift;
            shift += 7;
        } while ((currentByte & 0x80) != 0);
        return value;
    }

    public ulong ReadULong()
    {
        ulong value = (ulong)(this.buffer[this.readIndex++] |
            (this.buffer[this.readIndex++] << 8) |
            (this.buffer[this.readIndex++] << 0x10) |
            (this.buffer[this.readIndex++] << 0x18) |
            (this.buffer[this.readIndex++] << 0x20) |
            (this.buffer[this.readIndex++] << 0x28) |
            (this.buffer[this.readIndex++] << 0x30) |
            (this.buffer[this.readIndex++] << 0x38));

        return value;
    }

    public unsafe float ReadFloat()
    {
        uint value = (uint)(this.buffer[this.readIndex++] |
            (this.buffer[this.readIndex++] << 8) |
            (this.buffer[this.readIndex++] << 0x10) |
            (this.buffer[this.readIndex++] << 0x18));

        return *(float*)&value;
    }

    public Guid ReadGuid()
    {
        var guid = new Guid(this.buffer.AsSpan(this.readIndex, GuidByteSize));
        this.readIndex += GuidByteSize;
        return guid;
    }

    public string? ReadString()
    {
        uint length = this.ReadVarUInt();
        if (length == 0)
        {
            return null;
        }

        if (length > MaxStringSizeBytes)
        {
            throw new ArgumentException($"Read string value length {length} exceeded max allowed length {MaxStringSizeBytes}");
        }

        byte[] bytes = ArrayPool<byte>.Shared.Rent((int)length);

        try
        {
            Span<byte> bytesSpan = bytes.AsSpan(0, (int)length);
            this.ReadBytes(bytesSpan);

            return Encoding.UTF8.GetString(bytesSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    public void ReadBytes(Span<byte> destination)
    {
        // TODO: Limit bytes size to something reasonable (for example 256MB or similar)
        Span<byte> src = this.buffer.AsSpan(this.readIndex, destination.Length);
        src.CopyTo(destination);

        this.readIndex += destination.Length;
    }

    public void Retain()
        => Interlocked.Increment(ref this.referenceCount);

    public void EndPacket()
    {
        int originalWriteIndex = this.writeIndex;

        this.readIndex = 0;
        this.writeIndex = 0;

        uint packetSize = (uint)(originalWriteIndex - sizeof(uint));
        this.WriteUInt(packetSize);

        this.writeIndex = originalWriteIndex;
    }

    public void Clear()
    {
        this.referenceCount = 1;
        this.writeIndex = 0;
        this.readIndex = 0;
    }

    public void Dispose()
    {
        if (Interlocked.Decrement(ref this.referenceCount) > 0)
        {
            return;
        }

        Pool.Return(this);
        GC.SuppressFinalize(this);
    }

    public bool Equals(IByteBuffer? other)
        => other is not null
            && this.writeIndex == other.WriteIndex
            && this.Size == other.Size
            && this.buffer[0..this.Size].SequenceEqual(other.Bytes[0..other.Size]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IByteBuffer FromPool()
        => Pool.Get();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IByteBuffer FromPool(Operation operation)
    {
        PooledByteBuffer pooledByteBuffer = Pool.Get();
        pooledByteBuffer.WriteUInt(0);
        pooledByteBuffer.WriteByte((byte)operation);
        return pooledByteBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IByteBuffer FromPool(Operation operation, uint requestId)
    {
        PooledByteBuffer pooledByteBuffer = Pool.Get();
        pooledByteBuffer.WriteUInt(0);
        pooledByteBuffer.WriteByte((byte)operation);
        pooledByteBuffer.WriteUInt(requestId);
        return pooledByteBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IByteBuffer FromPool(Operation operation, uint requestId, StatusCode statusCode)
    {
        PooledByteBuffer pooledByteBuffer = Pool.Get();
        pooledByteBuffer.WriteUInt(0);
        pooledByteBuffer.WriteByte((byte)operation);
        pooledByteBuffer.WriteUInt(requestId);
        pooledByteBuffer.WriteByte((byte)statusCode);
        return pooledByteBuffer;
    }
}
