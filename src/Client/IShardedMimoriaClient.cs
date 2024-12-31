// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

public interface IShardedMimoriaClient : IMimoriaClient
{
    public IReadOnlyList<IMimoriaClient> MimoriaClients { get; }

    Task<Stats> GetStatsAsync(int serverId, CancellationToken cancellationToken = default);
}
