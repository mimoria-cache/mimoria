// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using System.Net.Sockets;
using System.Net;

using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;
using Varelen.Mimoria.Server;
using Varelen.Mimoria.Client;
using Varelen.Mimoria.Server.Metrics;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    private const string Ip = "127.0.0.1";
    private const string Password = "password";

    private readonly ExpiringDictionaryCache cache;
    private readonly IMimoriaMetrics metrics;
    private readonly IPubSubService pubSubService;
    private MimoriaServer mimoriaServerOne = null!;
    private ushort port;

    public MimoriaServerTests()
    {
        this.metrics = Substitute.For<IMimoriaMetrics>();
        this.pubSubService = new PubSubService(NullLogger<PubSubService>.Instance, this.metrics);
        this.cache = new ExpiringDictionaryCache(NullLogger<ExpiringDictionaryCache>.Instance, this.metrics, this.pubSubService, TimeSpan.FromMinutes(5));
    }

    public async Task InitializeAsync()
    {
        (this.mimoriaServerOne, port) = await this.CreateAndStartServerAsync();
    }

    public async Task DisposeAsync()
    {
        await this.mimoriaServerOne.StopAsync();
        this.cache.Dispose();
    }

    private static ushort GetFreePort()
    {
        using var tcpListenerOne = new TcpListener(IPAddress.Parse(Ip), port: 0);
        tcpListenerOne.Start();
        return (ushort)((IPEndPoint)tcpListenerOne.LocalEndpoint).Port;
    }

    private async Task<MimoriaClient> ConnectToServerAsync()
    {
        var mimoriaClient = new MimoriaClient(Ip, this.port, Password);
        await mimoriaClient.ConnectAsync();
        return mimoriaClient;
    }

    private async Task<(MimoriaServer MimoriaServer, ushort Port)> CreateAndStartServerAsync()
    {
        var port = GetFreePort();

        var optionsMock = Substitute.For<IOptionsMonitor<MimoriaOptions>>();
        optionsMock.CurrentValue.Returns(new MimoriaOptions() { Ip = Ip, Password = Password, Port = port });

        var mimoriaServerOne = new MimoriaServer(NullLogger<MimoriaServer>.Instance, new NullLoggerFactory(), optionsMock, this.pubSubService, new MimoriaSocketServer(NullLogger<MimoriaSocketServer>.Instance, this.metrics), this.cache, this.metrics);

        await mimoriaServerOne.StartAsync();

        return (mimoriaServerOne, port);
    }
}
