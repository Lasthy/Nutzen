using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nutzen;

public interface IEvent
{
    string Id { get; }
}

public record NEvent : IEvent
{
    public string Id { get; } = Guid.NewGuid().ToString();
}

public interface IEventHandler<TEvent>
    where TEvent : IEvent
{
    Task Handle(TEvent @event);
}

public interface IEventBus
{
    Task Publish<TEvent>(TEvent @event) where TEvent : IEvent;
}

public class NEventBus : IEventBus
{
    private readonly ILogger<NEventBus> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Type, List<Type>> _handlers = new();

    public NEventBus(ILogger<NEventBus> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task Publish<TEvent>(TEvent @event) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        _handlers.TryGetValue(eventType, out var handlers);

        if(handlers == null)
        {
            _logger.LogInformation("No handlers registered for event type {EventType}.", eventType.Name);

            return;
        }

        foreach (var handlerType in handlers)
        {
            var handler = ActivatorUtilities.CreateInstance(_serviceProvider, handlerType) as IEventHandler<TEvent>;

            if (handler == null)
            {
                _logger.LogWarning("Handler type {HandlerType} could not be created for event type {EventType}.", handlerType.Name, eventType.Name);
                
                continue;
            }

            _logger.LogInformation("Handling event {EventId} with handler {HandlerType}.", @event.Id, handlerType.Name);

            await handler.Handle(@event);
        }
    }
}