// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.ConsistentHash;

public interface IHasher
{
    uint Hash(string value);
}
