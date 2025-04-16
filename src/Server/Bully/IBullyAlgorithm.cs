// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Bully;

public interface IBullyAlgorithm
{
    public delegate Task LeaderElectedAsyncEvent(int newLeaderId);

    event LeaderElectedAsyncEvent? LeaderElected;

    bool IsLeader { get; }

    int Leader { get; }
}
