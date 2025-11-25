using RedisFlow.Domain.Base;

namespace RedisFlow.Domain.Aggregates;

using RedisFlow.Domain.ValueObjects;

public record StreamHandler : AggregateRoot
{
    public void AddMessage<T>(Entry<T> message)
    {
        RaiseDomainEvent(message);
    }
}