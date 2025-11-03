namespace RedisFlow.Domain.ValueObjects;

public class Message
{
    public string Producer
    {
        get;
        private set;
    }

    public string Content
    {
        get;
    }

    public DateTime CreatedAt { get; }

    public Message(string producer, string content) : this(producer, content, DateTime.UtcNow)
    {
    }

    public Message(string producer, string content, DateTime createdAt)
    {
        Producer = producer;
        Content = content;
        CreatedAt = createdAt;
    }
}