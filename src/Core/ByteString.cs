// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;

namespace Varelen.Mimoria.Core;

/// <summary>
/// Represents a byte array string as UTF8 bytes with a hash code.
/// </summary>
public sealed class ByteString
{
    // TODO: Lazy initialization of the hash code?
    private readonly int hashCode;

    /// <summary>
    /// Gets the UTF8 bytes of the string.
    /// </summary>
    public byte[] Bytes { get; }

    /// <summary>
    /// Gets the size of the UTF8 bytes.
    /// </summary>
    public int Size => this.Bytes.Length;

    /// <summary>
    /// Creates a new instance of the <see cref="ByteString"/> class.
    /// </summary>
    /// <param name="bytes">The UTF8 string bytes.</param>
    public ByteString(byte[] bytes)
    {
        this.Bytes = bytes;
        var hashCode = new HashCode();
        hashCode.AddBytes(this.Bytes);
        this.hashCode = hashCode.ToHashCode();
    }

    /// <summary>
    /// Compares two <see cref="ByteString"/> instances for equality.
    /// </summary>
    /// <param name="left">The left <see cref="ByteString"/> instance.</param>
    /// <param name="right">The right <see cref="ByteString"/> instance.</param>
    /// <returns>True if the two instances are equal; otherwise, false.</returns>
    public static bool operator ==(ByteString? left, ByteString? right)
        => left?.Equals(right) == true;

    /// <summary>
    /// Compares two <see cref="ByteString"/> instances for inequality.
    /// </summary>
    /// <param name="left">The left <see cref="ByteString"/> instance.</param>
    /// <param name="right">The right <see cref="ByteString"/> instance.</param>
    /// <returns>True if the two instances are not equal; otherwise, false.</returns>
    public static bool operator !=(ByteString? left, ByteString? right)
        => !(left == right);

    /// <summary>
    /// Implicitly converts a byte array to a <see cref="ByteString"/> instance.
    /// </summary>
    /// <param name="bytes">The UTF8 string bytes.</param>
    public static implicit operator ByteString(byte[] bytes)
        => new(bytes);

    /// <summary>
    /// Implicitly converts a <see cref="ByteString"/> instance to a byte array.
    /// </summary>
    /// <param name="byteString">The <see cref="ByteString"/> instance.</param>
    public static implicit operator byte[](ByteString byteString)
        => byteString.Bytes;

    /// <summary>
    /// Implicitly converts a <see cref="ByteString"/> instance to a <see cref="ReadOnlySpan{T}"/> of bytes.
    /// </summary>
    /// <param name="byteString">The <see cref="ByteString"/> instance.</param>
    public static implicit operator ReadOnlySpan<byte>(ByteString byteString)
        => byteString.Bytes.AsSpan();

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is not ByteString byteString)
        {
            return false;
        }

        return this.hashCode == byteString.hashCode;
    }

    /// <inheritdoc />
    public override int GetHashCode()
        => this.hashCode;
}
