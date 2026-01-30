using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nutzen;

#region  Request

public interface IRequest<TResponse> 
{
    string Id { get; }
}

public interface IRequest : IRequest<Empty> 
{

}

public record Request<TResponse> : IRequest<TResponse>
{
    public string Id { get; } = Guid.NewGuid().ToString();
}

public record Request : IRequest
{
    public string Id { get; } = Guid.NewGuid().ToString();
}

#endregion

#region Handler

public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request);
}

public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Empty>
    where TRequest : IRequest
{

}

#endregion

#region Interceptor

/// <summary>
/// Marks a class as a request interceptor. The class must implement 
/// IRequestInterceptor&lt;TRequest, TResponse&gt;.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class InterceptorAttribute : Attribute
{
    /// <summary>
    /// The order in which this interceptor should be executed. Lower values execute first.
    /// </summary>
    public int Order { get; set; } = int.MaxValue;
}

/// <summary>
/// Base class for generated interceptor attributes that can be applied to request handlers.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public abstract class InterceptorAttributeBase : Attribute
{
    /// <summary>
    /// The type of the interceptor class.
    /// </summary>
    public abstract Type InterceptorType { get; }

    /// <summary>
    /// The order in which this interceptor should be executed. Lower values execute first.
    /// </summary>
    public int Order { get; set; } = 0;
}


public interface IRequestInterceptor<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> Intercept(TRequest request, Func<TRequest, Task<Result<TResponse>>> next);
}

public interface IRequestInterceptor<TRequest> : IRequestInterceptor<TRequest, Empty>
    where TRequest : IRequest
{

}

/// <summary>
/// A request handler that wraps another handler and executes interceptors in a pipeline.
/// </summary>
public sealed class InterceptedRequestHandler<TRequest, TResponse> : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Func<TRequest, Task<Result<TResponse>>> _pipeline;

    public InterceptedRequestHandler(Func<TRequest, Task<Result<TResponse>>> pipeline)
    {
        _pipeline = pipeline;
    }

    public Task<Result<TResponse>> Handle(TRequest request)
    {
        return _pipeline(request);
    }
}

/// <summary>
/// A request handler that wraps another handler and executes interceptors in a pipeline.
/// This version is for requests that don't return a response (IRequest without TResponse).
/// </summary>
public sealed class InterceptedRequestHandler<TRequest> : IRequestHandler<TRequest>
    where TRequest : IRequest
{
    private readonly Func<TRequest, Task<Result<Empty>>> _pipeline;

    public InterceptedRequestHandler(Func<TRequest, Task<Result<Empty>>> pipeline)
    {
        _pipeline = pipeline;
    }

    public Task<Result<Empty>> Handle(TRequest request)
    {
        return _pipeline(request);
    }
}

#endregion

#region Dispatcher

public interface IDispatcher
{
    Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>;

    Task<Result> DispatchAsync<TRequest>(TRequest request)
        where TRequest : IRequest;
}

public class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Result<TResponse>> DispatchAsync<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(typeof(TRequest), typeof(TResponse));

        var handler = (IRequestHandler<TRequest, TResponse>)_serviceProvider.GetService(handlerType)!;

        if (handler == null)
        {
            return Result<TResponse>.Failure($"No handler found for request type {typeof(TRequest).FullName}.");
        }

        return await handler.Handle(request);
    }

    public async Task<Result> DispatchAsync<TRequest>(TRequest request)
        where TRequest : IRequest
    {
        return (Result) await DispatchAsync<TRequest, Empty>(request);
    }
}

#endregion