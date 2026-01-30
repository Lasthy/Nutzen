using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nutzen;
using System;
using System.Threading.Tasks;

namespace Nutzen.Common.Retry;

/// <summary>
/// An interceptor that retries failed requests with configurable retry count, delay, and jitter.
/// Apply the generated [RetryInterceptor] attribute to request handlers to use this interceptor.
/// </summary>
/// <remarks>
/// This interceptor supports:
/// - Configurable number of retry attempts
/// - Base delay between retries with optional exponential backoff
/// - Random jitter to prevent thundering herd problems
/// - Custom predicates to determine which exceptions/results should trigger retries
/// </remarks>
[Interceptor(Order = -50)] // Run before logging but after caching
public class RetryInterceptor<TRequest, TResponse> : IRequestInterceptor<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly RetryInterceptorOptions _options;
    private readonly ILogger<RetryInterceptor<TRequest, TResponse>> _logger;
    private readonly Random _random;

    public RetryInterceptor(
        IOptions<RetryInterceptorOptions> options,
        ILogger<RetryInterceptor<TRequest, TResponse>> logger)
    {
        _options = options.Value;
        _logger = logger;
        _random = new Random();
    }

    /// <summary>
    /// Intercepts the request and retries on failure based on the configured options.
    /// </summary>
    public async Task<Result<TResponse>> Intercept(
        TRequest request,
        Func<TRequest, Task<Result<TResponse>>> next)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = request.Id;
        var attempt = 0;
        var maxAttempts = _options.MaxRetryCount + 1; // +1 for the initial attempt

        while (true)
        {
            attempt++;

            try
            {
                var result = await next(request);

                if (result.IsSuccess)
                {
                    if (attempt > 1)
                    {
                        _logger.LogInformation(
                            "[{RequestId}] Request '{RequestName}' succeeded on attempt {Attempt}",
                            requestId, requestName, attempt);
                    }
                    return result;
                }

                // Check if we should retry on failed result
                if (_options.ShouldRetryOnFailedResult?.Invoke(result.ErrorMessage) == true && attempt < maxAttempts)
                {
                    var delay = CalculateDelay(attempt);
                    _logger.LogWarning(
                        "[{RequestId}] Request '{RequestName}' failed with error '{Error}' on attempt {Attempt}/{MaxAttempts}. Retrying in {DelayMs}ms...",
                        requestId, requestName, result.ErrorMessage, attempt, maxAttempts, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    continue;
                }

                // Not configured to retry on failed results, or max attempts reached
                if (attempt > 1)
                {
                    _logger.LogWarning(
                        "[{RequestId}] Request '{RequestName}' failed after {Attempt} attempt(s) with error: {Error}",
                        requestId, requestName, attempt, result.ErrorMessage);
                }
                return result;
            }
            catch (Exception ex)
            {
                // Check if we should retry this exception
                var shouldRetry = _options.ShouldRetryOnException?.Invoke(ex) ?? true;

                if (shouldRetry && attempt < maxAttempts)
                {
                    var delay = CalculateDelay(attempt);
                    _logger.LogWarning(
                        ex,
                        "[{RequestId}] Request '{RequestName}' threw exception on attempt {Attempt}/{MaxAttempts}. Retrying in {DelayMs}ms...",
                        requestId, requestName, attempt, maxAttempts, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    continue;
                }

                // Not configured to retry this exception, or max attempts reached
                _logger.LogError(
                    ex,
                    "[{RequestId}] Request '{RequestName}' failed after {Attempt} attempt(s) with exception",
                    requestId, requestName, attempt);

                throw;
            }
        }
    }

    /// <summary>
    /// Calculates the delay for the given retry attempt, including jitter.
    /// </summary>
    private TimeSpan CalculateDelay(int attempt)
    {
        var baseDelay = _options.BaseDelay;

        if (_options.UseExponentialBackoff)
        {
            // Exponential backoff: baseDelay * 2^(attempt-1)
            var multiplier = Math.Pow(2, attempt - 1);
            baseDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * multiplier);
        }

        // Cap at max delay
        if (baseDelay > _options.MaxDelay)
        {
            baseDelay = _options.MaxDelay;
        }

        // Add jitter
        var jitterMs = _random.NextDouble() * _options.MaxJitter.TotalMilliseconds;
        var totalDelay = baseDelay + TimeSpan.FromMilliseconds(jitterMs);

        return totalDelay;
    }
}
