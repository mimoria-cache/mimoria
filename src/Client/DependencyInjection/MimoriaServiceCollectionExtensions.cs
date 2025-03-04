// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;

namespace Varelen.Mimoria.Client.DependencyInjection;

public static class MimoriaServiceCollectionExtensions
{
    public static IServiceCollection AddMimoria(this IServiceCollection services, Action<MimoriaConfiguration> configure)
    {
        var mimoriaConfiguration = new MimoriaConfiguration();

        configure(mimoriaConfiguration);

        return services.AddMimoria(mimoriaConfiguration);
    }

    public static IServiceCollection AddMimoria(this IServiceCollection services, MimoriaConfiguration configuration)
    {
        services.AddSingleton<IMimoriaClient>(_ =>
        {
            var mimoriaClient = new MimoriaClient(configuration.Ip, configuration.Port, configuration.Password);
            // TODO: Is this the correct way or should each method in the client check and establish the connection lazy if needed?
            mimoriaClient.ConnectAsync().GetAwaiter().GetResult();
            return mimoriaClient;
        });

        return services;
    }

    public static IServiceCollection AddMimoria(this IServiceCollection services)
        => services.AddMimoria(new MimoriaConfiguration());

    public static IServiceCollection AddMimoriaWithMicroCache(this IServiceCollection services, Action<MimoriaConfiguration> configure, TimeSpan expiration)
    {
        var mimoriaConfiguration = new MimoriaConfiguration();

        configure(mimoriaConfiguration);

        return services.AddMimoriaWithMicroCache(mimoriaConfiguration, expiration);
    }

    public static IServiceCollection AddMimoriaWithMicroCache(this IServiceCollection services, MimoriaConfiguration configuration, TimeSpan expiration)
    {
        services.AddSingleton<IMimoriaClient>(_ =>
        {
            var mimoriaClient = new MimoriaClient(configuration.Ip, configuration.Port, configuration.Password);
            var microCacheMimoriaClient = new MicrocacheMimoriaClient(mimoriaClient, expiration);
            // TODO: Is this the correct way or should each method in the client check and establish the connection lazy if needed?
            microCacheMimoriaClient.ConnectAsync().GetAwaiter().GetResult();
            return microCacheMimoriaClient;
        });

        return services;
    }

    public static IServiceCollection AddShardedMimoria(this IServiceCollection services, Action<ShardedMimoriaConfiguration> configure)
    {
        var shardedMimoriaConfiguration = new ShardedMimoriaConfiguration();

        configure(shardedMimoriaConfiguration);

        return services.AddShardedMimoria(shardedMimoriaConfiguration);
    }

    public static IServiceCollection AddShardedMimoria(this IServiceCollection services, ShardedMimoriaConfiguration configuration)
    {
        services.AddSingleton<IShardedMimoriaClient>(_ =>
        {
            var sharedMimoriaClient = new ShardedMimoriaClient(configuration.Password, configuration.IPEndPoints.ToArray());
            // TODO: Is this the correct way or should each method in the client check and establish the connection lazy if needed?
            sharedMimoriaClient.ConnectAsync().GetAwaiter().GetResult();
            return sharedMimoriaClient;
        });

        return services;
    }

    public static IServiceCollection AddShardedMimoriaWithMicroCache(this IServiceCollection services, ShardedMimoriaConfiguration configuration)
    {
        services.AddSingleton<IShardedMimoriaClient>(_ =>
        {
            var sharedMimoriaClient = new ShardedMimoriaClient(configuration.Password, configuration.IPEndPoints.ToArray());
            var microCacheMimoriaClient = new MicrocacheMimoriaClient(sharedMimoriaClient);
            // TODO: Is this the correct way or should each method in the client check and establish the connection lazy if needed?
            microCacheMimoriaClient.ConnectAsync().GetAwaiter().GetResult();
            return microCacheMimoriaClient;
        });

        return services;
    }
}
