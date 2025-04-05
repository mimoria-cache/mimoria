// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

using Varelen.Mimoria.Client;

namespace Varelen.Mimoria.Tests.System;

public class MimoriaTests : IAsyncLifetime
{
    private const string Ip = "127.0.0.1";
    private const string Password = "tests";

    private IFutureDockerImage image = null!;
    private IContainer container = null!;

    public async Task InitializeAsync()
    {
        ConsoleLogger.Instance.DebugLogLevelEnabled = true;

        using IOutputConsumer outputConsumer = Consume.RedirectStdoutAndStderrToConsole();

        this.image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), string.Empty)
            .WithDockerfile("src/Service/Dockerfile")
            .Build();

        await this.image.CreateAsync();

        this.container = new ContainerBuilder()
            .WithOutputConsumer(outputConsumer)
            .WithImage(this.image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithPortBinding(6565, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        await this.container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await this.container.DisposeAsync();
        await this.image.DeleteAsync();
        await this.image.DisposeAsync();
    }

    [Fact]
    public async Task MimoriaTest_SetString_And_GetString()
    {
        // Arrange
        var mimoriaClient = new MimoriaClient(Ip, this.container.GetMappedPublicPort(6565), Password);
        await mimoriaClient.ConnectAsync();

        // Act
        await mimoriaClient.SetStringAsync("key", "value");
        string? value = await mimoriaClient.GetStringAsync("key");

        // Assert
        Assert.Equal("value", value);
    }
}
