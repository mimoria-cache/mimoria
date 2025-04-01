// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Core.Network;

/// <summary>
/// Reads length-prefixed packets from bytes with support for partial packets and multiple packets in one byte array.
/// </summary>
public sealed class LengthPrefixedPacketReader : IDisposable
{
    private readonly int prefixLength;
    private readonly PooledByteBuffer buffer;

    private int expectedPacketLength;

    /// <summary>
    /// Create a new length-prefixed packet reader.
    /// </summary>
    public LengthPrefixedPacketReader(int prefixLength)
    {
        this.prefixLength = prefixLength;
        this.buffer = new PooledByteBuffer();
    }

    /// <summary>
    /// Try to read packets from the bytes.
    /// </summary>
    /// <param name="bytes">The bytes to read from.</param>
    /// <param name="received">The number of bytes received.</param>
    /// <returns>The packets read if any.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerable<IByteBuffer> TryRead(ReadOnlyMemory<byte> bytes, int received)
    {
        this.buffer.WriteBytes(bytes.Span[..received]);

        while (this.buffer.Size >= this.prefixLength)
        {
            this.expectedPacketLength = BinaryPrimitives.ReadInt32BigEndian(this.buffer.Bytes.AsSpan(0, this.prefixLength));
            if (this.buffer.Size < this.expectedPacketLength + this.prefixLength)
            {
                yield break;
            }

            var byteBuffer = PooledByteBuffer.FromPool();
            byteBuffer.WriteBytes(this.buffer.Bytes.AsSpan(this.prefixLength, this.expectedPacketLength));

            int remainingBytes = this.buffer.Size - (this.prefixLength + this.expectedPacketLength);

            Debug.Assert(remainingBytes >= 0, "Remaining bytes should not be negative");

            if (remainingBytes == 0)
            {
                this.buffer.Clear();

                yield return byteBuffer;
                yield break;
            }
            
            this.buffer.Bytes.AsSpan(this.prefixLength + this.expectedPacketLength, remainingBytes).CopyTo(this.buffer.Bytes);
            this.buffer.WriteIndex = remainingBytes;

            yield return byteBuffer;
        }
    }

    /// <inheritdoc />
    public void Dispose()
        => this.buffer.Dispose();

    /// <summary>
    /// Resets the reader.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        this.buffer.Clear();
        this.expectedPacketLength = 0;
    }
}
