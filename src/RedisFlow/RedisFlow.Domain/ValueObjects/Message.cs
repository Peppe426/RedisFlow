namespace RedisFlow.Domain.ValueObjects;

/// <summary>
/// Domain message representing a message in the Redis stream
/// </summary>
/// <typeparam name="TContent">The type of the message content</typeparam>
public class Message<TContent>
{
    /// <summary>
    /// Producer identifier
    /// </summary>
    public ProducerReference Producer { get; set; } = default!;

    /// <summary>
    /// Message content
    /// </summary>
    public TContent Content { get; set; } = default!;

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public Message()
    {
    }

    public Message(string producer, TContent content)
    {
        Producer = producer;
        Content = content;
    }
}