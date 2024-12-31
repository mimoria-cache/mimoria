// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Replication;

public interface IReplicator : IDisposable
{
    ValueTask ReplicateSetStringAsync(string key, string? value, uint ttlMilliseconds);
}
