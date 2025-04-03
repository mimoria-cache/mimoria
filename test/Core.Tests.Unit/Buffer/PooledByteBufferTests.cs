// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Core.Tests.Unit.Buffer;

public sealed class PooledByteBufferTests : IDisposable
{
    private readonly PooledByteBuffer sut;

    public PooledByteBufferTests()
        => this.sut = new PooledByteBuffer();

    [Fact]
    public void NewInstance_When_DisposingUsedInstance_And_InstantiatingNewInstance_Then_ByteBufferComesFromPool()
    {
        // Arrange
        byte[] expectedBytes = [1, 2, 3, 4];
        this.sut.WriteBytes(expectedBytes);
        this.sut.Dispose();

        // Act
        using var newSut = PooledByteBuffer.FromPool();

        // Assert
        Assert.Equal(expectedBytes, newSut.Bytes[..4]);
    }

    [Fact]
    public void Dispose_When_ReferenceCountIsZero_Then_ByteBufferIsReturnedToPool()
    {
        // Arrange
        byte[] expectedBytes = [5, 6, 7, 8];
        this.sut.WriteBytes(expectedBytes);

        // Act 1
        this.sut.Retain();
        this.sut.Dispose();

        // Assert 1
        Assert.Equal((uint)1, this.sut.ReferenceCount);
        using var pooledByteBufferDifferentBytes = PooledByteBuffer.FromPool();
        Assert.NotEqual(expectedBytes, pooledByteBufferDifferentBytes.Bytes[..4]);

        // Act 2: Finally the buffer should be returned to pool
        this.sut.Dispose();

        // Assert 2: Reference count is reset to 1 by the pool
        Assert.Equal((uint)1, this.sut.ReferenceCount);
    }

    [Fact]
    public void Retain_When_RetainingAndDisposingOnce_Then_ReferenceCountIsIncreasedAndDecreased()
    {
        // Act & Assert
        this.sut.Retain();

        Assert.Equal((uint)2, this.sut.ReferenceCount);

        this.sut.Dispose();

        Assert.Equal((uint)1, this.sut.ReferenceCount);
    }

    [Fact]
    public void ReadByte_When_WritingByte_And_ReadingByte_Then_CorrectWrittenByteIsReturnedAndSizeIsCorrect()
    {
        // Act
        this.sut.WriteByte(5);
        byte read = sut.ReadByte();

        // Assert
        Assert.Equal(1, this.sut.Size);
        Assert.Equal(5, read);
    }

    [Fact]
    public void ReadUInt_When_WritingUInt_And_ReadingUInt_Then_CorrectWrittenUIntIsReturnedAndSizeIsCorrect()
    {
        // Act
        this.sut.WriteUInt(500);
        uint read = this.sut.ReadUInt();

        // Assert
        Assert.Equal(4, this.sut.Size);
        Assert.Equal(500u, read);
    }

    [Fact]
    public void ReadUInt_When_BufferTooSmall_Then_ExceptionIsThrown()
    {
        // Arrange
        this.sut.WriteByte(1);
        this.sut.WriteByte(1);
        this.sut.WriteByte(1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => this.sut.ReadUInt());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(127, 1)]
    [InlineData(128, 2)]
    [InlineData(16_383, 2)]
    [InlineData(16_384, 3)]
    [InlineData(2_097_151, 3)]
    [InlineData(2_097_152, 4)]
    [InlineData(268_435_455, 4)]
    [InlineData(268_435_456, 5)]
    [InlineData(4_294_967_295, 5)]
    public void ReadVarUInt_When_WritingVarUInt_And_ReadingVarUInt_Then_CorrectWrittenVarUIntIsReturnedAndSizeIsCorrect(uint expectedValue, int expectedSize)
    {
        // Act
        this.sut.WriteVarUInt(expectedValue);
        uint read = this.sut.ReadVarUInt();

        // Assert
        Assert.Equal(expectedSize, this.sut.Size);
        Assert.Equal(expectedValue, read);
    }

    [Fact]
    public void ReadULong_When_WritingULong_And_ReadingULong_Then_CorrectWrittenULongIsReturnedAndSizeIsCorrect()
    {
        // Act
        this.sut.WriteULong(ulong.MaxValue);
        ulong read = this.sut.ReadULong();

        // Assert
        Assert.Equal(8, this.sut.Size);
        Assert.Equal(ulong.MaxValue, read);
    }

    [Fact]
    public void ReadFloat_When_WritingFloat_And_ReadingFloat_Then_CorrectWrittenFloatIsReturnedAndSizeIsCorrect()
    {
        // Act
        this.sut.WriteFloat(3.1415F);
        float read = this.sut.ReadFloat();

        // Assert
        Assert.Equal(4, this.sut.Size);
        Assert.Equal(3.1415F, read);
    }

