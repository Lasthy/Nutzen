using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Nutzen.Common.Logging;

/// <summary>
/// An interceptor that logs request execution details including timing information.
/// Apply the generated [LoggingInterceptor] attribute to request handlers to use this interceptor.
/// </summary>
/// <remarks>
/// This interceptor uses object-based signatures to support all request/response types.
/// The generator will create proper typed wrappers for each handler.
/// </remarks>
[Interceptor(Order = 0)]
public class LoggingInterceptor<TRequest, TResponse> : IRequestInterceptor<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingInterceptor<TRequest, TResponse>> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Intercepts the request and logs timing and result information.
    /// </summary>
    public async Task<Result<TResponse>> Intercept(
        TRequest request, 
        Func<TRequest, Task<Result<TResponse>>> next)
    {
        var requestType = typeof(TRequest);
        var requestName = $"{(requestType.DeclaringType?.Name ?? "")}.{requestType.Name}";
        var requestId = request.Id;
        
        _logger.LogInformation("[{RequestId}] Starting request '{RequestName}'", requestId, requestName);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await next(request);
            stopwatch.Stop();
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("[{RequestId}] Completed request '{RequestName}' successfully in {ElapsedMs}ms", requestId, requestName, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("[{RequestId}] Request '{RequestName}' failed in {ElapsedMs}ms: {ErrorMessage}", requestId, requestName, stopwatch.ElapsedMilliseconds, result.ErrorMessage);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[{RequestId}] Request '{RequestName}' threw an exception in {ElapsedMs}ms", requestId, requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
