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

    public async Task<string> ProduceAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var db = _redis.GetDatabase();

        // Serialize message using protobuf
        byte[] payload = RedisFlow.Domain.Messages.MessageExtensions.ToBytes(message);

        // Add to Redis stream
        NameValueEntry[] streamEntries = new NameValueEntry[]
        {
            new NameValueEntry("data", payload)
        };

        RedisValue messageId = await db.StreamAddAsync(_streamKey, streamEntries);

        _logger.LogInformation(
            "Produced message to stream '{StreamKey}' with ID '{MessageId}' from producer '{Producer}'",
            _streamKey,
            messageId,
            message.Producer);

        return messageId.ToString();
    }
}
