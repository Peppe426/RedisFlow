using RedisFlow.Domain.Base;

namespace RedisFlow.Domain.Aggregates;

using RedisFlow.Domain.ValueObjects;

public record StreamHandler : AggregateRoot
{
    public Connection Connection
    {
        get;
        private set;
    }

    public StreamHandler(string host, int port, string? password = null)
    {
        Connection = new Connection(host, port, password);
    }

    public void AddMessage<T>(Entry<T> message)
    {
        RaiseDomainEvent(message);
    }


}