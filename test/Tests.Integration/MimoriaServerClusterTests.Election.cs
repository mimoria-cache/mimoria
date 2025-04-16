// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Varelen.Mimoria.Server;
using Varelen.Mimoria.Server.Bully;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerClusterTests : IAsyncLifetime
{
    [Fact]
    public void Election_Given_TwoNodes_HighestIdIsLeader()
    {
        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);
    }

    [Fact]
    public async Task Election_Given_TwoNodes_When_SecondaryGoesOffAndComesBackOn_EverythingAsBefore()
    {
        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        this.mimoriaServerOne.Stop();

        var optionsMock = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock.CurrentValue.Returns(new MimoriaOptions() { Port = this.firstPort, Password = Password, Cluster = new MimoriaOptions.ClusterOptions() { Id = 1, Port = this.firstClusterPort, Password = ClusterPassword, Nodes = [new() { Id = 2, Host = "127.0.0.1", Port = this.secondClusterPort }] } });

        this.mimoriaServerOne = new MimoriaServer(this.loggerFactory.CreateLogger<MimoriaServer>(), this.loggerFactory, optionsMock, Substitute.For<IPubSubService>(), new MimoriaSocketServer(NullLogger<MimoriaSocketServer>.Instance, this.metrics), this.cacheOne, this.metrics);
        await this.mimoriaServerOne.StartAsync();

        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);
    }

    [Fact]
    public async Task Election_Given_TwoNodes_When_PrimaryGoesOffAndComesBackOn_SecondaryIsLeaderAndAfterThatPrimaryIsTheNewLeader()
    {
        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        this.mimoriaServerTwo.Stop();

        var taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task HandleLeaderElectedAsync(int leaderId)
        {
            taskCompletionSource.SetResult();
            return Task.CompletedTask;
        }

        ((BullyAlgorithm)this.mimoriaServerOne.BullyAlgorithm).LeaderElected += HandleLeaderElectedAsync;

        // Wait for server two to stop and for server one to be elected as new leader
        await taskCompletionSource.Task;

        taskCompletionSource = new TaskCompletionSource();

        Assert.True(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(1, this.mimoriaServerOne.BullyAlgorithm.Leader);

        var optionsMock = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock.CurrentValue.Returns(new MimoriaOptions() { Port = this.secondPort, Password = Password, Cluster = new MimoriaOptions.ClusterOptions() { Id = 2, Port = this.secondClusterPort, Password = ClusterPassword, Nodes = [new() { Id = 1, Host = "127.0.0.1", Port = this.firstClusterPort }] } });

        this.mimoriaServerTwo = new MimoriaServer(this.loggerFactory.CreateLogger<MimoriaServer>(), this.loggerFactory, optionsMock, Substitute.For<IPubSubService>(), new MimoriaSocketServer(NullLogger<MimoriaSocketServer>.Instance, this.metrics), this.cacheTwo, this.metrics);
        await this.mimoriaServerTwo.StartAsync();

        // Wait for server one to receive new election for server two as leader
        await taskCompletionSource.Task;

        ((BullyAlgorithm)this.mimoriaServerOne.BullyAlgorithm).LeaderElected -= HandleLeaderElectedAsync;

        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);
    }
}
