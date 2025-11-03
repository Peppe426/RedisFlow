using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RedisFlow.Contracts;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services;
using RedisFlow.Services.Contracts;
using StackExchange.Redis;

namespace RedisFlow.Integration;

[TestFixture]
public class RedisStreamIntegrationTests
{
    private DistributedApplication? _app;
    private IConnectionMultiplexer? _redis;

    [SetUp]
    public async Task Setup()
    {
        // Given: An Aspire distributed application with Redis
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.RedisFlow_AppHost>();
        
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Get Redis connection
        var connectionString = await _app.GetConnectionStringAsync("redis");
        _redis = ConnectionMultiplexer.Connect(connectionString!);
        
        // Wait a bit for Redis to be ready
        await Task.Delay(2000);
    }

    [TearDown]
    public async Task TearDown()
    {
        _redis?.Dispose();
        
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    [Test]
    public async Task Should_ProduceAndConsumeMessage_When_UsingRedisStream()
    {
        // Given
        var logger = _app!.Services.GetRequiredService<ILogger<RedisStreamProducer>>();
        var producer = new RedisStreamProducer(_redis!, logger);
        
        var consumerLogger = _app.Services.GetRequiredService<ILogger<RedisStreamConsumer>>();
        var consumer = new RedisStreamConsumer(_redis!, consumerLogger);

        var receivedMessages = new List<Message>();
        var cts = new CancellationTokenSource();

        // When
        var message = new Message("TestProducer", "Test message content");
        await producer.ProduceAsync(message);

        var consumeTask = Task.Run(async () =>
        {
            await consumer.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                cts.Cancel(); // Stop after first message
                await Task.CompletedTask;
            }, cts.Token);
        });

        await Task.Delay(3000); // Wait for consumer to process
        cts.Cancel();
        
        try
        {
            await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Then
        receivedMessages.Should().HaveCount(1, "because one message was produced");
        receivedMessages[0].Producer.Should().Be("TestProducer");
        receivedMessages[0].Content.Should().Be("Test message content");
    }

    [Test]
    public async Task Should_PersistMessages_When_ProducerSendsMultiple()
    {
        // Given
        var logger = _app!.Services.GetRequiredService<ILogger<RedisStreamProducer>>();
        var producer = new RedisStreamProducer(_redis!, logger);

        // When
        for (int i = 1; i <= 5; i++)
        {
            var message = new Message("TestProducer", $"Message {i}");
            await producer.ProduceAsync(message);
        }

        // Wait a bit for messages to be persisted
        await Task.Delay(1000);

        // Then: Verify messages are in the stream
        var db = _redis!.GetDatabase();
        var streamInfo = await db.StreamInfoAsync("redisflow:stream");
        
        streamInfo.Length.Should().BeGreaterOrEqualTo(5, "because at least 5 messages were produced");
    }

