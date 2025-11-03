using Google.Protobuf;
using RedisFlow.Domain.Proto;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisConsumer : IConsumer
{
    private readonly IDatabase _database;
    private readonly string _streamName;
    private readonly string _consumerGroup;
    private readonly string _consumerName;

    public RedisConsumer(IConnectionMultiplexer redis, string streamName, string consumerGroup, string consumerName)
    {
        _database = redis.GetDatabase();
        _streamName = streamName;
        _consumerGroup = consumerGroup;
        _consumerName = consumerName;
    }

    public async Task ConsumeAsync(Func<Message, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        // Ensure consumer group exists
        await EnsureConsumerGroupExistsAsync();

        // First, process any pending messages from previous runs
        await ProcessPendingMessagesAsync(handler, cancellationToken);

        // Then, continuously read new messages
        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = await _database.StreamReadGroupAsync(
                _streamName,
                _consumerGroup,
                _consumerName,
                ">",
                count: 10
            );

            foreach (var entry in entries)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var data = entry.Values.FirstOrDefault(v => v.Name == "data");
                if (data.Value.IsNull)
                    continue;

                var protoMessage = MessageProto.Parser.ParseFrom((byte[])data.Value!);
                var message = new Message(
                    protoMessage.Producer,
                    protoMessage.Content
                );

                await handler(message, cancellationToken);
                await _database.StreamAcknowledgeAsync(_streamName, _consumerGroup, entry.Id);
            }

            if (entries.Length == 0)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async Task EnsureConsumerGroupExistsAsync()
    {
        try
        {
            // Use StreamPosition.Beginning to ensure we can recover messages added before group creation
            // This is important for scenarios where messages exist in stream before consumer starts
            await _database.StreamCreateConsumerGroupAsync(_streamName, _consumerGroup, StreamPosition.Beginning);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists, ignore
        }
    }

    private async Task ProcessPendingMessagesAsync(Func<Message, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        var pendingInfo = await _database.StreamPendingAsync(_streamName, _consumerGroup);
        if (pendingInfo.PendingMessageCount == 0)
            return;

        var pendingMessages = await _database.StreamPendingMessagesAsync(
            _streamName,
            _consumerGroup,
            (int)pendingInfo.PendingMessageCount,
            RedisValue.Null
        );

        foreach (var pendingMsg in pendingMessages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Claim the message if it's been idle for at least 5 seconds
            // This prevents aggressive claiming and allows consumers time to process
            var claimedEntries = await _database.StreamClaimAsync(
                _streamName,
                _consumerGroup,
                _consumerName,
                5000, // Min idle time in milliseconds (5 seconds)
                new[] { pendingMsg.MessageId }
            );

            foreach (var entry in claimedEntries)
            {
                var data = entry.Values.FirstOrDefault(v => v.Name == "data");
                if (data.Value.IsNull)
                    continue;

                var protoMessage = MessageProto.Parser.ParseFrom((byte[])data.Value!);
                var message = new Message(
                    protoMessage.Producer,
                    protoMessage.Content
                );

                await handler(message, cancellationToken);
                await _database.StreamAcknowledgeAsync(_streamName, _consumerGroup, entry.Id);
            }
        }
    }
}
