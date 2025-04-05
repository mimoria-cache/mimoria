// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Containers;

namespace Varelen.Mimoria.Tests.System;

public sealed class MimoriaContainerFixture : IAsyncLifetime
{
    public const string Ip = "127.0.0.1";
    public const string Password = "tests";

    private IContainer container = null!;

    public IContainer Container => this.container;

    public async Task InitializeAsync()
    {
        IFutureDockerImage image = await MimoriaDockerImage.GetOrCreateImageAsync();

        this.container = new ContainerBuilder()
            .WithImage(image)
            .WithEnvironment("MIMORIA__PASSWORD", Password)
            .WithPortBinding(6565, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6565))
            .Build();

        await this.container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await this.container.DisposeAsync();
    }
}
