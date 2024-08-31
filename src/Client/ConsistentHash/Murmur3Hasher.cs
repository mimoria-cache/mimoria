// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Text;

namespace Varelen.Mimoria.Client.ConsistentHash;

/// <summary>
/// This is the Murmur3 hasher implementation which is used for consistent hashing.
/// </summary>
public sealed class Murmur3Hasher : IHasher
{
    // TODO: Review
    private const uint Seed = 0xa4204b3c;

    public uint Hash(string value)
    {
        byte[] valueBytes = Encoding.UTF8.GetBytes(value);
        return MurmurHash3.Hash32(valueBytes, Seed);
    }
}
