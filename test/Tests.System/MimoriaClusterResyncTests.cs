// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Client;

namespace Varelen.Mimoria.Tests.System;

public class MimoriaClusterResyncTests : IAsyncLifetime
{
    private const string Password = "cool";
    private const string ClusterPassword = "coolcluster";

    private IContainer firstInstance = null!;
    private IContainer secondInstance = null!;
    private IContainer thirdInstance = null!;

    private IFutureDockerImage image = null!;

    private readonly INetwork network;

    private string firstName;
    private readonly string secondName;
    private readonly string thirdName;

    private readonly ushort portOne;
    private readonly ushort portTwo;
    private readonly ushort portThree;

    public MimoriaClusterResyncTests()
    {
        (this.portOne, this.portTwo, this.portThree) = GetFreePorts();

        this.firstName = Guid.NewGuid().ToString();
        this.secondName = Guid.NewGuid().ToString();
        this.thirdName = Guid.NewGuid().ToString();

        this.network = new NetworkBuilder()
            .Build();
    }

    public async Task InitializeAsync()
    {
        this.image = await MimoriaDockerImage.GetOrCreateImageAsync();

        this.firstInstance = new ContainerBuilder()
            .WithName(this.firstName)
            .WithNetwork(this.network)
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithEnvironment("MIMORIA__CLUSTER__ID", "1")
            .WithEnvironment("MIMORIA__CLUSTER__IP", "0.0.0.0")
            .WithEnvironment("MIMORIA__CLUSTER__PORT", "6566")
            .WithEnvironment("MIMORIA__CLUSTER__PASSWORD", ClusterPassword)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__ID", "2")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__HOST", this.secondName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__PORT", "6568")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__ID", "3")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__HOST", this.thirdName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__PORT", "6570")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__TYPE", "sync")
            .WithPortBinding(this.portOne, 6565)
            .WithPortBinding(6566, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        this.secondInstance = new ContainerBuilder()
            .WithName(this.secondName)
            .WithNetwork(this.network)
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithEnvironment("MIMORIA__CLUSTER__ID", "2")
            .WithEnvironment("MIMORIA__CLUSTER__IP", "0.0.0.0")
            .WithEnvironment("MIMORIA__CLUSTER__PORT", "6568")
            .WithEnvironment("MIMORIA__CLUSTER__PASSWORD", ClusterPassword)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__ID", "1")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__HOST", this.firstName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__PORT", "6566")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__ID", "3")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__HOST", this.thirdName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__PORT", "6570")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__TYPE", "sync")
            .WithPortBinding(this.portTwo, 6565)
            .WithPortBinding(6568, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        this.thirdInstance = new ContainerBuilder()
            .WithName(this.thirdName)
            .WithNetwork(this.network)
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithEnvironment("MIMORIA__CLUSTER__ID", "3")
            .WithEnvironment("MIMORIA__CLUSTER__IP", "0.0.0.0")
            .WithEnvironment("MIMORIA__CLUSTER__PORT", "6570")
            .WithEnvironment("MIMORIA__CLUSTER__PASSWORD", ClusterPassword)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__ID", "1")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__HOST", this.firstName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__PORT", "6566")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__ID", "2")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__HOST", this.secondName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__PORT", "6568")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__TYPE", "sync")
            .WithPortBinding(this.portThree, 6565)
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
    public async Task MimoriaClusterTests_Given_Resync_When_SetString_And_GetString_AfterSecondaryNodesWentDownAndUp_Then_DataIsResynced()
    {
        // Arrange
        var clusterMimoriaClient = new ClusterMimoriaClient(Password, [
            new IPEndPoint(IPAddress.Loopback, this.portOne),
            new IPEndPoint(IPAddress.Loopback, this.portTwo),
            new IPEndPoint(IPAddress.Loopback, this.portThree)
        ]);
        await clusterMimoriaClient.ConnectAsync();

        // Act
        await clusterMimoriaClient.SetStringAsync("key", "value");

        await this.firstInstance.StopAsync();

        this.firstName = Guid.NewGuid().ToString();

        this.firstInstance = new ContainerBuilder()
            .WithName(this.firstName)
            .WithNetwork(this.network)
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithEnvironment("MIMORIA__CLUSTER__ID", "1")
            .WithEnvironment("MIMORIA__CLUSTER__IP", "0.0.0.0")
            .WithEnvironment("MIMORIA__CLUSTER__PORT", "6566")
            .WithEnvironment("MIMORIA__CLUSTER__PASSWORD", ClusterPassword)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__ID", "2")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__HOST", this.secondName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__0__PORT", "6568")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__ID", "3")
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__HOST", this.thirdName)
            .WithEnvironment("MIMORIA__CLUSTER__NODES__1__PORT", "6570")
            .WithEnvironment("MIMORIA__CLUSTER__REPLICATION__TYPE", "sync")
            .WithPortBinding(this.portOne, 6565)
            .WithPortBinding(6566, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        await this.firstInstance.StartAsync();

        // Wait for reconnect
        await Task.Delay(6000);

        string? value = await clusterMimoriaClient.GetStringAsync("key", preferSecondary: true);

        // Assert
        Assert.Equal("value", value);
    }

    private static (ushort Port, ushort PortTwo, ushort PortThree) GetFreePorts()
    {
        using var tcpListenerOne = new TcpListener(IPAddress.Loopback, port: 0);
        tcpListenerOne.Start();
        int port = ((IPEndPoint)tcpListenerOne.LocalEndpoint).Port;

        using var tcpListenerTwo = new TcpListener(IPAddress.Loopback, port: 0);
        tcpListenerTwo.Start();
        int portTwo = ((IPEndPoint)tcpListenerTwo.LocalEndpoint).Port;

        using var tcpListenerThree = new TcpListener(IPAddress.Loopback, port: 0);
        tcpListenerThree.Start();
        int portThree = ((IPEndPoint)tcpListenerThree.LocalEndpoint).Port;

        return ((ushort)port, (ushort)portTwo, (ushort)portThree);
    }
}
