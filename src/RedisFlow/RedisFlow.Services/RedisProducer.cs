using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using RedisFlow.Domain.Proto;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisProducer : IProducer
{
    private readonly IDatabase _database;
    private readonly string _streamName;

    public RedisProducer(IConnectionMultiplexer redis, string streamName)
    {
        _database = redis.GetDatabase();
        _streamName = streamName;
    }

    public async Task ProduceAsync(Message message, CancellationToken cancellationToken = default)
    {
        var protoMessage = new MessageProto
        {
            Producer = message.Producer,
            Content = message.Content,
            CreatedAt = Timestamp.FromDateTime(message.CreatedAt.ToUniversalTime())
        };

        var serialized = protoMessage.ToByteArray();

        var streamValues = new[]
        {
            new NameValueEntry("data", serialized)
        };

        await _database.StreamAddAsync(_streamName, streamValues);
    }
}
