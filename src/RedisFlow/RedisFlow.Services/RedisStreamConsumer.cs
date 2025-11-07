using Google.Protobuf;
using Microsoft.Extensions.Logging;
using RedisFlow.Contracts;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services;

public class RedisStreamConsumer : IConsumer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStreamConsumer> _logger;
    private readonly string _streamKey;
    private readonly string _consumerGroup;
    private readonly string _consumerName;

    public RedisStreamConsumer(
        IConnectionMultiplexer redis,
        ILogger<RedisStreamConsumer> logger,
        string consumerGroup = "default-group",
        string? consumerName = null,
        string streamKey = "redisflow:stream")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _streamKey = streamKey ?? throw new ArgumentNullException(nameof(streamKey));
        _consumerGroup = consumerGroup ?? throw new ArgumentNullException(nameof(consumerGroup));
        _consumerName = consumerName ?? Environment.MachineName;
    }

    public async Task ConsumeAsync(
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var db = _redis.GetDatabase();

        // Ensure consumer group exists
        await EnsureConsumerGroupExistsAsync(db);

        _logger.LogInformation(
            "Starting consumer '{ConsumerName}' in group '{ConsumerGroup}' on stream '{StreamKey}'",
            _consumerName,
            _consumerGroup,
            _streamKey);

        // First, process any pending messages (replay)
        await ProcessPendingMessagesAsync(db, handler, cancellationToken);

        // Then continuously read new messages
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    _streamKey,
                    _consumerGroup,
                    _consumerName,
                    ">", // Read only new messages
                    count: 10);

                foreach (var entry in entries)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await ProcessMessageAsync(db, entry, handler, cancellationToken);
                }

                if (entries.Length == 0)
                {
                    // No messages, wait a bit before polling again
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from stream");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task EnsureConsumerGroupExistsAsync(IDatabase db)
    {
        try
        {
            // Try to create the consumer group
            await db.StreamCreateConsumerGroupAsync(
                _streamKey,
                _consumerGroup,
                StreamPosition.NewMessages);

            _logger.LogInformation(
                "Created consumer group '{ConsumerGroup}' on stream '{StreamKey}'",
                _consumerGroup,
                _streamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists, this is fine
            _logger.LogDebug("Consumer group '{ConsumerGroup}' already exists", _consumerGroup);
        }
    }

    private async Task ProcessPendingMessagesAsync(
        IDatabase db,
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for pending messages in PEL...");

        var pending = await db.StreamPendingMessagesAsync(
            _streamKey,
            _consumerGroup,
            count: 100,
            consumerName: _consumerName);

        if (pending.Length > 0)
        {
            _logger.LogInformation("Found {Count} pending messages to process", pending.Length);

            // Claim and process pending messages
            var messageIds = pending.Select(p => p.MessageId).ToArray();
            var claimed = await db.StreamClaimAsync(
                _streamKey,
                _consumerGroup,
                _consumerName,
                minIdleTimeInMs: 0,
                messageIds: messageIds);

            foreach (var entry in claimed)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessMessageAsync(db, entry, handler, cancellationToken);
            }
        }
        else
        {
            _logger.LogInformation("No pending messages found");
        }
    }

    private async Task ProcessMessageAsync(
        IDatabase db,
        StreamEntry entry,
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract the protobuf payload
            var dataField = entry.Values.FirstOrDefault(v => v.Name == "data");
            if (dataField.Value.IsNull)
            {
                _logger.LogWarning("Message {MessageId} has no data field", entry.Id);
                await db.StreamAcknowledgeAsync(_streamKey, _consumerGroup, entry.Id);
                return;
            }

            var payload = (byte[])dataField.Value!;
            var eventMessage = EventMessage.Parser.ParseFrom(payload);

            // Convert protobuf EventMessage to domain Message
            var message = new Message(
                eventMessage.Producer,
                eventMessage.Content);

            _logger.LogInformation(
                "Processing message {MessageId} from producer '{Producer}'",
                entry.Id,
                eventMessage.Producer);

            // Invoke the handler
            await handler(message, cancellationToken);

            // Acknowledge the message
            await db.StreamAcknowledgeAsync(_streamKey, _consumerGroup, entry.Id);

            _logger.LogDebug("Acknowledged message {MessageId}", entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing message {MessageId}. Message will remain in PEL for retry.",
                entry.Id);
            // Don't acknowledge - message stays in PEL for retry
        }
    }
}
