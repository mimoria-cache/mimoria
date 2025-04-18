// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

using NSubstitute;

using System.Net;
using System.Net.Sockets;

using Varelen.Mimoria.Client;
using Varelen.Mimoria.Server;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Metrics;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerClusterTests : IAsyncLifetime
{
    private const string Password = "password";
    private const string ClusterPassword = "clusterpassword";

    private static readonly string Ip = IPAddress.Loopback.ToString();

    private readonly ILoggerFactory loggerFactory;
    private readonly IMimoriaMetrics metrics;
    private readonly IPubSubService pubSubServiceOne;
    private readonly IPubSubService pubSubServiceTwo;

    private ExpiringDictionaryCache cacheOne;
    private ExpiringDictionaryCache cacheTwo;
    private MimoriaServer mimoriaServerOne = null!;
    private MimoriaServer mimoriaServerTwo = null!;
    private ushort firstPort;
    private ushort firstClusterPort;
    private ushort secondPort;
    private ushort secondClusterPort;

    public MimoriaServerClusterTests()
    {
        var optionsMock = Substitute.For<IOptionsMonitor<ConsoleLoggerOptions>>();
        optionsMock.CurrentValue.Returns(new ConsoleLoggerOptions());

        this.loggerFactory = Substitute.For<ILoggerFactory>();
        this.metrics = Substitute.For<IMimoriaMetrics>();
        this.cacheOne = new ExpiringDictionaryCache(NullLogger<ExpiringDictionaryCache>.Instance, this.metrics, Substitute.For<IPubSubService>(), TimeSpan.FromMinutes(5));
        this.cacheTwo = new ExpiringDictionaryCache(NullLogger<ExpiringDictionaryCache>.Instance, this.metrics, Substitute.For<IPubSubService>(), TimeSpan.FromMinutes(5));
        this.pubSubServiceOne = new PubSubService(NullLogger<PubSubService>.Instance, this.metrics);
        this.pubSubServiceTwo = new PubSubService(NullLogger<PubSubService>.Instance, this.metrics);
    }

    public async Task InitializeAsync()
    {
        (this.mimoriaServerOne, this.mimoriaServerTwo, firstPort, firstClusterPort, secondPort, secondClusterPort) = await this.CreateClusterAsync();
    }

    public Task DisposeAsync()
    {
        this.mimoriaServerOne.Stop();
        this.mimoriaServerTwo.Stop();
        this.pubSubServiceOne.Dispose();
        this.pubSubServiceTwo.Dispose();
        this.cacheOne.Dispose();
        this.cacheTwo.Dispose();
        return Task.CompletedTask;
    }

    private static (ushort Port, ushort ClusterPort) GetFreePorts()
    {
        using var tcpListenerOne = new TcpListener(IPAddress.Loopback, port: 0);
        tcpListenerOne.Start();
        int port = ((IPEndPoint)tcpListenerOne.LocalEndpoint).Port;

        using var tcpListenerTwo = new TcpListener(IPAddress.Loopback, port: 0);
        tcpListenerTwo.Start();
        int clusterPort = ((IPEndPoint)tcpListenerTwo.LocalEndpoint).Port;

        return ((ushort)port, (ushort)clusterPort);
    }

    protected async Task<IClusterMimoriaClient> ConnectToClusterAsync()
    {
        var mimoriaClient = new ClusterMimoriaClient(Password, [new ServerEndpoint(Ip, this.firstPort), new ServerEndpoint(Ip, this.secondPort)]);
        await mimoriaClient.ConnectAsync();
        return mimoriaClient;
    }

    private async Task<(MimoriaServer Secondary, MimoriaServer Primary, ushort PortOne, ushort ClusterPortOne, ushort PortTwo, ushort ClusterPortTwo)> CreateClusterAsync()
    {
        var (portOne, clusterPortOne) = GetFreePorts();
        var (portTwo, clusterPortTwo) = GetFreePorts();

        var optionsMock = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock.CurrentValue.Returns(new MimoriaOptions() { Password = Password, Port = portOne, Cluster = new MimoriaOptions.ClusterOptions() { Id = 1, Port = clusterPortOne, Password = ClusterPassword, Nodes = [new() { Id = 2, Host = "127.0.0.1", Port = clusterPortTwo }] } });

        var mimoriaServerOne = new MimoriaServer(NullLogger<MimoriaServer>.Instance, this.loggerFactory, optionsMock, this.pubSubServiceOne, new MimoriaSocketServer(NullLogger<MimoriaSocketServer>.Instance, this.metrics), this.cacheOne, this.metrics);

        var optionsMock2 = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock2.CurrentValue.Returns(new MimoriaOptions() { Password = Password, Port = portTwo, Cluster = new MimoriaOptions.ClusterOptions() { Id = 2, Port = clusterPortTwo, Password = ClusterPassword, Nodes = [new() { Id = 1, Host = "127.0.0.1", Port = clusterPortOne }] } });

        var mimoriaServerTwo = new MimoriaServer(NullLogger<MimoriaServer>.Instance, this.loggerFactory, optionsMock2, this.pubSubServiceTwo, new MimoriaSocketServer(NullLogger<MimoriaSocketServer>.Instance, this.metrics), this.cacheTwo, this.metrics);

        await Task.WhenAll(mimoriaServerOne.StartAsync(), mimoriaServerTwo.StartAsync());

        return (mimoriaServerOne, mimoriaServerTwo, portOne, clusterPortOne, portTwo, clusterPortTwo);
    }
}
