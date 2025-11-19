using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Messages;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

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

    public async Task<string> ProduceAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();

        // Serialize message using protobuf
        byte[] serializedData = RedisFlow.Domain.Messages.MessageExtensions.ToBytes(message);

        // Add to Redis Stream with single field containing serialized protobuf
        NameValueEntry[] streamEntry = new NameValueEntry[]
        {
            new("data", serializedData)
        };

        RedisValue messageId = await db.StreamAddAsync(_streamKey, streamEntry);

        _logger.LogInformation(
            "Produced message to stream '{StreamKey}' with ID '{MessageId}' from producer '{Producer}'",
            _streamKey,
            messageId,
            message.Producer);

        return messageId.ToString();
    }
}
