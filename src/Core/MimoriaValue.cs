// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("Varelen.Mimoria.Server")]

namespace Varelen.Mimoria.Core;

public readonly struct MimoriaValue
{
    public static readonly MimoriaValue Null = new();

    internal enum ValueType : byte
    {
        Null = 0,
        Bytes = 1,
        String = 2,
        Int = 3,
        Long = 4,
        Double = 5,
        Bool = 6
    }

    public object? Value { get; }
    internal ValueType Type { get; }

    public MimoriaValue()
    {
        this.Value = null;
        this.Type = ValueType.Null;
    }

    public MimoriaValue(byte[]? bytes)
    {
        this.Value = bytes;
        this.Type = bytes != null ? ValueType.Bytes : ValueType.Null;
    }

    public MimoriaValue(string? s)
    {
        this.Value = s;
        this.Type = s != null ? ValueType.String : ValueType.Null;
    }

    public MimoriaValue(int i)
    {
        this.Value = i;
        this.Type = ValueType.Int;
    }

    public MimoriaValue(long l)
    {
        this.Value = l;
        this.Type = ValueType.Long;
    }

    public MimoriaValue(double d)
    {
        this.Value = d;
        this.Type = ValueType.Double;
    }

    public MimoriaValue(bool b)
    {
        this.Value = b;
        this.Type = ValueType.Bool;
    }

    public override string? ToString()
    {
        return this.Type switch
        {
            ValueType.Null => "null",
            ValueType.Bytes => Convert.ToHexString((byte[])this.Value!),
            ValueType.String => (string?)this.Value,
            ValueType.Int or ValueType.Long or ValueType.Double or ValueType.Bool => this.Value!.ToString(),
            _ => throw new InvalidOperationException($"Unkown type {this.Type}"),
        };
    }


    public static implicit operator MimoriaValue(byte[]? value)
        => new(value);

    public static implicit operator MimoriaValue(string? value)
        => new(value);

    public static implicit operator MimoriaValue(int value)
        => new(value);

    public static implicit operator MimoriaValue(long value)
        => new(value);

    public static implicit operator MimoriaValue(double value)
        => new(value);

    public static implicit operator MimoriaValue(bool value)
        => new(value);

    public static implicit operator string?(MimoriaValue value)
    {
        return value.Type switch
        {
            ValueType.Null => null,
            ValueType.String => (string)value.Value!,
            ValueType.Int or ValueType.Long or ValueType.Double or ValueType.Bool => value.Value!.ToString(),
            ValueType.Bytes => Encoding.UTF8.GetString((byte[])value.Value!),
            _ => throw new InvalidOperationException(),
        };
    }
}
