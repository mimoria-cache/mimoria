// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Bully;

public interface IBullyAlgorithm
{
    public delegate void BullyEvent();

    public event BullyEvent? LeaderElected;

    bool IsLeader { get; }
    int Leader { get; }
}
