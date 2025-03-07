// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Bully;

public interface IBullyAlgorithm
{
    bool IsLeader { get; }
    int Leader { get; }
}
