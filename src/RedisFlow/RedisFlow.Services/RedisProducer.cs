using MessagePack;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisProducer : IProducer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _streamKey;

    public RedisProducer(IConnectionMultiplexer redis, string streamKey = "messages")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _streamKey = streamKey ?? throw new ArgumentNullException(nameof(streamKey));
    }

    public async Task ProduceAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var database = _redis.GetDatabase();
        
        // Serialize the message using MessagePack
        var serializedMessage = MessagePackSerializer.Serialize(message);
        
        // Add to Redis stream
        var streamEntry = new NameValueEntry[]
        {
            new("data", serializedMessage)
        };
        
        var messageId = await database.StreamAddAsync(_streamKey, streamEntry);
        
        if (messageId.IsNull)
        {
            throw new InvalidOperationException("Failed to add message to Redis stream.");
        }
    }
}
