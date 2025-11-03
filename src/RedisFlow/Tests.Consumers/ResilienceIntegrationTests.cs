using FluentAssertions;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services;
using StackExchange.Redis;
using TestBase;

namespace Tests.Consumers;

[TestFixture]
[Category("Integration test")]
public class ResilienceIntegrationTests : UnitTest
{
    private IConnectionMultiplexer? _redis;
    private const string StreamName = "test-stream";
    private const string ConsumerGroup = "test-group";

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        // Connect to Redis (assumes Redis is running locally or via Aspire)
        _redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
        
        // Clean up any existing test data
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(StreamName);
    }

    [OneTimeTearDown]
    public async Task OneTimeTeardown()
    {
        if (_redis != null)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(StreamName);
            await _redis.DisposeAsync();
        }
    }

    [SetUp]
    public async Task TestSetup()
    {
        // Clean stream before each test
        if (_redis != null)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(StreamName);
        }
    }

    [Test]
    public async Task Should_ContinueProcessing_When_OneProducerGoesOffline()
    {
        // Given
        var producer1 = new RedisProducer(_redis!, StreamName);
        var producer2 = new RedisProducer(_redis!, StreamName);
        var receivedMessages = new List<Message>();
        var consumer = new RedisConsumer(_redis!, StreamName, ConsumerGroup, "consumer1");
        
        var cts = new CancellationTokenSource();

        // Start consumer in background
        var consumerTask = Task.Run(async () =>
        {
            await consumer.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }, cts.Token);
        }, cts.Token);

        // Producer 1 sends messages
        await producer1.ProduceAsync(new Message("Producer1", "Message1"), CancellationToken.None);
        await producer1.ProduceAsync(new Message("Producer1", "Message2"), CancellationToken.None);
        
        await Task.Delay(500);

        // When - Producer 1 "goes offline" (stops sending), but Producer 2 continues
        await producer2.ProduceAsync(new Message("Producer2", "Message3"), CancellationToken.None);
        await producer2.ProduceAsync(new Message("Producer2", "Message4"), CancellationToken.None);
        
        await Task.Delay(500);

        // Then - All messages should be processed
        cts.Cancel();
        try { await consumerTask; } catch (OperationCanceledException) { }

        receivedMessages.Should().HaveCount(4, "because all messages should be processed regardless of producer status");
        receivedMessages.Should().Contain(m => m.Producer == "Producer1" && m.Content == "Message1");
        receivedMessages.Should().Contain(m => m.Producer == "Producer1" && m.Content == "Message2");
        receivedMessages.Should().Contain(m => m.Producer == "Producer2" && m.Content == "Message3");
        receivedMessages.Should().Contain(m => m.Producer == "Producer2" && m.Content == "Message4");
    }

    [Test]
    public async Task Should_ReprocessPendingMessages_When_ConsumerRestarts()
    {
        // Given
        var producer = new RedisProducer(_redis!, StreamName);
        var receivedMessages = new List<Message>();
        
        // Send messages
        await producer.ProduceAsync(new Message("Producer1", "Message1"), CancellationToken.None);
        await producer.ProduceAsync(new Message("Producer1", "Message2"), CancellationToken.None);
        await producer.ProduceAsync(new Message("Producer1", "Message3"), CancellationToken.None);

        // Start consumer that processes some but not all messages (simulating crash)
        var consumer1 = new RedisConsumer(_redis!, StreamName, ConsumerGroup, "consumer1");
        var cts1 = new CancellationTokenSource();
        var processedCount = 0;

        var consumerTask1 = Task.Run(async () =>
        {
            await consumer1.ConsumeAsync(async (msg, ct) =>
            {
                processedCount++;
                if (processedCount >= 2)
                {
                    // Simulate crash before acknowledging the third message
                    cts1.Cancel();
                    throw new OperationCanceledException();
                }
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }, cts1.Token);
        }, cts1.Token);

        try { await consumerTask1; } catch (OperationCanceledException) { }
        await Task.Delay(500);

        // When - Consumer restarts with the same consumer name
        var consumer2 = new RedisConsumer(_redis!, StreamName, ConsumerGroup, "consumer1");
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var consumerTask2 = Task.Run(async () =>
        {
            await consumer2.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }, cts2.Token);
        }, cts2.Token);

        try { await consumerTask2; } catch (OperationCanceledException) { }

        // Then - All messages including pending ones should be processed
        receivedMessages.Should().HaveCount(3, "because all messages including pending ones should be processed");
        receivedMessages.Should().Contain(m => m.Content == "Message1");
        receivedMessages.Should().Contain(m => m.Content == "Message2");
        receivedMessages.Should().Contain(m => m.Content == "Message3");
    }

    [Test]
    public async Task Should_HandleCombinedScenario_When_ProducerOfflineAndConsumerRestarts()
    {
        // Given
        var producer1 = new RedisProducer(_redis!, StreamName);
        var producer2 = new RedisProducer(_redis!, StreamName);
        var receivedMessages = new List<Message>();
        
        // Producer 1 sends initial messages
        await producer1.ProduceAsync(new Message("Producer1", "InitialMessage1"), CancellationToken.None);
        await producer1.ProduceAsync(new Message("Producer1", "InitialMessage2"), CancellationToken.None);

        // Start first consumer
        var consumer1 = new RedisConsumer(_redis!, StreamName, ConsumerGroup, "consumer1");
        var cts1 = new CancellationTokenSource();
        var processedCount = 0;

        var consumerTask1 = Task.Run(async () =>
        {
            await consumer1.ConsumeAsync(async (msg, ct) =>
            {
                processedCount++;
                if (processedCount >= 1)
                {
                    // Simulate consumer crash after first message
                    cts1.Cancel();
                    throw new OperationCanceledException();
                }
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }, cts1.Token);
        }, cts1.Token);

        try { await consumerTask1; } catch (OperationCanceledException) { }
        await Task.Delay(500);

        // When - Producer 1 goes offline, Producer 2 continues sending while consumer is down
        await producer2.ProduceAsync(new Message("Producer2", "NewMessage1"), CancellationToken.None);
        await producer2.ProduceAsync(new Message("Producer2", "NewMessage2"), CancellationToken.None);

        // Consumer restarts
        var consumer2 = new RedisConsumer(_redis!, StreamName, ConsumerGroup, "consumer1");
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var consumerTask2 = Task.Run(async () =>
        {
            await consumer2.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }, cts2.Token);
        }, cts2.Token);

        try { await consumerTask2; } catch (OperationCanceledException) { }

        // Then - All messages should be processed
        receivedMessages.Should().HaveCount(4, "because all messages should be processed after consumer restart");
        receivedMessages.Should().Contain(m => m.Producer == "Producer1");
        receivedMessages.Should().Contain(m => m.Producer == "Producer2");
    }

    [Test]
    public async Task Should_ProvideStreamMetrics_When_ProducerOffline()
    {
        // Given
        var producer1 = new RedisProducer(_redis!, StreamName);
        var producer2 = new RedisProducer(_redis!, StreamName);
        
        // When - Both producers send messages
        await producer1.ProduceAsync(new Message("Producer1", "Message1"), CancellationToken.None);
        await producer2.ProduceAsync(new Message("Producer2", "Message2"), CancellationToken.None);
        
        // Producer1 goes offline, Producer2 continues
        await producer2.ProduceAsync(new Message("Producer2", "Message3"), CancellationToken.None);
        
        // Then - Stream should contain all messages
        var db = _redis!.GetDatabase();
        var streamInfo = await db.StreamInfoAsync(StreamName);
        
        streamInfo.Length.Should().Be(3, "because all messages from both producers should be in the stream");
    }

    [Test]
    public async Task Should_RecoverPendingMessages_When_ConsumerRestartsWithDifferentName()
    {
        // Given
        var producer = new RedisProducer(_redis!, StreamName);
        await producer.ProduceAsync(new Message("Producer1", "Message1"), CancellationToken.None);
        await producer.ProduceAsync(new Message("Producer1", "Message2"), CancellationToken.None);

        // Consumer1 reads but doesn't acknowledge (simulating crash)
        var consumer1 = new RedisConsumer(_redis!, StreamName, ConsumerGroup, "consumer1");
        var cts1 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        _ = Task.Run(async () =>
        {
            await consumer1.ConsumeAsync(async (msg, ct) =>
            {
                // Don't process, just read
                await Task.Delay(10000, ct); // Prevent acknowledgment
            }, cts1.Token);
        }, cts1.Token);

        try { await Task.Delay(1000); } catch { }
        
        // When - Different consumer joins the same group
        var receivedMessages = new List<Message>();
        var consumer2 = new RedisConsumer(_redis!, StreamName, ConsumerGroup, "consumer2");
        var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var consumerTask2 = Task.Run(async () =>
        {
            await consumer2.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessages.Add(msg);
                await Task.CompletedTask;
            }, cts2.Token);
        }, cts2.Token);

        try { await consumerTask2; } catch (OperationCanceledException) { }

        // Then - New consumer should claim and process pending messages
        receivedMessages.Should().HaveCount(2, "because pending messages should be claimed by the new consumer");
    }
}
