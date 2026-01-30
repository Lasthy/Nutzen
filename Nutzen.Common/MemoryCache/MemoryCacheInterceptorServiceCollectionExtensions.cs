using Microsoft.Extensions.DependencyInjection;
using System;

namespace Nutzen.Common.MemoryCache;

/// <summary>
/// Extension methods for registering cache interceptor services.
/// </summary>
public static class MemoryCacheInterceptorServiceCollectionExtensions
{
    /// <summary>
    /// Adds the memory cache interceptor services to the service collection.
    /// This includes the cache management service and the cleanup hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure cache options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMemoryCacheInterceptor(
        this IServiceCollection services,
        Action<MemoryCacheInterceptorOptions>? configureOptions = null)
    {
        // Register options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<MemoryCacheInterceptorOptions>(_ => { });
        }

        // Register memory cache if not already registered
        services.AddMemoryCache();

        // Register cache management service as singleton to maintain metadata across requests
        services.AddSingleton<ICacheManagementService, CacheManagementService>();

        // Register the cleanup hosted service
        services.AddHostedService<CacheCleanupHostedService>();

        return services;
    }
}
