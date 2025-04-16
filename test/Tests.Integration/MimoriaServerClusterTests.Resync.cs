// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server;
using Varelen.Mimoria.Server.Bully;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerClusterTests : IAsyncLifetime
{
    [Fact]
    public async Task Resync_Given_TwoNodes_When_WriteToPrimary_AndSecondaryGoesOfflineAndComesBackOnline_Then_SecondaryAlsoHasKeyAfterResync()
    {
        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        var map = new Dictionary<string, MimoriaValue>()
        {
            { "string", "string" },
            { "int", (int)1 },
            { "long", (long)1 },
            { "double", (double)1.23 },
            { "bytes", new byte[] { 1, 2, 3, 4 } }
        };

        await clusterMimoriaClient.SetStringAsync("string", "value");
        await clusterMimoriaClient.SetStringAsync("string:null", null);
        
        await clusterMimoriaClient.SetBytesAsync("bytes", [5, 6, 7, 8]);
        await clusterMimoriaClient.SetBytesAsync("bytes:null", null);

        await clusterMimoriaClient.AddListAsync("list", "foo");
        await clusterMimoriaClient.AddListAsync("list", "bar");
        
        await clusterMimoriaClient.SetMapAsync("map", map);

        await clusterMimoriaClient.IncrementCounterAsync("counter", 2);

        this.mimoriaServerOne.Stop();

        this.cacheOne = new ExpiringDictionaryCache(NullLogger<ExpiringDictionaryCache>.Instance, this.metrics, Substitute.For<IPubSubService>(), TimeSpan.FromMinutes(5));

        var optionsMock = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock.CurrentValue.Returns(new MimoriaOptions() { Port = this.firstPort, Password = Password, Cluster = new MimoriaOptions.ClusterOptions() { Id = 1, Port = this.firstClusterPort, Password = ClusterPassword, Nodes = [new() { Id = 2, Host = "127.0.0.1", Port = this.secondClusterPort }] } });

        this.mimoriaServerOne = new MimoriaServer(this.loggerFactory.CreateLogger<MimoriaServer>(), this.loggerFactory, optionsMock, Substitute.For<IPubSubService>(), new MimoriaSocketServer(NullLogger<MimoriaSocketServer>.Instance, this.metrics), this.cacheOne, this.metrics);
        await this.mimoriaServerOne.StartAsync();

        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        string? stringValue = await clusterMimoriaClient.GetStringAsync("string", preferSecondary: true);
        string? stringValueNull = await clusterMimoriaClient.GetStringAsync("string:null", preferSecondary: true);
        
        byte[]? bytesValue = await clusterMimoriaClient.GetBytesAsync("bytes", preferSecondary: true);
        byte[]? bytesValueNull = await clusterMimoriaClient.GetBytesAsync("bytes:null", preferSecondary: true);

        List<string> listValue = await clusterMimoriaClient.GetListAsync("list", preferSecondary: true);
        
        Dictionary<string, MimoriaValue> mapValue = await clusterMimoriaClient.GetMapAsync("map", preferSecondary: true);
        
        long counterValue = await clusterMimoriaClient.GetCounterAsync("counter");

        // Assert
        Assert.Equal("value", stringValue);
        Assert.Null(stringValueNull);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, bytesValue);
        Assert.Null(bytesValueNull);
        Assert.Equal(["foo", "bar"], listValue);
        Assert.Equal(map, mapValue);
        Assert.Equal(2, counterValue);
    }

    [Fact]
    public async Task Resync_Given_TwoNodes_When_WriteToPrimary_AndPrimaryGoesOfflineAndComesBackOnline_Then_PrimaryAlsoHasKeyAfterResync()
    {
        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        await clusterMimoriaClient.SetStringAsync("string", "value resync");

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

        ((BullyAlgorithm)this.mimoriaServerOne.BullyAlgorithm).LeaderElected -= HandleLeaderElectedAsync;

        await clusterMimoriaClient.SetStringAsync("string2", "value resync 2");

        this.cacheTwo = new ExpiringDictionaryCache(NullLogger<ExpiringDictionaryCache>.Instance, this.metrics, Substitute.For<IPubSubService>(), TimeSpan.FromMinutes(5));

        var optionsMock = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock.CurrentValue.Returns(new MimoriaOptions() { Password = Password, Port = this.secondPort, Cluster = new MimoriaOptions.ClusterOptions() { Id = 2, Port = secondClusterPort, Password = ClusterPassword, Nodes = [new() { Id = 1, Host = "127.0.0.1", Port = this.firstClusterPort }] } });

        this.mimoriaServerTwo = new MimoriaServer(this.loggerFactory.CreateLogger<MimoriaServer>(), this.loggerFactory, optionsMock, Substitute.For<IPubSubService>(), new MimoriaSocketServer(NullLogger<MimoriaSocketServer>.Instance, this.metrics), this.cacheTwo, this.metrics);
        await this.mimoriaServerTwo.StartAsync();

        Assert.False(this.mimoriaServerOne.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerOne.BullyAlgorithm.Leader);

        Assert.True(this.mimoriaServerTwo.BullyAlgorithm!.IsLeader);
        Assert.Equal(2, this.mimoriaServerTwo.BullyAlgorithm.Leader);

        string? stringValue = await clusterMimoriaClient.GetStringAsync("string", preferSecondary: false);
        string? stringValue2 = await clusterMimoriaClient.GetStringAsync("string2", preferSecondary: false);

        // Assert
        Assert.Equal("value resync", stringValue);
        Assert.Equal("value resync 2", stringValue2);
    }
}
