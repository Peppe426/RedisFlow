using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace RedisFlow.Integration;

[TestFixture]
public class MessagePersistenceTests
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redis;
    private string _testStreamKey = null!;

    [SetUp]
    public async Task SetUp()
    {
        // Create unique stream key for each test
        _testStreamKey = $"test-stream-{Guid.NewGuid():N}";

        // Create and start Redis container
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();

        // Get Redis connection
        var connectionString = _redisContainer.GetConnectionString();
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_redis != null)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(_testStreamKey);
            await _redis.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync();
            await _redisContainer.DisposeAsync();
        }
    }

    [Test]
    public async Task Should_ProduceAndConsumeMessage_When_ConsumerIsOnline()
    {
        // Given
        var logger = new LoggerFactory().CreateLogger<RedisStreamProducer>();
        var producer = new RedisStreamProducer(_redis!, logger, _testStreamKey);

        var consumerLogger = new LoggerFactory().CreateLogger<RedisStreamConsumer>();
        var consumer = new RedisStreamConsumer(_redis!, consumerLogger, "test-group", "consumer-1", _testStreamKey);

        var message = new Message("producer-1", "test message");
        var receivedMessages = new List<Message>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // When
        var consumeTask = Task.Run(async () =>
        {
            await consumer.ConsumeAsync((msg, ct) =>
            {
                receivedMessages.Add(msg);
                cts.Cancel(); // Stop after first message
                return Task.CompletedTask;
            }, cts.Token);
        }, cts.Token);

        // Give consumer time to set up
        await Task.Delay(500);

        await producer.ProduceAsync(message, cts.Token);

        await consumeTask;

        // Then
        receivedMessages.Should().HaveCount(1, "because one message was produced");
        receivedMessages[0].Producer.Should().Be("producer-1");
        receivedMessages[0].Content.Should().Be("test message");
    }

    [Test]
    public async Task Should_PersistMessages_When_ConsumerIsOffline()
    {
        // Given
        var logger = new LoggerFactory().CreateLogger<RedisStreamProducer>();
        var producer = new RedisStreamProducer(_redis!, logger, _testStreamKey);

        var db = _redis!.GetDatabase();

        // When - produce messages while consumer is offline
        await producer.ProduceAsync(new Message("producer-1", "message 1"), CancellationToken.None);
        await producer.ProduceAsync(new Message("producer-1", "message 2"), CancellationToken.None);
        await producer.ProduceAsync(new Message("producer-1", "message 3"), CancellationToken.None);

        // Then - verify messages are in the stream and persisted
        var streamLength = await db.StreamLengthAsync(_testStreamKey);
        streamLength.Should().Be(3, "because three messages were produced");

        // Create consumer group AFTER messages are produced to demonstrate they persist
        // and will be available when consumer starts
        await db.StreamCreateConsumerGroupAsync(_testStreamKey, "test-group", "0-0");

        // Messages are available to be read from the beginning by using ">" to get new messages
        // since the consumer group starts at position 0-0
        var entries = await db.StreamReadGroupAsync(_testStreamKey, "test-group", "test-consumer", ">", count: 10);
        entries.Length.Should().Be(3, "because all messages should be available to read");

        // After reading, they are now in pending state
        var pendingInfo = await db.StreamPendingAsync(_testStreamKey, "test-group");
        pendingInfo.PendingMessageCount.Should().Be(3, "because messages were read but not acknowledged");
    }

    [Test]
    public async Task Should_ReplayPendingMessages_When_ConsumerRestarts()
    {
        // Given
        var logger = new LoggerFactory().CreateLogger<RedisStreamProducer>();
        var producer = new RedisStreamProducer(_redis!, logger, _testStreamKey);

        var db = _redis!.GetDatabase();

        // Create consumer group and produce messages while consumer is offline
        await db.StreamCreateConsumerGroupAsync(_testStreamKey, "test-group", StreamPosition.Beginning, createStream: true);

        await producer.ProduceAsync(new Message("producer-1", "offline message 1"), CancellationToken.None);
        await producer.ProduceAsync(new Message("producer-1", "offline message 2"), CancellationToken.None);
        await producer.ProduceAsync(new Message("producer-1", "offline message 3"), CancellationToken.None);

        var receivedMessages = new List<Message>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // When - start consumer and it should process pending messages
        var consumerLogger = new LoggerFactory().CreateLogger<RedisStreamConsumer>();
        var consumer = new RedisStreamConsumer(_redis!, consumerLogger, "test-group", "consumer-1", _testStreamKey);

        var consumeTask = Task.Run(async () =>
        {
            await consumer.ConsumeAsync((msg, ct) =>
            {
                receivedMessages.Add(msg);
                if (receivedMessages.Count >= 3)
                {
                    cts.Cancel(); // Stop after all messages
                }
                return Task.CompletedTask;
            }, cts.Token);
        }, cts.Token);

        await consumeTask;

        // Then
        receivedMessages.Should().HaveCount(3, "because three messages were pending");
        receivedMessages[0].Content.Should().Be("offline message 1");
        receivedMessages[1].Content.Should().Be("offline message 2");
        receivedMessages[2].Content.Should().Be("offline message 3");

        // Verify messages are acknowledged
        var pendingInfo = await db.StreamPendingAsync(_testStreamKey, "test-group");
        pendingInfo.PendingMessageCount.Should().Be(0, "because all messages should be acknowledged");
    }

    [Test]
    public async Task Should_MaintainPendingMessages_When_ConsumerCrashesBeforeAcknowledge()
    {
        // Given
        var logger = new LoggerFactory().CreateLogger<RedisStreamProducer>();
        var producer = new RedisStreamProducer(_redis!, logger, _testStreamKey);

        var db = _redis!.GetDatabase();

        await db.StreamCreateConsumerGroupAsync(_testStreamKey, "test-group", StreamPosition.Beginning, createStream: true);

        await producer.ProduceAsync(new Message("producer-1", "crash test message"), CancellationToken.None);

        // When - consumer reads but crashes before acknowledging (simulated by not calling ConsumeAsync properly)
        var entries = await db.StreamReadGroupAsync(_testStreamKey, "test-group", "consumer-crash", ">", count: 1);
        entries.Should().HaveCount(1, "because one message was read");

        // Then - message should still be in pending list
        var pendingInfo = await db.StreamPendingAsync(_testStreamKey, "test-group");
        pendingInfo.PendingMessageCount.Should().Be(1, "because message was read but not acknowledged");
    }

    [Test]
    public async Task Should_ContinueProcessingNewMessages_After_ReplayingPending()
    {
        // Given
        var logger = new LoggerFactory().CreateLogger<RedisStreamProducer>();
        var producer = new RedisStreamProducer(_redis!, logger, _testStreamKey);

        var db = _redis!.GetDatabase();

        // Create consumer group and produce some pending messages
        await db.StreamCreateConsumerGroupAsync(_testStreamKey, "test-group", StreamPosition.Beginning, createStream: true);

        await producer.ProduceAsync(new Message("producer-1", "pending message"), CancellationToken.None);

        var receivedMessages = new List<Message>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var consumerLogger = new LoggerFactory().CreateLogger<RedisStreamConsumer>();
        var consumer = new RedisStreamConsumer(_redis!, consumerLogger, "test-group", "consumer-1", _testStreamKey);

        // When - start consumer (processes pending), then produce new messages
        var consumeTask = Task.Run(async () =>
        {
            await consumer.ConsumeAsync((msg, ct) =>
            {
                receivedMessages.Add(msg);
                if (receivedMessages.Count >= 3)
                {
                    cts.Cancel(); // Stop after all messages
                }
                return Task.CompletedTask;
            }, cts.Token);
        }, cts.Token);

        // Give consumer time to process pending message
        await Task.Delay(1000);

        // Produce new messages
        await producer.ProduceAsync(new Message("producer-1", "new message 1"), cts.Token);
        await producer.ProduceAsync(new Message("producer-1", "new message 2"), cts.Token);

        await consumeTask;

        // Then
        receivedMessages.Should().HaveCount(3, "because one pending and two new messages were produced");
        receivedMessages[0].Content.Should().Be("pending message");
        receivedMessages[1].Content.Should().Be("new message 1");
        receivedMessages[2].Content.Should().Be("new message 2");
    }
}
