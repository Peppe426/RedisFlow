using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using RedisFlow.Contracts.Messages;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisStreamProducer : IProducer
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisStreamProducer> _logger;
    private readonly string _streamKey;

    public RedisStreamProducer(
        IConnectionMultiplexer redis,
        ILogger<RedisStreamProducer> logger,
        string streamKey = "messages")
    {
        _database = redis.GetDatabase();
        _logger = logger;
        _streamKey = streamKey;
    }

    public async Task ProduceAsync(Message message, CancellationToken cancellationToken = default)
    {
        var streamMessage = new StreamMessage
        {
            Producer = message.Producer,
            Content = message.Content,
            CreatedAt = Timestamp.FromDateTime(message.CreatedAt.ToUniversalTime())
        };

        var payload = streamMessage.ToByteArray();

        var entries = new[]
        {
            new NameValueEntry("data", payload)
        };

        var messageId = await _database.StreamAddAsync(_streamKey, entries);

        _logger.LogInformation(
            "Produced message to stream {StreamKey} with ID {MessageId} from producer {Producer}",
            _streamKey, messageId, message.Producer);
    }
}
