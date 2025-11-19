using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Messages;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;
using System.Threading;
using System.Threading.Tasks;

namespace RedisFlow.Services.Implementations;

public class RedisStreamProducer : IProducer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStreamProducer> _logger;
    private readonly string _streamKey;

    public RedisStreamProducer(
        IConnectionMultiplexer redis,
        ILogger<RedisStreamProducer> logger,
        string streamKey = "messages")
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
        byte[] serialized = RedisFlow.Domain.Messages.MessageExtensions.ToBytes(message);

        NameValueEntry[] entries = new NameValueEntry[]
        {
            new NameValueEntry("data", serialized)
        };

        RedisValue messageId = await db.StreamAddAsync(_streamKey, entries);

        _logger.LogInformation(
            "Produced message to stream {StreamKey} with ID {MessageId}. Producer: {Producer}, Content: {Content}",
            _streamKey,
            messageId,
            message.Producer,
            message.Content);

        return messageId.ToString();
    }
}
