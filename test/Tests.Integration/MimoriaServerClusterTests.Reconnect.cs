// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;
using Varelen.Mimoria.Server;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerClusterTests : IAsyncLifetime
{
    [Fact]
    public async Task Reconnect_Given_TwoNodes_When_PrimaryGoesOffline_And_SetString_Then_RequestIsSentToNewPrimary()
    {
        const string key = "string:reconnect:key";
        const string value = "value";

        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        await this.mimoriaServerTwo.StopAsync();

        await clusterMimoriaClient.SetStringAsync(key, value);

        string? actualValue = await clusterMimoriaClient.GetStringAsync(key);

        Assert.Equal(value, actualValue);
        Assert.True(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(1, this.mimoriaServerOne.BullyAlgorithm.Leader);
    }

    [Fact]
    public async Task Reconnect_Given_TwoNodes_When_PrimaryRestarts_And_SetString_Then_RequestIsSentToFirstPrimary()
    {
        const string key = "string:reconnect:key";
        const string value = "value";

        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        await this.mimoriaServerTwo.StopAsync();

        var optionsMock = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock.CurrentValue.Returns(new MimoriaOptions() { Port = this.secondPort, Password = Password, Cluster = new MimoriaOptions.ClusterOptions() { Id = 2, Port = this.secondClusterPort, Password = ClusterPassword, Nodes = [new() { Id = 1, Host = "127.0.0.1", Port = this.firstClusterPort }] } });

        this.mimoriaServerTwo = new MimoriaServer(this.loggerFactory.CreateLogger<MimoriaServer>(), this.loggerFactory, optionsMock, Substitute.For<IPubSubService>(), new MimoriaSocketServer(this.loggerFactory.CreateLogger<MimoriaSocketServer>(), this.metrics), this.cacheTwo, this.metrics);
        await this.mimoriaServerTwo.StartAsync();

        await clusterMimoriaClient.SetStringAsync(key, value);

        string? actualValue = await clusterMimoriaClient.GetStringAsync(key);

        Assert.True(clusterMimoriaClient.MimoriaClients[0].IsConnected);
        Assert.False(clusterMimoriaClient.MimoriaClients[0].IsPrimary);

        Assert.True(clusterMimoriaClient.MimoriaClients[1].IsConnected);
        Assert.True(clusterMimoriaClient.MimoriaClients[1].IsPrimary);

        Assert.Equal(value, actualValue);

        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);
        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);
    }
}
