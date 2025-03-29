// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;

namespace Varelen.Mimoria.Client.DependencyInjection;

/// <summary>
/// Provides extension methods for adding Mimoria services to the dependency injection container.
/// </summary>
public static class MimoriaServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Mimoria client services to the specified <see cref="IServiceCollection"/> with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configure">An action to configure the <see cref="MimoriaConfiguration"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMimoria(this IServiceCollection services, Action<MimoriaConfiguration> configure)
    {
        var mimoriaConfiguration = new MimoriaConfiguration();

        configure(mimoriaConfiguration);

        return services.AddMimoria(mimoriaConfiguration);
    }

    /// <summary>
    /// Adds the Mimoria client services to the specified <see cref="IServiceCollection"/> with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The <see cref="MimoriaConfiguration"/> to use for configuring the client.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
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

    /// <summary>
    /// Adds the Mimoria client services to the specified <see cref="IServiceCollection"/> with the default configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>

    public static IServiceCollection AddMimoria(this IServiceCollection services)
        => services.AddMimoria(new MimoriaConfiguration());

    /// <summary>
    /// Adds the Mimoria client services with micro-cache to the specified <see cref="IServiceCollection"/> with the specified configuration and expiration time.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configure">An action to configure the <see cref="MimoriaConfiguration"/>.</param>
    /// <param name="expiration">The expiration time for the micro-cache.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMimoriaWithMicroCache(this IServiceCollection services, Action<MimoriaConfiguration> configure, TimeSpan expiration)
    {
        var mimoriaConfiguration = new MimoriaConfiguration();

        configure(mimoriaConfiguration);

        return services.AddMimoriaWithMicroCache(mimoriaConfiguration, expiration);
    }

    /// <summary>
    /// Adds the Mimoria client services with micro-cache to the specified <see cref="IServiceCollection"/> with the specified configuration and expiration time.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The <see cref="MimoriaConfiguration"/> to use for configuring the client.</param>
    /// <param name="expiration">The expiration time for the micro-cache.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
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

    /// <summary>
    /// Adds the sharded Mimoria client services to the specified <see cref="IServiceCollection"/> with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configure">An action to configure the <see cref="ShardedMimoriaConfiguration"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddShardedMimoria(this IServiceCollection services, Action<ShardedMimoriaConfiguration> configure)
    {
        var shardedMimoriaConfiguration = new ShardedMimoriaConfiguration();

        configure(shardedMimoriaConfiguration);

        return services.AddShardedMimoria(shardedMimoriaConfiguration);
    }

    /// <summary>
    /// Adds the sharded Mimoria client services to the specified <see cref="IServiceCollection"/> with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The <see cref="ShardedMimoriaConfiguration"/> to use for configuring the client.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
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

    /// <summary>
    /// Adds the sharded Mimoria client services with micro-cache to the specified <see cref="IServiceCollection"/> with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The <see cref="ShardedMimoriaConfiguration"/> to use for configuring the client.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
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
