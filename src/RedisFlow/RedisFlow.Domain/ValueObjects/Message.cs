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

    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public Message(string producer, string content)
    {
        Producer = producer;
        Content = content;
    }
}