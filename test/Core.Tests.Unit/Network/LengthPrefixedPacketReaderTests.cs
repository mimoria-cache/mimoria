// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;

using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core.Network;

namespace Varelen.Mimoria.Core.Tests.Unit.Network;

public class LengthPrefixedPacketReaderTests : IDisposable
{
    private readonly LengthPrefixedPacketReader sut;

    public LengthPrefixedPacketReaderTests()
        => this.sut = new LengthPrefixedPacketReader(ProtocolDefaults.LengthPrefixLength);

    [Fact]
    public void TryRead_When_EmptyBytes_Then_ReturnsNoPackets()
    {
        // Arrange
        var bytes = new ReadOnlyMemory<byte>([]);
        
        // Act
        var packets = this.sut.TryRead(bytes, bytes.Length).ToList();

        // Assert
        Assert.Empty(packets);

        // Cleanup
        DisposePackets(packets);
    }

    [Fact]
    public void TryRead_When_IncompletePacket_Then_ReturnsNoPackets()
    {
        // Arrange
        var bytes = new ReadOnlyMemory<byte>([0, 0, 0, 5, 1, 2]);

        // Act
        var packets = this.sut.TryRead(bytes, bytes.Length).ToList();

        // Assert
        Assert.Empty(packets);

        // Cleanup
        DisposePackets(packets);
    }

    [Fact]
    public void TryRead_When_IncompletePacketWithSecondRead_Then_ReturnsPackets()
    {
        // Arrange
        var bytes = new ReadOnlyMemory<byte>([0, 0, 0, 5, 1, 2]);

        // Act
        var packets = this.sut.TryRead(bytes, bytes.Length).ToList();

        // Assert 1
        Assert.Empty(packets);

        // Act 2
        packets = this.sut.TryRead(new byte[] { 3, 4, 5 }, 3).ToList();

        // Assert 2
        Assert.Single(packets);

        // Cleanup
        DisposePackets(packets);
    }

    [Fact]
    public void TryRead_When_CompletePacket_Then_ReturnsPacket()
    {
        // Arrange
        var bytes = new ReadOnlyMemory<byte>([0, 0, 0, 2, 1, 2]);

        // Act
        var packets = this.sut.TryRead(bytes, bytes.Length).ToList();

        // Assert
        Assert.Single(packets);
        Assert.Equal(new byte[] { 1, 2 }, packets[0].Bytes[0..packets[0].Size]);

        // Cleanup
        DisposePackets(packets);
    }

    [Fact]
    public void TryRead_When_MultiplePackets_Then_ReturnsAllPackets()
    {
        // Arrange
        var bytes = new ReadOnlyMemory<byte>([0, 0, 0, 2, 1, 2, 0, 0, 0, 2, 3, 4]);

        // Act
        var packets = this.sut.TryRead(bytes, bytes.Length).ToList();

        // Assert
        Assert.Equal(2, packets.Count);
        Assert.Equal(new byte[] { 1, 2 }, packets[0].Bytes[0..packets[0].Size]);
        Assert.Equal(new byte[] { 3, 4 }, packets[1].Bytes[0..packets[1].Size]);

        // Cleanup
        DisposePackets(packets);
    }

    [Fact]
    public void TryRead_When_LargeCompletePacket_Then_ReturnsPacket()
    {
        // Arrange
        var aBunchOfBytes = new byte[ProtocolDefaults.MaxByteArrayLength];
        Random.Shared.NextBytes(aBunchOfBytes);

        var bytes = new byte[ProtocolDefaults.LengthPrefixLength + aBunchOfBytes.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(), aBunchOfBytes.Length);
        aBunchOfBytes.CopyTo(bytes.AsSpan(ProtocolDefaults.LengthPrefixLength));

        // Act
        var packets = this.sut.TryRead(bytes, bytes.Length).ToList();

        // Assert
        Assert.Single(packets);
        Assert.Equal(aBunchOfBytes, packets[0].Bytes[0..packets[0].Size]);

        // Cleanup
        DisposePackets(packets);
    }

    [Fact]
    public void Reset_When_SplitPacketAndResetInBetween_Then_NoPacketIsReturned()
    {
        // Arrange
        var bytes = new ReadOnlyMemory<byte>([0, 0, 0, 5, 1, 2]);

        // Act
        var packets = this.sut.TryRead(bytes, bytes.Length).ToList();

        // Assert 1
        Assert.Empty(packets);

        // Act 2
        this.sut.Reset();

        packets = this.sut.TryRead(new byte[] { 3, 4, 5 }, 3).ToList();

        // Assert 2
        Assert.Empty(packets);

        // Cleanup
        DisposePackets(packets);
    }

    private static void DisposePackets(List<IByteBuffer> packets)
    {
        foreach (var packet in packets)
        {
            packet.Dispose();
        }
    }

    public void Dispose()
    {
        this.sut.Dispose();
        GC.SuppressFinalize(this);
    }
}
