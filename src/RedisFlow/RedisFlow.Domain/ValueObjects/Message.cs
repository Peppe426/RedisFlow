using MessagePack;

namespace RedisFlow.Domain.ValueObjects;

[MessagePackObject]
public class Message
{
    [Key(0)]
    public string Producer { get; set; } = string.Empty;

    [Key(1)]
    public string Content { get; set; } = string.Empty;

    [Key(2)]
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
}