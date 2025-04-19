// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.DependencyInjection;

using Varelen.Mimoria.Client.Protocol;

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
            var mimoriaClient = new MimoriaClient(configuration.Host, configuration.Port, configuration.Password, new MimoriaSocketClient(configuration.OperationTimeout, configuration.OperationRetryPolicy), configuration.ConnectRetryPolicy);
            return new LazyConnectingMimoriaClient(mimoriaClient);
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
            var mimoriaClient = new MimoriaClient(configuration.Host, configuration.Port, configuration.Password, new MimoriaSocketClient(configuration.OperationTimeout, configuration.OperationRetryPolicy), configuration.ConnectRetryPolicy);
            var microCacheMimoriaClient = new MicrocacheMimoriaClient(mimoriaClient, expiration);
            
            return new LazyConnectingMimoriaClient(mimoriaClient);
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
            var sharedMimoriaClient = new ShardedMimoriaClient(configuration.Password, configuration.Endpoints.ToArray());
            return new LazyConnectingShardedMimoriaClient(sharedMimoriaClient);
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
            var sharedMimoriaClient = new ShardedMimoriaClient(configuration.Password, configuration.Endpoints.ToArray());
            var microCacheMimoriaClient = new MicrocacheMimoriaClient(sharedMimoriaClient);
            
            return new LazyConnectingShardedMimoriaClient(microCacheMimoriaClient);
        });

        return services;
    }

    /// <summary>
    /// Adds the cluster Mimoria client services to the specified <see cref="IServiceCollection"/> with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configure">An action to configure the <see cref="ClusterMimoriaConfiguration"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddClusterMimoria(this IServiceCollection services, Action<ClusterMimoriaConfiguration> configure)
    {
        var clusterMimoriaConfiguration = new ClusterMimoriaConfiguration();

        configure(clusterMimoriaConfiguration);

        return services.AddClusterMimoria(clusterMimoriaConfiguration);
    }

    /// <summary>
    /// Adds the cluster Mimoria client services to the specified <see cref="IServiceCollection"/> with the specified configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The <see cref="ClusterMimoriaConfiguration"/> to use for configuring the client.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddClusterMimoria(this IServiceCollection services, ClusterMimoriaConfiguration configuration)
    {
        services.AddSingleton<IClusterMimoriaClient>(_ =>
        {
            var clusterMimoriaClient = new ClusterMimoriaClient(
                configuration.Password,
                configuration.RetryCount,
                configuration.RetryDelay,
                configuration.Endpoints.ToArray());

            return new LazyConnectingClusterMimoriaClient(clusterMimoriaClient);
        });

        return services;
    }
}