    [Fact]
    public void ReadGuid_When_WritingGuid_And_ReadingGuid_Then_CorrectWrittenGuidIsReturnedAndSizeIsCorrect()
    {
        // Arrange
        Guid guid = Guid.Parse("00001000-0030-0030-0070-000000000000");

        // Act
        this.sut.WriteGuid(guid);
        Guid read = this.sut.ReadGuid();

        // Assert
        Assert.Equal(16, this.sut.Size);
        Assert.Equal(guid, read);
    }

    [Fact]
    public void ReadString_When_WritingString_And_ReadingString_Then_CorrectWrittenStringIsReturnedAndSizeIsCorrect()
    {
        // Arrange
        const byte valueVarUIntSize = 1;
        const string value = "Mimoria";

        // Act
        this.sut.WriteString(value);
        string read = this.sut.ReadString()!;

        // Assert
        Assert.Equal(value.Length + valueVarUIntSize, this.sut.Size);
        Assert.Equal(value, read);
    }

    [Fact]
    public void ReadString_When_WritingStringNull_And_ReadingString_Then_CorrectWrittenNullStringIsReturnedAndSizeIsCorrect()
    {
        // Act
        this.sut.WriteString(null);
        string? read = this.sut.ReadString();

        // Assert
        Assert.Equal(1, this.sut.Size);
        Assert.Null(read);
    }

    [Fact]
    public void ReadString_When_ReadingLargeStringWithTooSmallBuffer_Then_ExceptionIsThrown()
    {
        // Arrange
        this.sut.WriteVarUInt(100);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => this.sut.ReadString());
    }

    [Fact]
    public void ReadString_When_WritingStringLongerThanBuffer_And_ReadingString_Then_CorrectWrittenStringIsReturnedAndSizeIsCorrect()
    {
        // Arrange
        var value = new string('t', PooledByteBuffer.DefaultBufferSize + 1);

        // Act
        int bufferSizeBefore = this.sut.Bytes.Length;
        this.sut.WriteString(value);
        string read = this.sut.ReadString()!;

        // Assert
        Assert.Equal(bufferSizeBefore * PooledByteBuffer.BufferGrowFactor, this.sut.Bytes.Length);
        Assert.Equal(value, read);
    }

    [Fact]
    public void ReadString_When_ReadingStringLargerThanMaxStringSize_Then_ArgumentExceptionIsThrown()
    {
        // Arrange
        this.sut.WriteVarUInt(PooledByteBuffer.MaxStringSizeBytes + 1);
        this.sut.WriteBytes(new byte[PooledByteBuffer.MaxStringSizeBytes + 1]);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => this.sut.ReadString());
        Assert.Equal($"Read string value length '{PooledByteBuffer.MaxStringSizeBytes + 1}' exceeded max allowed length '{PooledByteBuffer.MaxStringSizeBytes}'", exception.Message);
    }

    [Fact]
    public void ReadBytes_When_WritingBytes_And_ReadingBytes_Then_CorrectWrittenBytesIsReturnedAndSizeIsCorrect()
    {
        // Arrange
        byte[] fibonacci = [0, 1, 1, 2, 3, 5, 8, 13];
        byte[] readFibonacci = new byte[fibonacci.Length];

        // Act
        this.sut.WriteBytes(fibonacci);
        this.sut.ReadBytes(readFibonacci);

        // Assert
        Assert.Equal(fibonacci.Length, this.sut.Size);
        Assert.Equal(fibonacci, readFibonacci);
    }

    [Fact]
    public void Equals_When_CheckingTwoByteBuffersWithEqualContent_Then_EqualsReturnsTrue()
    {
        // Arrange
        using var otherPooledByteBuffer = new PooledByteBuffer();

        // Act
        this.sut.WriteString("string");
        this.sut.WriteUInt(4);
        otherPooledByteBuffer.WriteString("string");
        otherPooledByteBuffer.WriteUInt(4);

        // Assert
        Assert.True(this.sut.Equals(otherPooledByteBuffer));
    }

    [Fact]
    public void Equals_When_CheckingTwoByteBuffersWithNotEqualContent_Then_EqualsReturnsFalse()
    {
        // Arrange
        using var otherPooledByteBuffer = new PooledByteBuffer();

        // Act
        this.sut.WriteString("string");
        this.sut.WriteUInt(4);
        otherPooledByteBuffer.WriteString("string");
        otherPooledByteBuffer.WriteUInt(2);

        // Assert
        Assert.False(this.sut.Equals(otherPooledByteBuffer));
    }

    [Fact]
    public void WriteString_When_WritingStringLargerThanMaxStringSize_Then_ArgumentExceptionIsThrown()
    {
        // Arrange
        var value = new string('t', (int)PooledByteBuffer.MaxStringSizeBytes + 1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => this.sut.WriteString(value));
        Assert.Equal($"Written string value length '{value.Length}' exceeded max allowed length '{PooledByteBuffer.MaxStringSizeBytes}'", exception.Message);
    }

    public void Dispose()
        => this.sut.Dispose();
}
