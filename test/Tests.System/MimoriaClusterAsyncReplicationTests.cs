// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

using System.Net;

using Varelen.Mimoria.Client;

namespace Varelen.Mimoria.Tests.System;

public class MimoriaClusterAsyncReplicationTests : IAsyncLifetime
{
    private const string Password = "cool";
    private const string ClusterPassword = "coolcluster";
    private const int ReplicationIntervalMilliseconds = 3000;

    private IContainer firstInstance = null!;
    private IContainer secondInstance = null!;
    private IContainer thirdInstance = null!;

    public async Task InitializeAsync()
    {
        string firstName = Guid.NewGuid().ToString();
        string secondName = Guid.NewGuid().ToString();
        string thirdName = Guid.NewGuid().ToString();

        var network = new NetworkBuilder()
            .Build();

        IFutureDockerImage image = await MimoriaDockerImage.GetOrCreateImageAsync();

        this.firstInstance = new ContainerBuilder()
            .WithName(firstName)
            .WithNetwork(network)
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithEnvironment("MIMORIA__CLUSTER__ID", "1")
            .WithEnvironment("MIMORIA__CLUSTER__IP", "0.0.0.0")
            .WithEnvironment("MIMORIA__CLUSTER__PORT", "6566")
            .WithEnvironment("MIMORIA__CLUSTER__PASSWORD", ClusterPassword)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__ID", "2")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__HOST", secondName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__PORT", "6568")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__ID", "3")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__HOST", thirdName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__PORT", "6570")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__TYPE", "async")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__INTERVALMILLISECONDS", ReplicationIntervalMilliseconds.ToString())
            .WithPortBinding(6565, assignRandomHostPort: true)
            .WithPortBinding(6566, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        this.secondInstance = new ContainerBuilder()
            .WithName(secondName)
            .WithNetwork(network)
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithEnvironment("MIMORIA__CLUSTER__ID", "2")
            .WithEnvironment("MIMORIA__CLUSTER__IP", "0.0.0.0")
            .WithEnvironment("MIMORIA__CLUSTER__PORT", "6568")
            .WithEnvironment("MIMORIA__CLUSTER__PASSWORD", ClusterPassword)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__ID", "1")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__HOST", firstName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__PORT", "6566")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__ID", "3")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__HOST", thirdName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__PORT", "6570")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__TYPE", "async")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__INTERVALMILLISECONDS", ReplicationIntervalMilliseconds.ToString())
            .WithPortBinding(6565, assignRandomHostPort: true)
            .WithPortBinding(6568, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        this.thirdInstance = new ContainerBuilder()
            .WithName(thirdName)
            .WithNetwork(network)
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithEnvironment("MIMORIA__CLUSTER__ID", "3")
            .WithEnvironment("MIMORIA__CLUSTER__IP", "0.0.0.0")
            .WithEnvironment("MIMORIA__CLUSTER__PORT", "6570")
            .WithEnvironment("MIMORIA__CLUSTER__PASSWORD", ClusterPassword)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__ID", "1")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__HOST", firstName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__PORT", "6566")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__ID", "2")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__HOST", secondName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__PORT", "6568")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__TYPE", "async")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__INTERVALMILLISECONDS", ReplicationIntervalMilliseconds.ToString())
            .WithPortBinding(6565, assignRandomHostPort: true)
            .WithPortBinding(6570, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        await Task.WhenAll(
            this.firstInstance.StartAsync(),
            this.secondInstance.StartAsync(),
            this.thirdInstance.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await this.firstInstance.DisposeAsync();
        await this.secondInstance.DisposeAsync();
        await this.thirdInstance.DisposeAsync();
    }

    [Fact]
    public async Task MimoriaClusterTests_Given_AsyncReplication_When_SetString_And_GetString_Then_DataIsReplicatedAsynchronously()
    {
        // Arrange
        const string key = "key";
        const string value = "value";

        var clusterMimoriaClient = new ClusterMimoriaClient(Password, [
            new IPEndPoint(IPAddress.Loopback, this.firstInstance.GetMappedPublicPort(6565)),
            new IPEndPoint(IPAddress.Loopback, this.secondInstance.GetMappedPublicPort(6565)),
            new IPEndPoint(IPAddress.Loopback, this.thirdInstance.GetMappedPublicPort(6565))
        ]);
        await clusterMimoriaClient.ConnectAsync();

        // Act
        await clusterMimoriaClient.SetStringAsync(key, value);

        string? firstValue = await clusterMimoriaClient.GetStringAsync(key, preferSecondary: true);

        await Task.Delay(ReplicationIntervalMilliseconds + 1000);

        string? secondValue = await clusterMimoriaClient.GetStringAsync(key, preferSecondary: true);

        // Assert
        Assert.Null(firstValue);
        Assert.Equal(value, secondValue);
    }
}
