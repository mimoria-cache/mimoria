// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

[assembly: InternalsVisibleTo("Varelen.Mimoria.Server")]

namespace Varelen.Mimoria.Core;

/// <summary>
/// Represents a value that can be stored in a Mimoria map.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct MimoriaValue
{
    /// <summary>
    /// A null value.
    /// </summary>
    public static readonly MimoriaValue Null = new();

    /// <summary>
    /// Represents the type of the value stored in <see cref="MimoriaValue"/>.
    /// </summary>
    internal enum ValueType : byte
    {
        /// <summary>
        /// Represents a null value.
        /// </summary>
        Null = 0,
        /// <summary>
        /// Represents a byte array value.
        /// </summary>
        Bytes = 1,
        /// <summary>
        /// Represents a string value.
        /// </summary>
        String = 2,
        /// <summary>
        /// Represents an integer value.
        /// </summary>
        Int = 3,
        /// <summary>
        /// Represents a long value.
        /// </summary>
        Long = 4,
        /// <summary>
        /// Represents a double value.
        /// </summary>
        Double = 5,
        /// <summary>
        /// Represents a boolean value.
        /// </summary>
        Bool = 6
    }

    /// <summary>
    /// Gets the value stored in this <see cref="MimoriaValue"/>.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the type of the value stored in this <see cref="MimoriaValue"/>.
    /// </summary>
    internal ValueType Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with a null value.
    /// </summary>
    public MimoriaValue()
    {
        this.Value = null;
        this.Type = ValueType.Null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with a byte array value.
    /// </summary>
    /// <param name="bytes">The byte array value.</param>
    public MimoriaValue(byte[]? bytes)
    {
        this.Value = bytes;
        this.Type = bytes != null ? ValueType.Bytes : ValueType.Null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with a string value.
    /// </summary>
    /// <param name="s">The string value.</param>
    public MimoriaValue(string? s)
    {
        this.Value = s is not null ? new ByteString(Encoding.UTF8.GetBytes(s)) : null;
        this.Type = s != null ? ValueType.String : ValueType.Null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with a <see cref="ByteString"/> value.
    /// </summary>
    /// <param name="s">The byte string value.</param>
    public MimoriaValue(ByteString? s)
    {
        this.Value = s;
        this.Type = s != null ? ValueType.String : ValueType.Null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with an integer value.
    /// </summary>
    /// <param name="i">The integer value.</param>
    public MimoriaValue(int i)
    {
        this.Value = i;
        this.Type = ValueType.Int;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with a long value.
    /// </summary>
    /// <param name="l">The long value.</param>
    public MimoriaValue(long l)
    {
        this.Value = l;
        this.Type = ValueType.Long;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with a double value.
    /// </summary>
    /// <param name="d">The double value.</param>
    public MimoriaValue(double d)
    {
        this.Value = d;
        this.Type = ValueType.Double;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimoriaValue"/> struct with a boolean value.
    /// </summary>
    /// <param name="b">The boolean value.</param>
    public MimoriaValue(bool b)
    {
        this.Value = b;
        this.Type = ValueType.Bool;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is not MimoriaValue other || this.Type != other.Type)
        {
            return false;
        }

        return this.Type switch
        {
            ValueType.Null => true,
            ValueType.Bytes => ((byte[])this.Value!).SequenceEqual((byte[])other.Value!),
            ValueType.String => ((ByteString)this.Value!).Bytes.AsSpan().SequenceEqual(((ByteString)other.Value!).Bytes.AsSpan()),
            ValueType.Int => (int)this.Value! == (int)other.Value!,
            ValueType.Long => (long)this.Value! == (long)other.Value!,
            ValueType.Double => (double)this.Value! == (double)other.Value!,
            ValueType.Bool => (bool)this.Value! == (bool)other.Value!,
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add((byte)this.Type);

        switch (this.Type)
        {
            case ValueType.Null:
                break;
            case ValueType.Bytes:
                hashCode.AddBytes((byte[])this.Value!);
                break;
            case ValueType.String:
                hashCode.AddBytes((ByteString)this.Value!);
                break;
            case ValueType.Int:
                hashCode.Add((int)this.Value!);
                break;
            case ValueType.Long:
                hashCode.Add((long)this.Value!);
                break;
            case ValueType.Double:
                hashCode.Add((double)this.Value!);
                break;
            case ValueType.Bool:
                hashCode.Add((bool)this.Value!);
                break;
            default:
                throw new InvalidOperationException($"Unknown type '{this.Type}'");
        }

        return hashCode.ToHashCode();
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        return this.Type switch
        {
            ValueType.Null => "null",
            ValueType.Bytes => Convert.ToHexString((byte[])this.Value!),
            ValueType.String => Encoding.UTF8.GetString(((ByteString)this.Value!).Bytes.AsSpan()),
            ValueType.Int or ValueType.Long or ValueType.Double or ValueType.Bool => this.Value!.ToString(),
            _ => throw new InvalidOperationException($"Unknown type {this.Type}"),
        };
    }

    /// <summary>
    /// Implicitly converts a byte array to a <see cref="MimoriaValue"/>.
    /// </summary>
    /// <param name="value">The byte array value.</param>
    public static implicit operator MimoriaValue(byte[]? value)
        => new(value);

    /// <summary>
    /// Implicitly converts a string to a <see cref="MimoriaValue"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    public static implicit operator MimoriaValue(string? value)
        => new(value);

    /// <summary>
    /// Implicitly converts a byte string to a <see cref="MimoriaValue"/>.
    /// </summary>
    /// <param name="value">The byte string value.</param>
    public static implicit operator MimoriaValue(ByteString? value)
        => new(value);

    /// <summary>
    /// Implicitly converts an integer to a <see cref="MimoriaValue"/>.
    /// </summary>
    /// <param name="value">The integer value.</param>
    public static implicit operator MimoriaValue(int value)
        => new(value);

    /// <summary>
    /// Implicitly converts a long to a <see cref="MimoriaValue"/>.
    /// </summary>
    /// <param name="value">The long value.</param>
    public static implicit operator MimoriaValue(long value)
        => new(value);

    /// <summary>
    /// Implicitly converts a double to a <see cref="MimoriaValue"/>.
    /// </summary>
    /// <param name="value">The double value.</param>
    public static implicit operator MimoriaValue(double value)
        => new(value);

    /// <summary>
    /// Implicitly converts a boolean to a <see cref="MimoriaValue"/>.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    public static implicit operator MimoriaValue(bool value)
        => new(value);

    /// <summary>
    /// Implicitly converts a <see cref="MimoriaValue"/> to a byte array.
    /// </summary>
    /// <param name="value">The <see cref="MimoriaValue"/>.</param>
    public static implicit operator byte[](MimoriaValue value)
        => (byte[])value.Value!;

    /// <summary>
    /// Implicitly converts a <see cref="MimoriaValue"/> to an integer.
    /// </summary>
    /// <param name="value">The <see cref="MimoriaValue"/>.</param>
    public static implicit operator int(MimoriaValue value)
        => (int)value.Value!;

    /// <summary>
    /// Implicitly converts a <see cref="MimoriaValue"/> to a long.
    /// </summary>
    /// <param name="value">The <see cref="MimoriaValue"/>.</param>
    public static implicit operator long(MimoriaValue value)
        => (long)value.Value!;

    /// <summary>
    /// Implicitly converts a <see cref="MimoriaValue"/> to a double.
    /// </summary>
    /// <param name="value">The <see cref="MimoriaValue"/>.</param>
    public static implicit operator double(MimoriaValue value)
        => (double)value.Value!;

    /// <summary>
    /// Implicitly converts a <see cref="MimoriaValue"/> to a boolean.
    /// </summary>
    /// <param name="value">The <see cref="MimoriaValue"/>.</param>
    public static implicit operator bool(MimoriaValue value)
        => (bool)value.Value!;

    /// <summary>
    /// Implicitly converts a <see cref="MimoriaValue"/> to a string.
    /// </summary>
    /// <param name="value">The <see cref="MimoriaValue"/>.</param>
    public static implicit operator string?(MimoriaValue value)
    {
        return value.Type switch
        {
            ValueType.Null => null,
            ValueType.String => Encoding.UTF8.GetString(((ByteString)value.Value!).Bytes.AsSpan()),
            ValueType.Int or ValueType.Long or ValueType.Double or ValueType.Bool => value.Value!.ToString(),
            ValueType.Bytes => Convert.ToHexString((byte[])value.Value!),
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// Determines whether two specified <see cref="MimoriaValue"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="MimoriaValue"/> to compare.</param>
    /// <param name="right">The second <see cref="MimoriaValue"/> to compare.</param>
    /// <returns><c>true</c> if the two <see cref="MimoriaValue"/> instances are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(MimoriaValue left, MimoriaValue right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two specified <see cref="MimoriaValue"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="MimoriaValue"/> to compare.</param>
    /// <param name="right">The second <see cref="MimoriaValue"/> to compare.</param>
    /// <returns><c>true</c> if the two <see cref="MimoriaValue"/> instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(MimoriaValue left, MimoriaValue right)
        => !(left == right);
}
