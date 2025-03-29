// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Replication;

public interface IReplicator : IDisposable
{
    ValueTask ReplicateSetStringAsync(string key, string? value, uint ttlMilliseconds);

    ValueTask ReplicateSetBytesAsync(string key, byte[]? value, uint ttlMilliseconds);

    ValueTask ReplicateAddListAsync(string key, string? value, uint ttlMilliseconds, uint valueTtlMilliseconds);

    ValueTask ReplicateRemoveListAsync(string key, string value);

    ValueTask ReplicateDeleteAsync(string key);
}
