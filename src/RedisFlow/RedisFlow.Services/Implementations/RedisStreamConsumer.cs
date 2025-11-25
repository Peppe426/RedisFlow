using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Messages;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Services.Implementations;

public class RedisStreamConsumer : IConsumer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStreamConsumer> _logger;
    private readonly string _streamKey;
    private readonly string _groupName;
    private readonly string _consumerName;
    private readonly int _claimIdleTimeoutMs;
    private readonly int _pollingIntervalMs;

    public RedisStreamConsumer(
        IConnectionMultiplexer redis,
        ILogger<RedisStreamConsumer> logger,
        string streamKey = "messages",
        string groupName = "default-group",
        string? consumerName = null,
        int claimIdleTimeoutMs = 5000,
        int pollingIntervalMs = 100)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _streamKey = streamKey ?? throw new ArgumentNullException(nameof(streamKey));
        _groupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
        _consumerName = consumerName ?? $"consumer-{Guid.NewGuid():N}";
        _claimIdleTimeoutMs = claimIdleTimeoutMs;
        _pollingIntervalMs = pollingIntervalMs;
    }

    public async Task ConsumeAsync(
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var db = _redis.GetDatabase();

        // Ensure consumer group exists
        await EnsureConsumerGroupExistsAsync(db, cancellationToken);

        _logger.LogInformation(
            "Starting consumer {ConsumerName} in group {GroupName} for stream {StreamKey}",
            _consumerName,
            _groupName,
            _streamKey);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // First, try to claim and process pending messages (for retry/recovery)
                await ProcessPendingMessagesAsync(db, handler, cancellationToken);

                // Then read new messages
                var entries = await db.StreamReadGroupAsync(
                    _streamKey,
                    _groupName,
                    _consumerName,
                    ">",
                    count: 10);

                foreach (var entry in entries)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await ProcessStreamEntryAsync(db, entry, handler, cancellationToken);
                }

                // If no messages, wait a bit before polling again
                if (entries.Length == 0)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            _logger.LogInformation(
                "Consumer {ConsumerName} stopped gracefully",
                _consumerName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Consumer {ConsumerName} cancelled",
                _consumerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in consumer {ConsumerName}",
                _consumerName);
            throw;
        }
    }

    private async Task EnsureConsumerGroupExistsAsync(IDatabase db, CancellationToken cancellationToken)
    {
        try
        {
            // Try to create the consumer group starting from the beginning
            await db.StreamCreateConsumerGroupAsync(_streamKey, _groupName, "0-0");
            
            _logger.LogInformation(
                "Created consumer group {GroupName} for stream {StreamKey}",
                _groupName,
                _streamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists, which is fine
            _logger.LogDebug(
                "Consumer group {GroupName} already exists for stream {StreamKey}",
                _groupName,
                _streamKey);
        }
    }

    private async Task ProcessPendingMessagesAsync(
        IDatabase db,
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check for pending messages for this consumer
            var pending = await db.StreamPendingMessagesAsync(
                _streamKey,
                _groupName,
                count: 10,
                consumerName: _consumerName);

            foreach (var pendingMessage in pending)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Claim the message if it's been pending for more than 5 seconds
                if (pendingMessage.IdleTimeInMilliseconds > 5000)
                {
                    var claimed = await db.StreamClaimAsync(
                        _streamKey,
                        _groupName,
                        _consumerName,
                        minIdleTimeInMs: 5000,
                        messageIds: new[] { pendingMessage.MessageId });

                    foreach (var entry in claimed)
                    {
                        _logger.LogInformation(
                            "Claimed pending message {MessageId} from stream {StreamKey}",
                            entry.Id,
                            _streamKey);

                        await ProcessStreamEntryAsync(db, entry, handler, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error processing pending messages for consumer {ConsumerName}",
                _consumerName);
        }
    }

    private async Task ProcessStreamEntryAsync(
        IDatabase db,
        StreamEntry entry,
        Func<Message, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var dataField = entry.Values.FirstOrDefault(v => v.Name == "data");
            if (dataField.Name.IsNullOrEmpty || !dataField.Value.HasValue)
            {
                _logger.LogWarning(
                    "Stream entry {MessageId} has no data field",
                    entry.Id);
                
                await db.StreamAcknowledgeAsync(_streamKey, _groupName, entry.Id);
                return;
            }

            var data = (byte[])dataField.Value!;
            var message = MessageExtensions.Deserialize(data);

            _logger.LogDebug(
                "Processing message {MessageId} from stream {StreamKey}. Producer: {Producer}, Content: {Content}",
                entry.Id,
                _streamKey,
                message.Producer,
                message.Content);

            await handler(message, cancellationToken);

            await db.StreamAcknowledgeAsync(_streamKey, _groupName, entry.Id);

            _logger.LogDebug(
                "Acknowledged message {MessageId} from stream {StreamKey}",
                entry.Id,
                _streamKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing message {MessageId} from stream {StreamKey}",
                entry.Id,
                _streamKey);
            
            throw;
        }
    }
}
