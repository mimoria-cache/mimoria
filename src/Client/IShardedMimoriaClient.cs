// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

/// <summary>
/// Represents a sharded mimoria client.
/// </summary>
public interface IShardedMimoriaClient : IMimoriaClient
{
    /// <summary>
    /// Gets the list of mimoria clients.
    /// </summary>
    public IReadOnlyList<IMimoriaClient> MimoriaClients { get; }

    /// <summary>
    /// Gets the stats of the given server.
    /// </summary>
    /// <param name="serverId">The server id.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the server stats.</returns>
    Task<Stats> GetStatsAsync(int serverId, CancellationToken cancellationToken = default);
}
