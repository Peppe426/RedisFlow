using MessagePack;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisConsumer : IConsumer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _streamKey;
    private readonly string _consumerGroup;
    private readonly string _consumerName;

    public RedisConsumer(
        IConnectionMultiplexer redis, 
        string streamKey = "messages",
        string consumerGroup = "default-group",
        string? consumerName = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _streamKey = streamKey ?? throw new ArgumentNullException(nameof(streamKey));
        _consumerGroup = consumerGroup ?? throw new ArgumentNullException(nameof(consumerGroup));
        _consumerName = consumerName ?? $"consumer-{Guid.NewGuid()}";
    }

    public async Task ConsumeAsync(Func<Message, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var database = _redis.GetDatabase();

        // Ensure consumer group exists
        await EnsureConsumerGroupExistsAsync(database);

        // Process pending messages first
        await ProcessPendingMessagesAsync(database, handler, cancellationToken);

        // Then process new messages
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var streamEntries = await database.StreamReadGroupAsync(
                    _streamKey,
                    _consumerGroup,
                    _consumerName,
                    position: ">",
                    count: 10);

                if (streamEntries.Length == 0)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                foreach (var entry in streamEntries)
                {
                    await ProcessStreamEntryAsync(database, entry, handler, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task EnsureConsumerGroupExistsAsync(IDatabase database)
    {
        try
        {
            await database.StreamCreateConsumerGroupAsync(_streamKey, _consumerGroup, StreamPosition.NewMessages);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists, ignore
        }
    }

    private async Task ProcessPendingMessagesAsync(
        IDatabase database, 
        Func<Message, CancellationToken, Task> handler, 
        CancellationToken cancellationToken)
    {
        var pendingMessages = await database.StreamPendingMessagesAsync(
            _streamKey,
            _consumerGroup,
            count: 100,
            _consumerName);

        foreach (var pendingMessage in pendingMessages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Claim the pending message
            var claimedEntries = await database.StreamClaimAsync(
                _streamKey,
                _consumerGroup,
                _consumerName,
                minIdleTimeInMs: 0,
                messageIds: new[] { pendingMessage.MessageId });

            foreach (var entry in claimedEntries)
            {
                await ProcessStreamEntryAsync(database, entry, handler, cancellationToken);
            }
        }
    }

    private async Task ProcessStreamEntryAsync(
        IDatabase database,
        StreamEntry entry,
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataField = entry.Values.FirstOrDefault(v => v.Name == "data");
            if (dataField.Value.IsNull)
            {
                return;
            }

            var messageBytes = (byte[])dataField.Value!;
            var message = MessagePackSerializer.Deserialize<Message>(messageBytes);

            await handler(message, cancellationToken);

            // Acknowledge the message
            await database.StreamAcknowledgeAsync(_streamKey, _consumerGroup, entry.Id);
        }
        catch (Exception)
        {
            // In production, log the error and potentially implement retry logic
            throw;
        }
    }
}
