using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nutzen.Common.MemoryCache;

/// <summary>
/// Default implementation of the cache management service.
/// </summary>
public sealed class CacheManagementService : ICacheManagementService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, CacheEntryMetadata> _cacheMetadata;
    private readonly MemoryCacheInterceptorOptions _options;
    private readonly ILogger<CacheManagementService> _logger;

    public CacheManagementService(
        IMemoryCache memoryCache,
        IOptions<MemoryCacheInterceptorOptions> options,
        ILogger<CacheManagementService> logger)
    {
        _memoryCache = memoryCache;
        _cacheMetadata = new ConcurrentDictionary<string, CacheEntryMetadata>();
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool TryGetValue<TResponse>(string key, out TResponse? value, out CacheEntryMetadata? metadata)
    {
        if (_memoryCache.TryGetValue(key, out value) && _cacheMetadata.TryGetValue(key, out metadata))
        {
            // Validate that the entry hasn't expired
            if (metadata.AbsoluteExpiration.HasValue && metadata.AbsoluteExpiration.Value <= DateTimeOffset.UtcNow)
            {
                // Entry has expired, remove it
                _memoryCache.Remove(key);
                _cacheMetadata.TryRemove(key, out _);
                value = default;
                metadata = null;
                return false;
            }

            metadata.LastAccessedAt = DateTimeOffset.UtcNow;
            return true;
        }

        value = default;
        metadata = null;
        return false;
    }

    /// <inheritdoc />
    public void Set<TResponse>(string key, TResponse value, string requestTypeName, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        var now = DateTimeOffset.UtcNow;
        var absExpiration = absoluteExpiration ?? _options.DefaultAbsoluteExpiration;
        var slideExpiration = slidingExpiration ?? _options.DefaultSlidingExpiration;

        var cacheEntryOptions = new MemoryCacheEntryOptions();
        
        if (absExpiration != TimeSpan.Zero)
        {
            cacheEntryOptions.SetAbsoluteExpiration(absExpiration);
        }

        if (slideExpiration.HasValue && slideExpiration.Value != TimeSpan.Zero)
        {
            cacheEntryOptions.SetSlidingExpiration(slideExpiration.Value);
        }

        // Register a callback to remove metadata when the entry is evicted
        cacheEntryOptions.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
        {
            if (evictedKey is string keyStr)
            {
                _cacheMetadata.TryRemove(keyStr, out _);
                _logger.LogDebug("Cache entry '{Key}' was evicted. Reason: {Reason}", keyStr, reason);
            }
        });

        _memoryCache.Set(key, value, cacheEntryOptions);

        var metadata = new CacheEntryMetadata
        {
            Key = key,
            RequestTypeName = requestTypeName,
            CachedAt = now,
            LastAccessedAt = now,
            AbsoluteExpiration = absExpiration != TimeSpan.Zero ? now.Add(absExpiration) : null,
            SlidingExpiration = slideExpiration
        };

        _cacheMetadata[key] = metadata;

        _logger.LogDebug("Cached entry '{Key}' for request type '{RequestType}'. Expires at: {Expiration}",
            key, requestTypeName, metadata.AbsoluteExpiration);
    }

    /// <inheritdoc />
    public bool Invalidate(string key)
    {
        _memoryCache.Remove(key);
        var removed = _cacheMetadata.TryRemove(key, out _);
        
        if (removed)
        {
            _logger.LogInformation("Invalidated cache entry '{Key}'", key);
        }

        return removed;
    }

    /// <inheritdoc />
    public int InvalidateByRequestType(string requestTypeName)
    {
        var keysToRemove = _cacheMetadata
            .Where(kvp => kvp.Value.RequestTypeName == requestTypeName)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheMetadata.TryRemove(key, out _);
        }

        _logger.LogInformation("Invalidated {Count} cache entries for request type '{RequestType}'",
            keysToRemove.Count, requestTypeName);

        return keysToRemove.Count;
    }

    /// <inheritdoc />
    public int InvalidateAll()
    {
        var count = _cacheMetadata.Count;
        var keys = _cacheMetadata.Keys.ToList();

        foreach (var key in keys)
        {
            _memoryCache.Remove(key);
        }

        _cacheMetadata.Clear();

        _logger.LogInformation("Invalidated all {Count} cache entries", count);

        return count;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<CacheEntryMetadata> GetAllMetadata()
    {
        return _cacheMetadata.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public int RemoveEntriesOlderThan(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(maxAge);
        var keysToRemove = _cacheMetadata
            .Where(kvp => kvp.Value.CachedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheMetadata.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogInformation("Cleanup removed {Count} cache entries older than {MaxAge}",
                keysToRemove.Count, maxAge);
        }

        return keysToRemove.Count;
    }

    /// <inheritdoc />
    public int RemoveExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var keysToRemove = _cacheMetadata
            .Where(kvp => kvp.Value.AbsoluteExpiration.HasValue && kvp.Value.AbsoluteExpiration.Value <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheMetadata.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogInformation("Removed {Count} expired cache entries", keysToRemove.Count);
        }

        return keysToRemove.Count;
    }

    /// <inheritdoc />
    public string GenerateCacheKey<TRequest>(TRequest request)
    {
        var requestType = typeof(TRequest).FullName ?? typeof(TRequest).Name;
        var serialized = JsonSerializer.Serialize(request, CacheKeySerializerOptions);
        
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(serialized));
        var hash = Convert.ToBase64String(hashBytes);

        return $"{requestType}:{hash}";
    }

    private static readonly JsonSerializerOptions CacheKeySerializerOptions = CreateCacheKeySerializerOptions();

    private static JsonSerializerOptions CreateCacheKeySerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    static typeInfo =>
                    {
                        if (typeInfo.Kind != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object)
                            return;

                        foreach (var property in typeInfo.Properties)
                        {
                            // Exclude the Id property from cache key generation since each request has a unique Id
                            if (property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                            {
                                property.ShouldSerialize = static (_, _) => false;
                            }
                        }
                    }
                }
            }
        };
        return options;
    }
}
