// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Varelen.Mimoria.Server;

namespace Varelen.Mimoria.Service;

public sealed class MimoriaHostedService : IHostedService
{
    private readonly ILogger<MimoriaHostedService> logger;
    private readonly IMimoriaServer mimoriaServer;

    public MimoriaHostedService(ILogger<MimoriaHostedService> logger, IMimoriaServer mimoriaServer)
    {
        this.logger = logger;
        this.mimoriaServer = mimoriaServer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = StartAsyncCore();
        return Task.CompletedTask;
    }

    public async Task StartAsyncCore()
    {
        try
        {
            await this.mimoriaServer.StartAsync();
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Unexpected error while starting Mimoria server");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.mimoriaServer.Stop();
        return Task.CompletedTask;
    }
}
