using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nutzen.Common.MemoryCache;

/// <summary>
/// A hosted service that periodically cleans up old cache entries.
/// This service runs in the background and removes entries that have exceeded
/// the maximum cache age configured in <see cref="MemoryCacheInterceptorOptions"/>.
/// </summary>
public sealed class CacheCleanupHostedService : BackgroundService
{
    private readonly ICacheManagementService _cacheService;
    private readonly MemoryCacheInterceptorOptions _options;
    private readonly ILogger<CacheCleanupHostedService> _logger;

    public CacheCleanupHostedService(
        ICacheManagementService cacheService,
        IOptions<MemoryCacheInterceptorOptions> options,
        ILogger<CacheCleanupHostedService> logger)
    {
        _cacheService = cacheService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Cache cleanup service started. Cleanup interval: {Interval}, Max cache age: {MaxAge}",
            _options.CleanupInterval, _options.MaxCacheAge);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                var removedCount = _cacheService.RemoveEntriesOlderThan(_options.MaxCacheAge);

                if (removedCount > 0)
                {
                    _logger.LogInformation("Cache cleanup completed. Removed {Count} stale entries.", removedCount);
                }
                else
                {
                    _logger.LogDebug("Cache cleanup completed. No stale entries found.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during cache cleanup");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("Cache cleanup service stopped.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cache cleanup service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
