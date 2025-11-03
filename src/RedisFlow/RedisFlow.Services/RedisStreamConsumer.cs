using Google.Protobuf;
using Microsoft.Extensions.Logging;
using RedisFlow.Contracts.Messages;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisStreamConsumer : IConsumer
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisStreamConsumer> _logger;
    private readonly string _streamKey;
    private readonly string _consumerGroup;
    private readonly string _consumerName;
    private readonly TimeSpan _pollingDelay;
    private readonly TimeSpan _errorRetryDelay;

    public RedisStreamConsumer(
        IConnectionMultiplexer redis,
        ILogger<RedisStreamConsumer> logger,
        string consumerGroup = "default-group",
        string? consumerName = null,
        string streamKey = "messages",
        TimeSpan? pollingDelay = null,
        TimeSpan? errorRetryDelay = null)
    {
        _database = redis.GetDatabase();
        _logger = logger;
        _streamKey = streamKey;
        _consumerGroup = consumerGroup;
        _consumerName = consumerName ?? $"consumer-{Guid.NewGuid():N}";
        _pollingDelay = pollingDelay ?? TimeSpan.FromMilliseconds(100);
        _errorRetryDelay = errorRetryDelay ?? TimeSpan.FromSeconds(1);
    }

    public async Task ConsumeAsync(
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        // Ensure consumer group exists
        await EnsureConsumerGroupExistsAsync();

        // First, process any pending messages (replay scenario)
        await ProcessPendingMessagesAsync(handler, cancellationToken);

        // Then, continuously read new messages
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var entries = await _database.StreamReadGroupAsync(
                    _streamKey,
                    _consumerGroup,
                    _consumerName,
                    ">", // Only new messages
                    count: 10);

                foreach (var entry in entries)
                {
                    await ProcessMessageAsync(entry, handler, cancellationToken);
                }

                if (entries.Length == 0)
                {
                    await Task.Delay(_pollingDelay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming messages from stream {StreamKey}", _streamKey);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task EnsureConsumerGroupExistsAsync()
    {
        try
        {
            await _database.StreamCreateConsumerGroupAsync(
                _streamKey,
                _consumerGroup,
                StreamPosition.NewMessages);

            _logger.LogInformation(
                "Created consumer group {ConsumerGroup} for stream {StreamKey}",
                _consumerGroup, _streamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists
            _logger.LogDebug(
                "Consumer group {ConsumerGroup} already exists for stream {StreamKey}",
                _consumerGroup, _streamKey);
        }
    }

    private async Task ProcessPendingMessagesAsync(
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Checking for pending messages in consumer group {ConsumerGroup}",
            _consumerGroup);

        var pendingInfo = await _database.StreamPendingAsync(_streamKey, _consumerGroup);

        if (pendingInfo.PendingMessageCount > 0)
        {
            _logger.LogInformation(
                "Found {PendingCount} pending messages, processing them",
                pendingInfo.PendingMessageCount);

            // Read pending messages for this consumer
            var entries = await _database.StreamReadGroupAsync(
                _streamKey,
                _consumerGroup,
                _consumerName,
                "0", // Start from beginning of pending messages
                count: (int)pendingInfo.PendingMessageCount);

            foreach (var entry in entries)
            {
                await ProcessMessageAsync(entry, handler, cancellationToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        StreamEntry entry,
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataField = entry.Values.FirstOrDefault(v => v.Name == "data");
            if (dataField.Name.IsNullOrEmpty)
            {
                _logger.LogWarning("Message {MessageId} has no data field", entry.Id);
                await _database.StreamAcknowledgeAsync(_streamKey, _consumerGroup, entry.Id);
                return;
            }

            var streamMessage = StreamMessage.Parser.ParseFrom((byte[])dataField.Value!);

            var message = new Message(
                streamMessage.Producer,
                streamMessage.Content);

            _logger.LogInformation(
                "Processing message {MessageId} from producer {Producer}",
                entry.Id, streamMessage.Producer);

            await handler(message, cancellationToken);

            // Acknowledge the message after successful processing
            await _database.StreamAcknowledgeAsync(_streamKey, _consumerGroup, entry.Id);

            _logger.LogInformation(
                "Acknowledged message {MessageId}",
                entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing message {MessageId}, will remain in pending list",
                entry.Id);
            // Message remains in PEL for retry
        }
    }
}
