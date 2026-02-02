using Nutzen;
using Nutzen.Common.Logging;
using Nutzen.Common.MemoryCache;
using Nutzen.Common.Retry;

namespace Nutzen.WebAPI.Features.RandomNumber;

/// <summary>
/// Unit of Work for generating a random number.
/// Returns success if the number is even, throws an exception if odd.
/// </summary>
[UnitOfWork]
public static class RandomNumber
{
    [Request]
    public record Request : NRequest<Response>;

    public record Response(int Number, bool IsEven);

    [Handler]
    [LoggingInterceptor]
    [MemoryCacheInterceptor]
    [RetryInterceptor]
    public class Handler : IRequestHandler<Request, Response>
    {
        private readonly ILogger<Handler> _logger;
        private readonly Random _random = new();

        public Handler(ILogger<Handler> logger)
        {
            _logger = logger;
        }

        public Task<Result<Response>> Handle(Request request)
        {
            var number = _random.Next(1, 100);
            
            _logger.LogInformation("Generated random number: {Number}", number);

            if (number % 2 != 0)
            {
                _logger.LogWarning("Number {Number} is odd, throwing exception", number);
                throw new InvalidOperationException($"Generated odd number: {number}. Only even numbers are allowed!");
            }

            _logger.LogInformation("Number {Number} is even, returning success", number);
            return Task.FromResult(Result<Response>.Success(new Response(number, true)));
        }
    }
}
