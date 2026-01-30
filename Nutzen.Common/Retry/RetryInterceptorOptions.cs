using System;

namespace Nutzen.Common.Retry;

/// <summary>
/// Configuration options for the retry interceptor.
/// </summary>
public sealed class RetryInterceptorOptions
{
    /// <summary>
    /// The maximum number of retry attempts after the initial failure.
    /// Default is 3 retries.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// The base delay between retry attempts.
    /// Default is 1 second.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The maximum jitter to add to the delay between retries.
    /// A random value between 0 and this amount will be added to the base delay.
    /// Default is 500 milliseconds.
    /// </summary>
    public TimeSpan MaxJitter { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Whether to use exponential backoff for retry delays.
    /// If true, the delay doubles with each retry attempt.
    /// Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// The maximum delay between retries (caps exponential backoff).
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A predicate to determine if an exception should trigger a retry.
    /// If null, all exceptions will trigger a retry.
    /// </summary>
    public Func<Exception, bool>? ShouldRetryOnException { get; set; }

    /// <summary>
    /// A predicate to determine if a failed result should trigger a retry.
    /// If null, failed results will not trigger a retry (only exceptions will).
    /// </summary>
    public Func<string?, bool>? ShouldRetryOnFailedResult { get; set; }
}
