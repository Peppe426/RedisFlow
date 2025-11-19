namespace RedisFlow.Domain.ValueObjects;

/// <summary>
/// Domain message representing a message in the Redis stream
/// </summary>
public class Message
{
    /// <summary>
    /// Producer identifier
    /// </summary>
    public string Producer { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Message()
    {
    }

    public Message(string producer, string content)
    {
        Producer = producer;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }

    public Message(string producer, string content, DateTime createdAt)
    {
        Producer = producer;
        Content = content;
        CreatedAt = createdAt;
    }
}