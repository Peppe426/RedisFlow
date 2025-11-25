using RedisFlow.Domain.Base;

namespace RedisFlow.Domain.ValueObjects;

/// <summary>
/// Represents a domain event message in the Redis stream, containing a producer identifier, message content, and creation timestamp.
/// </summary>
/// <typeparam name="TContent">The type of the message content</typeparam>
public record Entry<TContent> : IDomainEvent
{
    /// <summary>
    /// Producer identifier
    /// </summary>
    public ProducerReference Producer { get; private set; } = default!;

    /// <summary>
    /// Message content
    /// </summary>
    public TContent Content { get; private set; } = default!;

    /// <summary>
    /// Timestamp when the message was created (UTC)
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public Entry(string producer, TContent content)
    {
        Producer = producer;
        Content = content;
    }
}