    [Test]
    public async Task Should_ReplayPendingMessages_When_ConsumerRestarts()
    {
        // Given
        var logger = _app!.Services.GetRequiredService<ILogger<RedisStreamProducer>>();
        var producer = new RedisStreamProducer(_redis!, logger);

        var consumerLogger = _app.Services.GetRequiredService<ILogger<RedisStreamConsumer>>();
        var consumer1 = new RedisStreamConsumer(
            _redis!, 
            consumerLogger,
            consumerGroup: "test-group",
            consumerName: "consumer-1");

        // Produce messages
        await producer.ProduceAsync(new Message("TestProducer", "Message 1"));
        await producer.ProduceAsync(new Message("TestProducer", "Message 2"));
        await producer.ProduceAsync(new Message("TestProducer", "Message 3"));

        var receivedMessages = new List<Message>();
        var cts1 = new CancellationTokenSource();

        // When: First consumer reads but doesn't acknowledge (simulated by exception)
        var consumeTask1 = Task.Run(async () =>
        {
            await consumer1.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                if (receivedMessages.Count >= 2)
                {
                    cts1.Cancel(); // Stop consumer after 2 messages
                }
                await Task.CompletedTask;
            }, cts1.Token);
        });

        await Task.Delay(4000); // Wait for consumer to process
        cts1.Cancel();

        try
        {
            await consumeTask1.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Start a new consumer instance (restart scenario)
        var consumer2 = new RedisStreamConsumer(
            _redis!, 
            consumerLogger,
            consumerGroup: "test-group",
            consumerName: "consumer-1"); // Same consumer name

        var cts2 = new CancellationTokenSource();
        var consumeTask2 = Task.Run(async () =>
        {
            await consumer2.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                if (receivedMessages.Count >= 3)
                {
                    cts2.Cancel();
                }
                await Task.CompletedTask;
            }, cts2.Token);
        });

        await Task.Delay(4000);
        cts2.Cancel();

        try
        {
            await consumeTask2.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Then
        receivedMessages.Should().HaveCountGreaterOrEqualTo(3, 
            "because consumer should process all messages including pending ones after restart");
    }

    [Test]
    public async Task Should_HandleMultipleProducers_When_SendingConcurrently()
    {
        // Given
        var logger = _app!.Services.GetRequiredService<ILogger<RedisStreamProducer>>();
        var producer1 = new RedisStreamProducer(_redis!, logger, "test-stream");
        var producer2 = new RedisStreamProducer(_redis!, logger, "test-stream");

        // When: Multiple producers send messages concurrently
        var tasks = new List<Task>();
        for (int i = 1; i <= 3; i++)
        {
            var messageNumber = i;
            tasks.Add(Task.Run(async () =>
            {
                await producer1.ProduceAsync(new Message("Producer1", $"P1-Message-{messageNumber}"));
            }));
            
            tasks.Add(Task.Run(async () =>
            {
                await producer2.ProduceAsync(new Message("Producer2", $"P2-Message-{messageNumber}"));
            }));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(1000);

        // Then
        var db = _redis!.GetDatabase();
        var streamInfo = await db.StreamInfoAsync("test-stream");
        
        streamInfo.Length.Should().BeGreaterOrEqualTo(6, 
            "because 6 messages were sent from 2 producers");
    }

    [Test]
    public async Task Should_UseProtobufSerialization_When_SendingMessages()
    {
        // Given
        var logger = _app!.Services.GetRequiredService<ILogger<RedisStreamProducer>>();
        var producer = new RedisStreamProducer(_redis!, logger, "proto-test-stream");

        var message = new Message("TestProducer", "Protobuf test content");

        // When
        await producer.ProduceAsync(message);
        await Task.Delay(1000);

        // Then: Read raw message from stream and verify it's protobuf
        var db = _redis!.GetDatabase();
        var entries = await db.StreamReadAsync("proto-test-stream", "0-0", count: 1);

        entries.Should().HaveCount(1);
        
        var dataField = entries[0].Values.FirstOrDefault(v => v.Name == "data");
        dataField.Value.Should().NotBeNull();

        var payload = (byte[])dataField.Value!;
        payload.Should().NotBeEmpty();

        // Verify it's valid protobuf by parsing
        var eventMessage = EventMessage.Parser.ParseFrom(payload);
        eventMessage.Producer.Should().Be("TestProducer");
        eventMessage.Content.Should().Be("Protobuf test content");
        eventMessage.CreatedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Should_AcknowledgeMessages_When_ProcessedSuccessfully()
    {
        // Given
        var logger = _app!.Services.GetRequiredService<ILogger<RedisStreamProducer>>();
        var producer = new RedisStreamProducer(_redis!, logger, "ack-test-stream");

        var consumerLogger = _app.Services.GetRequiredService<ILogger<RedisStreamConsumer>>();
        var consumer = new RedisStreamConsumer(
            _redis!, 
            consumerLogger,
            consumerGroup: "ack-test-group",
            consumerName: "ack-consumer",
            streamKey: "ack-test-stream");

        // When
        await producer.ProduceAsync(new Message("TestProducer", "Test acknowledgment"));

        var cts = new CancellationTokenSource();
        var consumeTask = Task.Run(async () =>
        {
            await consumer.ConsumeAsync(async (msg, ct) =>
            {
                // Message processed successfully
                cts.Cancel();
                await Task.CompletedTask;
            }, cts.Token);
        });

        await Task.Delay(3000);
        cts.Cancel();

        try
        {
            await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Then: Check pending messages (should be 0 as message was acknowledged)
        await Task.Delay(1000);
        var db = _redis!.GetDatabase();
        var pending = await db.StreamPendingAsync("ack-test-stream", "ack-test-group");

        pending.PendingMessageCount.Should().Be(0, 
            "because the message should have been acknowledged");
    }
}
