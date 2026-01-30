using System;

namespace Nutzen.Common.MemoryCache;

/// <summary>
/// Metadata about a cached entry, including when it was cached and its expiration time.
/// </summary>
public sealed class CacheEntryMetadata
{
    /// <summary>
    /// The unique key for this cache entry.
    /// </summary>
    public string Key { get; init; } = null!;

    /// <summary>
    /// The type name of the request that generated this cache entry.
    /// </summary>
    public string RequestTypeName { get; init; } = null!;

    /// <summary>
    /// The UTC time when this entry was cached.
    /// </summary>
    public DateTimeOffset CachedAt { get; init; }

    /// <summary>
    /// The UTC time when this entry will expire (absolute expiration).
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; init; }

    /// <summary>
    /// The sliding expiration duration for this entry.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }

    /// <summary>
    /// The last time this entry was accessed.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; }
}
