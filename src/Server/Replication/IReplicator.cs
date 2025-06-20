﻿// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Server.Replication;

public interface IReplicator : IDisposable
{
    ValueTask ReplicateSetStringAsync(string key, ByteString? value, uint ttlMilliseconds);

    ValueTask ReplicateSetBytesAsync(string key, byte[]? value, uint ttlMilliseconds);

    ValueTask ReplicateAddListAsync(string key, ByteString? value, uint ttlMilliseconds, uint valueTtlMilliseconds);

    ValueTask ReplicateRemoveListAsync(string key, ByteString value);

    ValueTask ReplicateSetMapValueAsync(string key, string subKey, MimoriaValue value, uint ttlMilliseconds);

    ValueTask ReplicateSetMapAsync(string key, Dictionary<string, MimoriaValue> map, uint ttlMilliseconds);

    ValueTask ReplicateSetCounterAsync(string key, long value);

    ValueTask ReplicateIncrementCounterAsync(string key, long increment);

    ValueTask ReplicateDeleteAsync(string key);

    ValueTask ReplicateDeletePatternAsync(string pattern, Comparison comparison);

    ValueTask ReplicateClearAsync();
}
