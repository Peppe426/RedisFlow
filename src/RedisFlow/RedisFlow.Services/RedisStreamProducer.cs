using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using RedisFlow.Contracts;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisStreamProducer : IProducer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStreamProducer> _logger;
    private readonly string _streamKey;

    public RedisStreamProducer(
        IConnectionMultiplexer redis,
        ILogger<RedisStreamProducer> logger,
        string streamKey = "redisflow:stream")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _streamKey = streamKey ?? throw new ArgumentNullException(nameof(streamKey));
    }

    public async Task ProduceAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var db = _redis.GetDatabase();

        // Convert domain Message to protobuf EventMessage
        var eventMessage = new EventMessage
        {
            Producer = message.Producer,
            Content = message.Content,
            CreatedAt = Timestamp.FromDateTime(message.CreatedAt)
        };

        // Serialize to byte array
        var payload = eventMessage.ToByteArray();

        // Add to Redis stream
        var streamEntries = new NameValueEntry[]
        {
            new NameValueEntry("data", payload)
        };

        var messageId = await db.StreamAddAsync(_streamKey, streamEntries);

        _logger.LogInformation(
            "Produced message to stream '{StreamKey}' with ID '{MessageId}' from producer '{Producer}'",
            _streamKey,
            messageId,
            message.Producer);
    }
}
