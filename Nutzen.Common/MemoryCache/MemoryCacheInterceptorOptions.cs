using System;

namespace Nutzen.Common.MemoryCache;

/// <summary>
/// Configuration options for the memory cache interceptor.
/// </summary>
public sealed class MemoryCacheInterceptorOptions
{
    /// <summary>
    /// Default absolute expiration time for cached entries.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan DefaultAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default sliding expiration time for cached entries.
    /// If set, the entry will expire if not accessed within this duration.
    /// Default is null (no sliding expiration).
    /// </summary>
    public TimeSpan? DefaultSlidingExpiration { get; set; }

    /// <summary>
    /// Interval at which the cleanup service runs to remove expired entries.
    /// Default is 1 minute.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum age of cache entries before they are considered stale and eligible for cleanup.
    /// Default is 30 minutes.
    /// </summary>
    public TimeSpan MaxCacheAge { get; set; } = TimeSpan.FromMinutes(30);
}
