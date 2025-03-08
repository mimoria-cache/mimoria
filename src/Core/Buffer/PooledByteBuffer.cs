// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.ObjectPool;

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Varelen.Mimoria.Core.Buffer;

public sealed class PooledByteBuffer : IByteBuffer
{
    public const int DefaultBufferSize = 65_536;
    public const byte BufferGrowFactor = 2;
    public const uint MaxStringSizeBytes = 128_000_000;
    public const uint MaxByteArraySizeBytes = 128_000_000;
    private const byte GuidByteSize = 16;
    private const byte MaxUtf8CharLength = 4;

    private static readonly DefaultObjectPool<PooledByteBuffer> Pool = new(new PooledByteBufferPooledObjectPolicy());

    private byte[] buffer;
    private int readIndex;
    private int writeIndex;

    private uint referenceCount;

    public int Size => this.writeIndex;
    public byte[] Bytes => this.buffer;
    public int WriteIndex
    {
        get => this.writeIndex;
        set => this.writeIndex = value;
    }
    public uint ReferenceCount => this.referenceCount;

#if DEBUG
    private readonly string allocatedStackTrace;
#endif

    public PooledByteBuffer(int bufferSize = DefaultBufferSize)
    {
        this.readIndex = 0;
        this.writeIndex = 0;
        this.referenceCount = 1;
        this.buffer = new byte[bufferSize];
#if DEBUG
        this.allocatedStackTrace = new StackTrace().ToString();
#endif
    }

    public PooledByteBuffer(Operation operation)
        : this()
    {
        this.WriteLengthPlaceholder();
        this.WriteByte((byte)operation);
    }

    public PooledByteBuffer(Operation operation, uint requestId)
        : this()
    {
        this.WriteLengthPlaceholder();
        this.WriteByte((byte)operation);
        this.WriteUInt(requestId);
    }

