// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Azure.Monitor.OpenTelemetry.Exporter;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Varelen.Mimoria.Server;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Metrics;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;
using Varelen.Mimoria.Service;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

MeterProvider? meterProvider = null;

string? applicationInsightsConnectionString = configuration["ConnectionStrings:ApplicationInsights"];
if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
{
    meterProvider = Sdk.CreateMeterProviderBuilder()
        .AddMeter(MimoriaMetrics.MeterName)
        .AddAzureMonitorMetricExporter(options =>
        {
            options.ConnectionString = applicationInsightsConnectionString;
        })
        .Build();
}

try
{
    await Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services.AddOptions<MimoriaOptions>()
                .Bind(configuration.GetSection(MimoriaOptions.SectionName))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<MimoriaOptions>, MimoriaOptionsValidation>();

            services.AddSingleton<IMimoriaMetrics, MimoriaMetrics>();

            services.AddSingleton<IMimoriaSocketServer, MimoriaSocketServer>();
            services.AddSingleton<IPubSubService, PubSubService>();
#if DEBUG
            services.AddSingleton<ICache>(serviceProvider =>
            {
                LogMetricsEnabled(serviceProvider, enabled: meterProvider != null);

                var mimoriaOptions = serviceProvider.GetRequiredService<IOptions<MimoriaOptions>>();
                var expiringDictionaryCache = new ExpiringDictionaryCache(
                    serviceProvider.GetRequiredService<ILogger<ExpiringDictionaryCache>>(),
                    serviceProvider.GetRequiredService<IMimoriaMetrics>(),
                    serviceProvider.GetRequiredService<IPubSubService>(),
                    mimoriaOptions.Value.Cache.ExpirationCheckInterval);
                return new LoggingCache(serviceProvider.GetRequiredService<ILogger<LoggingCache>>(), expiringDictionaryCache);
            });
#else
            services.AddSingleton<ICache>(serviceProvider =>
            {
                LogMetricsEnabled(serviceProvider, enabled: meterProvider != null);

                var mimoriaOptions = serviceProvider.GetRequiredService<IOptions<MimoriaOptions>>();
                return new ExpiringDictionaryCache(
                    serviceProvider.GetRequiredService<ILogger<ExpiringDictionaryCache>>(),
                    serviceProvider.GetRequiredService<IMimoriaMetrics>(),
                    serviceProvider.GetRequiredService<IPubSubService>(),
                    mimoriaOptions.Value.Cache.ExpirationCheckInterval);
            });
#endif
            services.AddSingleton<IMimoriaServer, MimoriaServer>();

            services.AddHostedService<MimoriaHostedService>();
        })
        .RunConsoleAsync();
}
finally
{
    meterProvider?.Dispose();
}

static void LogMetricsEnabled(IServiceProvider serviceProvider, bool enabled)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Azure Application Insights metrics exporting {Enabled}", enabled ? "enabled" : "disabled");
}
