// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

public interface IShardedMimoriaClient : IMimoriaClient
{
    public IReadOnlyList<IMimoriaClient> CacheClients { get; }

    Task<Stats> GetStatsAsync(int index, CancellationToken cancellationToken = default);

    Task<Stats> GetStatsAsync(Guid serverId, CancellationToken cancellationToken = default);
}