    public PooledByteBuffer(Operation operation, uint requestId, StatusCode statusCode)
        : this()
    {
        this.WriteLengthPlaceholder();
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
    private void WriteLengthPlaceholder()
    {
        this.WriteUInt(0);
    }

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

        var newBuffer = new byte[newRealSize];
        this.buffer.AsSpan(0, this.writeIndex).CopyTo(newBuffer.AsSpan());

        this.buffer = newBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfOutOfRange(uint size)
    {
        if (this.readIndex + size > this.Size)
        {
            throw new ArgumentException($"Tried to read at '{this.readIndex + size}' but size is only '{this.Size}'");
        }
    }

    public void WriteBool(bool value)
    {
        this.EnsureBufferSize(1);

        this.buffer[this.writeIndex++] = (byte)(value ? 1 : 0);
    }

    public void WriteByte(byte value)
    {
        this.EnsureBufferSize(1);

        this.buffer[this.writeIndex++] = value;
    }

    public void WriteUInt(uint value)
    {
        this.EnsureBufferSize(4);

        BinaryPrimitives.WriteUInt32BigEndian(this.buffer.AsSpan(this.writeIndex), value);
        this.writeIndex += 4;
    }

    public void WriteInt(int value)
    {
        this.EnsureBufferSize(4);

        BinaryPrimitives.WriteInt32BigEndian(this.buffer.AsSpan(this.writeIndex), value);
        this.writeIndex += 4;
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

    public void WriteLong(long value)
    {
        this.EnsureBufferSize(8);

        BinaryPrimitives.WriteInt64BigEndian(this.buffer.AsSpan(this.writeIndex), value);
        this.writeIndex += 8;
    }

    public void WriteULong(ulong value)
    {
        this.EnsureBufferSize(8);

        BinaryPrimitives.WriteUInt64BigEndian(this.buffer.AsSpan(this.writeIndex), value);
        this.writeIndex += 8;
    }

    public unsafe void WriteFloat(float value)
    {
        this.EnsureBufferSize(4);

        uint tmp = *(uint*)&value;

        BinaryPrimitives.WriteSingleBigEndian(this.buffer.AsSpan(this.writeIndex), value);
        this.writeIndex += 4;
    }

    public void WriteDouble(double value)
    {
        this.EnsureBufferSize(4);

        BinaryPrimitives.WriteDoubleBigEndian(this.buffer.AsSpan(this.writeIndex), value);
        this.writeIndex += 8;
    }

    public void WriteGuid(in Guid guid)
    {
        this.EnsureBufferSize(GuidByteSize);

        guid.TryWriteBytes(this.buffer.AsSpan(this.writeIndex, GuidByteSize));

        this.writeIndex += GuidByteSize;
    }

    public void WriteDateTimeUtc(in DateTime dateTime)
    {
        this.WriteLong(dateTime.ToUniversalTime().Ticks);
    }

    public void WriteDateOnly(in DateOnly dateOnly)
    {
        this.WriteInt(dateOnly.DayNumber);
    }

    public void WriteTimeOnly(in TimeOnly timeOnly)
    {
        this.WriteLong(timeOnly.Ticks);
    }

    public void WriteString(string? value)
    {
        if (value is null)
        {
            this.WriteVarUInt(0);
            return;
        }

        byte[] data = ArrayPool<byte>.Shared.Rent(value.Length * MaxUtf8CharLength);

        try
        {
            int written = Encoding.UTF8.GetBytes(value.AsSpan(), data.AsSpan());
            if (written > MaxStringSizeBytes)
            {
                throw new ArgumentException($"Written string value length '{written}' exceeded max allowed length '{MaxStringSizeBytes}'");
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

    public void WriteValue(MimoriaValue value)
    {
        this.WriteByte((byte)value.Type);
        switch (value.Type)
        {
            case MimoriaValue.ValueType.Null:
                this.WriteByte(0);
                break;
            case MimoriaValue.ValueType.Bytes:
                this.WriteVarUInt((uint)((byte[])value.Value!).Length);
                this.WriteBytes((byte[])value.Value!);
                break;
            case MimoriaValue.ValueType.String:
                this.WriteString((string)value.Value!);
                break;
            case MimoriaValue.ValueType.Int:
                this.WriteInt((int)value.Value!);
                break;
            case MimoriaValue.ValueType.Long:
                this.WriteLong((long)value.Value!);
                break;
            case MimoriaValue.ValueType.Double:
                this.WriteDouble((double)value.Value!);
                break;
            case MimoriaValue.ValueType.Bool:
                this.WriteBool((bool)value.Value!);
                break;
            default:
                break;
        }
    }

    public bool ReadBool()
    {
        this.ThrowIfOutOfRange(1);
        return this.buffer[this.readIndex++] == 1;
    }

    public byte ReadByte()
    {
        this.ThrowIfOutOfRange(1);
        return this.buffer[this.readIndex++];
    }

    public uint ReadUInt()
    {
        this.ThrowIfOutOfRange(4);

        uint value = BinaryPrimitives.ReadUInt32BigEndian(this.buffer.AsSpan(this.readIndex));
        this.readIndex += 4;
        return value;
    }

    public int ReadInt()
    {
        this.ThrowIfOutOfRange(4);

        int value = BinaryPrimitives.ReadInt32BigEndian(this.buffer.AsSpan(this.readIndex));
        this.readIndex += 4;
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
            currentByte = this.ReadByte();
            value |= (uint)(currentByte & 0x7F) << shift;
            shift += 7;
        } while ((currentByte & 0x80) != 0);
        return value;
    }

    public long ReadLong()
    {
        this.ThrowIfOutOfRange(8);
     
        long value = BinaryPrimitives.ReadInt64BigEndian(this.buffer.AsSpan(this.readIndex));
        this.readIndex += 8;
        return value;
    }

    public ulong ReadULong()
    {
        this.ThrowIfOutOfRange(8);

        ulong value = BinaryPrimitives.ReadUInt64BigEndian(this.buffer.AsSpan(this.readIndex));
        this.readIndex += 8;
        return value;
    }

    public unsafe float ReadFloat()
    {
        this.ThrowIfOutOfRange(4);

        float value = BinaryPrimitives.ReadSingleBigEndian(this.buffer.AsSpan(this.readIndex));
        this.readIndex += 4;
        return value;
    }

    public double ReadDouble()
    {
        this.ThrowIfOutOfRange(8);

        double value = BinaryPrimitives.ReadDoubleBigEndian(this.buffer.AsSpan(this.readIndex));
        this.readIndex += 8;
        return value;
    }

    public Guid ReadGuid()
    {
        this.ThrowIfOutOfRange(GuidByteSize);

        var guid = new Guid(this.buffer.AsSpan(this.readIndex, GuidByteSize));
        this.readIndex += GuidByteSize;
        return guid;
    }

    public DateTime ReadDateTimeUtc()
    {
        long ticks = this.ReadLong();
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    public DateOnly ReadDateOnly()
    {
        int dayNumber = this.ReadInt();
        return DateOnly.FromDayNumber(dayNumber);
    }

    public TimeOnly ReadTimeOnly()
    {
        long ticks = this.ReadLong();
        return new TimeOnly(ticks);
    }

    public string? ReadString()
    {
        uint length = this.ReadVarUInt();
        if (length == 0)
        {
            return null;
        }

        this.ThrowIfOutOfRange(length);

        if (length > MaxStringSizeBytes)
        {
            throw new ArgumentException($"Read string value length '{length}' exceeded max allowed length '{MaxStringSizeBytes}'");
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
        this.ThrowIfOutOfRange((uint)destination.Length);

        // TODO: Limit bytes size to something reasonable (for example 256MB or similar)
        Span<byte> src = this.buffer.AsSpan(this.readIndex, destination.Length);
        src.CopyTo(destination);

        this.readIndex += destination.Length;
    }

    public MimoriaValue ReadValue()
    {
        var type = (MimoriaValue.ValueType)this.ReadByte();
        switch (type)
        {
            case MimoriaValue.ValueType.Null:
                this.ReadByte();
                return MimoriaValue.Null;
            case MimoriaValue.ValueType.Bytes:
                uint length = this.ReadVarUInt();
                // TODO: Hm, pooling possible? Problem is it's returned to the user
                byte[] bytes = new byte[length];
                this.ReadBytes(bytes);
                return bytes;
            case MimoriaValue.ValueType.String:
                return this.ReadString();
            case MimoriaValue.ValueType.Int:
                return this.ReadInt();
            case MimoriaValue.ValueType.Long:
                return this.ReadLong();
            case MimoriaValue.ValueType.Double:
                return this.ReadDouble();
            case MimoriaValue.ValueType.Bool:
                return this.ReadBool();
            default:
                return MimoriaValue.Null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        this.writeIndex = 0;
        this.readIndex = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
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
    {
        PooledByteBuffer byteBuffer = Pool.Get();
        Debug.Assert(byteBuffer.Size == 0, "PooledByteBuffer.FromPool buffer is not zero size");
        return byteBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IByteBuffer FromPool(Operation operation)
    {
        PooledByteBuffer pooledByteBuffer = Pool.Get();
       
        Debug.Assert(pooledByteBuffer.Size == 0, "PooledByteBuffer.FromPool(operation) buffer is not zero size");

        pooledByteBuffer.WriteLengthPlaceholder();
        pooledByteBuffer.WriteByte((byte)operation);
        return pooledByteBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IByteBuffer FromPool(Operation operation, uint requestId)
    {
        PooledByteBuffer pooledByteBuffer = Pool.Get();
        
        Debug.Assert(pooledByteBuffer.Size == 0, "PooledByteBuffer.FromPool(operation, requestId) buffer is not zero size");

        pooledByteBuffer.WriteLengthPlaceholder();
        pooledByteBuffer.WriteByte((byte)operation);
        pooledByteBuffer.WriteUInt(requestId);
        return pooledByteBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IByteBuffer FromPool(Operation operation, uint requestId, StatusCode statusCode)
    {
        PooledByteBuffer pooledByteBuffer = Pool.Get();

        Debug.Assert(pooledByteBuffer.Size == 0, "PooledByteBuffer.FromPool(operation, requestId, statusCode) buffer is not zero size");

        pooledByteBuffer.WriteLengthPlaceholder();
        pooledByteBuffer.WriteByte((byte)operation);
        pooledByteBuffer.WriteUInt(requestId);
        pooledByteBuffer.WriteByte((byte)statusCode);
        return pooledByteBuffer;
    }
}
