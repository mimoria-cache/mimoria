// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Varelen.Mimoria.Server;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;
using Varelen.Mimoria.Service;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        services.AddOptions<MimoriaOptions>()
            .Bind(configuration.GetSection("Mimoria"))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<MimoriaOptions>, MimoriaOptionsValidation>();

        services.AddSingleton<IMimoriaSocketServer, MimoriaSocketServer>();
        services.AddSingleton<IPubSubService, PubSubService>();
#if DEBUG
        services.AddSingleton<ICache>(serviceProvider => new LoggingCache(serviceProvider.GetRequiredService<ILogger<LoggingCache>>(), new ExpiringDictionaryCache(serviceProvider.GetRequiredService<ILogger<ExpiringDictionaryCache>>(), serviceProvider.GetRequiredService<IPubSubService>(), TimeSpan.FromMinutes(5))));
#else
        services.AddSingleton<ICache>(serviceProvider => new ExpiringDictionaryCache(serviceProvider.GetRequiredService<ILogger<ExpiringDictionaryCache>>(), serviceProvider.GetRequiredService<IPubSubService>(), TimeSpan.FromMinutes(5)));
#endif
        services.AddSingleton<IMimoriaServer, MimoriaServer>();

        services.AddHostedService<MimoriaHostedService>();
    })
    .RunConsoleAsync();
