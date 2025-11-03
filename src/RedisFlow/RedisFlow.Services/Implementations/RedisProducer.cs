using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using RedisFlow.Contracts;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services.Implementations;

public class RedisProducer : IProducer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisProducer> _logger;
    private readonly string _streamKey;

    public RedisProducer(
        IConnectionMultiplexer redis,
        ILogger<RedisProducer> logger,
        string streamKey = "messages:stream")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _streamKey = streamKey ?? throw new ArgumentNullException(nameof(streamKey));
    }

    public async Task ProduceAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var db = _redis.GetDatabase();

        // Convert domain Message to protobuf MessagePayload
        var payload = new MessagePayload
        {
            Producer = message.Producer,
            Content = message.Content,
            CreatedAt = Timestamp.FromDateTime(message.CreatedAt.ToUniversalTime())
        };

        // Serialize to byte array
        using var stream = new MemoryStream();
        using var codedOutputStream = new Google.Protobuf.CodedOutputStream(stream, leaveOpen: true);
        payload.WriteTo(codedOutputStream);
        codedOutputStream.Flush();
        var serializedData = stream.ToArray();

        // Add to Redis Stream with single field containing serialized protobuf
        var streamEntry = new NameValueEntry[]
        {
            new("data", serializedData)
        };

        var messageId = await db.StreamAddAsync(_streamKey, streamEntry);

        _logger.LogInformation(
            "Produced message to stream '{StreamKey}' with ID '{MessageId}' from producer '{Producer}'",
            _streamKey,
            messageId,
            message.Producer);
    }
}
