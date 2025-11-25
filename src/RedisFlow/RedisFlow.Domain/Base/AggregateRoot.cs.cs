namespace RedisFlow.Domain.Base;

using System;

public record AggregateRoot
{
    /// <summary>
    /// Subscribe to this event to react to domain events as they are raised.
    /// </summary>
    public static event Action<IDomainEvent>? DomainEventRaised;

    /// <summary>
    /// Invokes the DomainEventRaised event for subscribers.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    public void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        DomainEventRaised?.Invoke(domainEvent);
    }
}
