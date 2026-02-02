using Microsoft.Extensions.Logging;
using Nutzen;
using System;
using System.Threading.Tasks;

namespace Nutzen.Common.MemoryCache;

/// <summary>
/// An interceptor that caches responses for identical requests using IMemoryCache.
/// Apply the generated [MemoryCacheInterceptor] attribute to request handlers to use this interceptor.
/// </summary>
/// <remarks>
/// Requests are considered equal based on their JSON serialization.
/// The interceptor tracks metadata about cached entries, including when they were cached.
/// </remarks>
[Interceptor(Order = -100)] // Run early to potentially skip expensive operations
public class MemoryCacheInterceptor<TRequest, TResponse> : IRequestInterceptor<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheManagementService _cacheService;
    private readonly ILogger<MemoryCacheInterceptor<TRequest, TResponse>> _logger;

    public MemoryCacheInterceptor(
        ICacheManagementService cacheService,
        ILogger<MemoryCacheInterceptor<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts the request and returns a cached response if available,
    /// otherwise executes the handler and caches successful responses.
    /// </summary>
    public async Task<Result<TResponse>> Intercept(
        TRequest request,
        Func<TRequest, Task<Result<TResponse>>> next)
    {
        var requestType = typeof(TRequest);
        var cacheKey = _cacheService.GenerateCacheKey(request);
        var requestName = requestType.Name;
        var requestFullName = $"{(requestType.BaseType != null ? $"{requestType.BaseType.Name}." : "")}{requestName}";
        var requestId = request.Id;

        // Try to get from cache
        if (_cacheService.TryGetValue<Result<TResponse>>(cacheKey, out var cachedResult, out var metadata))
        {
            _logger.LogInformation(
                "[{RequestId}] Cache HIT for request '{RequestName}'. Cached at: {CachedAt}, Last accessed: {LastAccessed}",
                requestId, requestFullName, metadata!.CachedAt, metadata.LastAccessedAt);

            return cachedResult!;
        }

        _logger.LogInformation("[{RequestId}] Cache MISS for request '{RequestName}'", requestId, requestFullName);

        // Execute the handler
        var result = await next(request);

        // Only cache successful results
        if (result.IsSuccess)
        {
            _cacheService.Set(cacheKey, result, typeof(TRequest).FullName ?? requestName);

            _logger.LogInformation("[{RequestId}] Cached successful response for request '{RequestName}'",
                requestId, requestFullName);
        }

        return result;
    }
}
