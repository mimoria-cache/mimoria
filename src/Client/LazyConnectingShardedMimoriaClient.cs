// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client;

/// <summary>
/// A sharded client implementation that lazily establishes a connection to the Mimoria servers.
/// </summary>
public sealed class LazyConnectingShardedMimoriaClient : LazyConnectingMimoriaClient, IShardedMimoriaClient
{
    private readonly IShardedMimoriaClient shardedMimoriaClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyConnectingShardedMimoriaClient"/> class.
    /// </summary>
    /// <param name="shardedMimoriaClient">The underlying sharded Mimoria client to delegate operations to.</param>
    public LazyConnectingShardedMimoriaClient(IShardedMimoriaClient shardedMimoriaClient)
        : base(shardedMimoriaClient)
    {
        this.shardedMimoriaClient = shardedMimoriaClient;
    }

    /// <inheritdoc />
    public IReadOnlyList<IMimoriaClient> MimoriaClients => this.shardedMimoriaClient.MimoriaClients;

    /// <inheritdoc />
    public async Task<Stats> GetStatsAsync(int serverId, CancellationToken cancellationToken = default)
    {
        await base.EnsureConnectedAsync();

        return await this.shardedMimoriaClient.GetStatsAsync(serverId, cancellationToken);
    }
}
