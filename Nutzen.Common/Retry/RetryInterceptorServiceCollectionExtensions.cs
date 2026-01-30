using Microsoft.Extensions.DependencyInjection;
using System;

namespace Nutzen.Common.Retry;

/// <summary>
/// Extension methods for configuring the retry interceptor.
/// </summary>
public static class RetryInterceptorServiceCollectionExtensions
{
    /// <summary>
    /// Adds the retry interceptor with default options.
    /// </summary>
    public static IServiceCollection AddRetryInterceptor(
        this IServiceCollection services)
    {
        return services.AddRetryInterceptor(_ => { });
    }

    /// <summary>
    /// Adds the retry interceptor with the specified options.
    /// </summary>
    public static IServiceCollection AddRetryInterceptor(
        this IServiceCollection services,
        Action<RetryInterceptorOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services;
    }
}
