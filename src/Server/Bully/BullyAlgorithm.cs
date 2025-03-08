// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Cluster;

[assembly: InternalsVisibleTo("Varelen.Mimoria.Tests.Integration")]

namespace Varelen.Mimoria.Server.Bully;

/// <summary>
/// Simple implementation of https://en.wikipedia.org/wiki/Bully_algorithm
/// </summary>
public sealed class BullyAlgorithm : IBullyAlgorithm
{
    private const int MissingLeaderCheckIntervalMs = 500;

    private readonly ILogger<BullyAlgorithm> logger;
    private readonly int id;
    private readonly int[] nodeIds;
    private readonly ClusterServer clusterServer;
    private readonly TimeSpan leaderHeartbeatInterval;
    private readonly TimeSpan leaderMissingTimeout;
    private readonly TimeSpan electionTimeout;
    private readonly PeriodicTimer periodicTimer;

    private DateTime lastReceivedHeartbeat;

    private int receivedAlives;
    private int lastAliveReceivedLeader;
    private bool leaderElected;

    internal Func<Task> leaderElectedAsyncCallback;

    public bool IsLeader { get; private set; }

    private bool IsLeaderMissing => DateTime.Now - this.lastReceivedHeartbeat >= this.leaderMissingTimeout;

    public int Leader {  get; private set; }

