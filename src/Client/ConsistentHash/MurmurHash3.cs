﻿// MIT License

// Copyright (c) 2019 Evgeny Yuryev

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Client.ConsistentHash;

public static class MurmurHash3
{
    public static uint Hash32(ReadOnlySpan<byte> bytes, uint seed)
    {
        int length = bytes.Length;
        uint h1 = seed;
        int remainder = length & 3;
        int position = length - remainder;
        for (int start = 0; start < position; start += 4)
        {
            h1 = (uint)((int)RotateLeft(h1 ^ RotateLeft(BitConverter.ToUInt32(bytes.Slice(start, 4)) * 3432918353U, 15) * 461845907U, 13) * 5 - 430675100);
        }

        if (remainder > 0)
        {
            uint num = 0;
            switch (remainder)
            {
                case 1:
                    num ^= bytes[position];
                    break;
                case 2:
                    num ^= (uint)bytes[position + 1] << 8;
                    goto case 1;
                case 3:
                    num ^= (uint)bytes[position + 2] << 16;
                    goto case 2;
            }

            h1 ^= RotateLeft(num * 3432918353U, 15) * 461845907U;
        }

        h1 = FMix(h1 ^ (uint)length);

        return h1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint x, byte r)
    {
        return x << (int)r | x >> 32 - (int)r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint FMix(uint h)
    {
        h = (uint)(((int)h ^ (int)(h >> 16)) * -2048144789);
        h = (uint)(((int)h ^ (int)(h >> 13)) * -1028477387);
        return h ^ h >> 16;
    }
}
