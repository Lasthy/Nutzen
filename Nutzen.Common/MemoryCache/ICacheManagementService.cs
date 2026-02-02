using System;
using System.Collections.Generic;

namespace Nutzen.Common.MemoryCache;

/// <summary>
/// Interface for the cache management service.
/// </summary>
public interface ICacheManagementService
{
    /// <summary>
    /// Tries to get a cached value and its metadata.
    /// </summary>
    bool TryGetValue<TResponse>(string key, out TResponse? value, out CacheEntryMetadata? metadata);

    /// <summary>
    /// Sets a value in the cache with the specified options.
    /// </summary>
    void Set<TResponse>(string key, TResponse value, string requestTypeName, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null);

    /// <summary>
    /// Invalidates (removes) a specific cache entry.
    /// </summary>
    bool Invalidate(string key);

    /// <summary>
    /// Invalidates all cache entries for a specific request type.
    /// </summary>
    int InvalidateByRequestType(string requestTypeName);

    /// <summary>
    /// Invalidates all cache entries.
    /// </summary>
    int InvalidateAll();

    /// <summary>
    /// Gets all cache entry metadata.
    /// </summary>
    IReadOnlyCollection<CacheEntryMetadata> GetAllMetadata();

    /// <summary>
    /// Removes entries older than the specified age based on when they were cached.
    /// </summary>
    int RemoveEntriesOlderThan(TimeSpan maxAge);

    /// <summary>
    /// Removes entries that have passed their absolute expiration time.
    /// </summary>
    int RemoveExpiredEntries();

    /// <summary>
    /// Generates a cache key for the given request.
    /// </summary>
    string GenerateCacheKey<TRequest>(TRequest request);
}