    public BullyAlgorithm(
        ILogger<BullyAlgorithm> logger,
        int id,
        int[] nodeIds,
        ClusterServer clusterServer,
        TimeSpan leaderHeartbeatInterval,
        TimeSpan leaderMissingTimeout,
        TimeSpan electionTimeout,
        Func<Task> leaderElectedAsyncCallback)
    {
        this.logger = logger;
        this.id = id;
        this.nodeIds = nodeIds;
        this.clusterServer = clusterServer;
        this.leaderHeartbeatInterval = leaderHeartbeatInterval;
        this.leaderMissingTimeout = leaderMissingTimeout;
        this.electionTimeout = electionTimeout;
        this.periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(MissingLeaderCheckIntervalMs));
        this.IsLeader = false;
        this.lastReceivedHeartbeat = DateTime.Now;
        this.receivedAlives = 0;
        this.leaderElected = false;
        this.Leader = -1;
        this.leaderElectedAsyncCallback = leaderElectedAsyncCallback;
    }

    private async Task StartElectionAsync()
    {
        if (this.leaderElected)
        {
            this.logger.LogInformation("No need to start an election, we already have leader '{LeaderId}'", this.Leader);
            return;
        }

        this.logger.LogInformation("Starting new election with '{ClusterClientCount}' cluster clients connected", this.clusterServer.Clients.Count);

        this.IsLeader = false;
        this.receivedAlives = 0;

        int maxNodeId = this.nodeIds.Max();
        bool hasHighest = this.id > maxNodeId;

        this.logger.LogDebug("My id is '{Id}' and highest other node id is '{HighestOtherNodeId}'", this.id, maxNodeId);

        if (hasHighest)
        {
            this.logger.LogInformation("I am the leader");
            this.IsLeader = true;
            this.Leader = this.id;
            this.leaderElected = true;

            await this.SendVictoryMessageAsync();

            await this.OnLeaderElectedAsync();

            _ = this.StartLeaderHeartbeatAsync();
        }
        else
        {
            this.logger.LogDebug("Sending election messages to higher ids");

            await this.SendElectionMessageAsync();

            await Task.Delay(this.electionTimeout);

            if (this.leaderElected)
            {
                this.logger.LogInformation("We already got leader '{LeaderId}' by victory message during election", this.Leader);
                return;
            }

            if (this.receivedAlives == 0)
            {
                this.logger.LogInformation("I am the new leader, no other higher node answered");

                this.IsLeader = true;
                this.Leader = this.id;
                this.leaderElected = true;

                await this.SendVictoryMessageAsync();

                await this.OnLeaderElectedAsync();

                _ = this.StartLeaderHeartbeatAsync();
            }
            else
            {
                this.lastReceivedHeartbeat = DateTime.Now;
                this.IsLeader = false;

                if (!this.leaderElected && this.lastAliveReceivedLeader != -1)
                {
                    this.Leader = this.lastAliveReceivedLeader;
                    this.logger.LogDebug("Leader overwritten to '{LeaderId}'", this.lastAliveReceivedLeader);
                }

                this.logger.LogInformation("Received '{ReceivedAlives}' alives, I am a member and current leader is '{LeaderId}'", this.receivedAlives, this.Leader);

                this.leaderElected = true;
                await this.OnLeaderElectedAsync();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task OnLeaderElectedAsync()
        => await this.leaderElectedAsyncCallback();

    private async ValueTask SendElectionMessageAsync()
    {
        using var electionMessageBuffer = PooledByteBuffer.FromPool(Operation.ElectionMessage, requestId: 0);
        electionMessageBuffer.EndPacket();

        foreach (var (_, clusterConnection) in this.clusterServer.Clients.Where(c => c.Value.Id > this.id))
        {
            electionMessageBuffer.Retain();

            await clusterConnection.SendAsync(electionMessageBuffer);
        }
    }

    private async ValueTask SendVictoryMessageAsync()
    {
        using var victoryMessageBuffer = PooledByteBuffer.FromPool(Operation.VictoryMessage, requestId: 0);
        victoryMessageBuffer.WriteInt(this.id);
        victoryMessageBuffer.EndPacket();

        foreach (var (_, clusterConnection) in this.clusterServer.Clients)
        {
            victoryMessageBuffer.Retain();

            await clusterConnection.SendAsync(victoryMessageBuffer);
        }
    }

    private async Task StartLeaderHeartbeatAsync()
    {
        while (this.IsLeader)
        {
            using var heartbeatMessageBuffer = PooledByteBuffer.FromPool(Operation.HeartbeatMessage, requestId: 0);
            heartbeatMessageBuffer.WriteInt(this.id);
            heartbeatMessageBuffer.EndPacket();

            foreach (var (_, clusterConnection) in this.clusterServer.Clients)
            {
                heartbeatMessageBuffer.Retain();
                
                await clusterConnection.SendAsync(heartbeatMessageBuffer);
            }

            await Task.Delay(this.leaderHeartbeatInterval);
        }
    }

    public async Task StartAsync()
    {
        try
        {
            // First election does not reset 'leaderElected' or 'Leader'
            // because we could have received the victory message already
            // and so we don't need to start an election
            await this.StartElectionAsync();

            while (await this.periodicTimer.WaitForNextTickAsync())
            {
                if (!this.IsLeader && this.IsLeaderMissing)
                {
                    this.lastReceivedHeartbeat = DateTime.Now;
                    this.leaderElected = false;
                    this.Leader = -1;

                    await this.StartElectionAsync();
                }
            }
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Unexpected error in bully algorithm core loop");
        }
    }

    public void HandleAlive(int leader)
    {
        this.receivedAlives++;
        this.lastAliveReceivedLeader = leader;
    }

    public void HandleHeartbeat(int leaderId)
    {
        this.lastReceivedHeartbeat = DateTime.Now;

        this.logger.LogTrace("Received heartbeat from leader '{LeaderId}'", leaderId);
    }

    public async Task HandleVictoryAsync(int leaderId)
    {
        this.lastReceivedHeartbeat = DateTime.Now;
        this.Leader = leaderId;
        this.IsLeader = false;
        this.leaderElected = true;

        this.logger.LogInformation("New leader is '{LeaderId}'", leaderId);

        await this.OnLeaderElectedAsync();
    }

    public void Stop()
    {
        this.IsLeader = false;
        this.periodicTimer.Dispose();
    }
}
