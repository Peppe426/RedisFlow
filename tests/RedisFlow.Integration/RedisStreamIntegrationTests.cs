using FluentAssertions;
using MessagePack;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services;
using StackExchange.Redis;

namespace RedisFlow.Integration;

[TestFixture]
public class RedisStreamIntegrationTests : RedisIntegrationTestBase
{
    [Test]
    public async Task Should_ProduceMessageToStream_When_ProducerEmitsMessage()
    {
        // Given
        var producer = new RedisProducer(Redis, StreamKey);
        var message = new Message("test-producer", "Hello from integration test");

        // When
        await producer.ProduceAsync(message);

        // Then
        var database = Redis.GetDatabase();
        var streamLength = await database.StreamLengthAsync(StreamKey);
        streamLength.Should().Be(1, "because one message was produced");
    }

    [Test]
    public async Task Should_ConsumeMessage_When_ProducerEmitsMessage()
    {
        // Given
        var producer = new RedisProducer(Redis, StreamKey);
        var consumer = new RedisConsumer(Redis, StreamKey, "test-group", "test-consumer");
        var expectedMessage = new Message("test-producer", "Test message for consumption");
        
        Message? receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();

        // When
        await producer.ProduceAsync(expectedMessage);

        var consumeTask = Task.Run(async () =>
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await consumer.ConsumeAsync(async (msg, ct) =>
            {
                receivedMessage = msg;
                messageReceived.SetResult(true);
                cts.Cancel();
                await Task.CompletedTask;
            }, cts.Token);
        });

        await Task.WhenAny(messageReceived.Task, Task.Delay(TimeSpan.FromSeconds(6)));

        // Then
        receivedMessage.Should().NotBeNull("because the consumer should receive the message");
        receivedMessage!.Producer.Should().Be(expectedMessage.Producer, "because the producer name should match");
        receivedMessage.Content.Should().Be(expectedMessage.Content, "because the content should match");
    }

    [Test]
    public async Task Should_SerializeAndDeserializeCorrectly_When_UsingMessagePack()
    {
        // Given
        var producer = new RedisProducer(Redis, StreamKey);
        var originalMessage = new Message("test-producer", "Testing MessagePack serialization")
        {
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        // When
        await producer.ProduceAsync(originalMessage);

        var database = Redis.GetDatabase();
        var streamEntries = await database.StreamReadAsync(StreamKey, "0-0", count: 1);
        var entry = streamEntries.First();
        var dataField = entry.Values.First(v => v.Name == "data");
        var messageBytes = (byte[])dataField.Value!;
        var deserializedMessage = MessagePackSerializer.Deserialize<Message>(messageBytes);

        // Then
        deserializedMessage.Should().NotBeNull("because deserialization should succeed");
        deserializedMessage.Producer.Should().Be(originalMessage.Producer, "because MessagePack should preserve the producer");
        deserializedMessage.Content.Should().Be(originalMessage.Content, "because MessagePack should preserve the content");
        deserializedMessage.CreatedAt.Should().Be(originalMessage.CreatedAt, "because MessagePack should preserve the timestamp");
    }

    [Test]
    public async Task Should_AcknowledgeMessage_When_ConsumerProcessesSuccessfully()
    {
        // Given
        var producer = new RedisProducer(Redis, StreamKey);
        var consumer = new RedisConsumer(Redis, StreamKey, "test-ack-group", "test-ack-consumer");
        var message = new Message("test-producer", "Message to acknowledge");
        
        var messageProcessed = new TaskCompletionSource<bool>();

        // When
        await producer.ProduceAsync(message);

        var consumeTask = Task.Run(async () =>
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await consumer.ConsumeAsync(async (msg, ct) =>
            {
                messageProcessed.SetResult(true);
                cts.Cancel();
                await Task.CompletedTask;
            }, cts.Token);
        });

        await Task.WhenAny(messageProcessed.Task, Task.Delay(TimeSpan.FromSeconds(6)));

        // Then
        var database = Redis.GetDatabase();
        var pendingMessages = await database.StreamPendingMessagesAsync(
            StreamKey,
            "test-ack-group",
            count: 10,
            consumerName: RedisValue.Null);

        pendingMessages.Length.Should().Be(0, "because the message should be acknowledged and removed from pending list");
    }

    [Test]
    public async Task Should_ReplayPendingMessages_When_ConsumerRestarts()
    {
        // Given
        var producer = new RedisProducer(Redis, StreamKey);
        var consumer1 = new RedisConsumer(Redis, StreamKey, "test-replay-group", "consumer-1");
        var message = new Message("test-producer", "Message to replay");

        await producer.ProduceAsync(message);

        // Simulate consumer failure: start consuming but don't acknowledge
        var cts1 = new CancellationTokenSource();
        var firstConsumerStarted = new TaskCompletionSource<bool>();

        var consume1Task = Task.Run(async () =>
        {
            await consumer1.ConsumeAsync(async (msg, ct) =>
            {
                firstConsumerStarted.SetResult(true);
                // Simulate processing but cancel before acknowledging
                await Task.Delay(100, ct);
            }, cts1.Token);
        });

        await firstConsumerStarted.Task;
        cts1.Cancel(); // Simulate consumer crash

        try
        {
            await consume1Task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await Task.Delay(200); // Give time for the consumer to stop

        // When - Start a new consumer that should replay pending messages
        var consumer2 = new RedisConsumer(Redis, StreamKey, "test-replay-group", "consumer-2");
        Message? replayedMessage = null;
        var messageReplayed = new TaskCompletionSource<bool>();

        var consume2Task = Task.Run(async () =>
        {
            var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await consumer2.ConsumeAsync(async (msg, ct) =>
            {
                replayedMessage = msg;
                messageReplayed.SetResult(true);
                cts2.Cancel();
                await Task.CompletedTask;
            }, cts2.Token);
        });

        await Task.WhenAny(messageReplayed.Task, Task.Delay(TimeSpan.FromSeconds(6)));

        // Then
        replayedMessage.Should().NotBeNull("because pending messages should be replayed");
        replayedMessage!.Content.Should().Be(message.Content, "because the replayed message should match the original");
    }

    [Test]
    public async Task Should_ContinueConsuming_When_ProducerGoesOffline()
    {
        // Given
        var producer = new RedisProducer(Redis, StreamKey);
        var consumer = new RedisConsumer(Redis, StreamKey, "test-offline-group", "test-offline-consumer");
        
        var messagesReceived = new List<Message>();
        var firstMessageReceived = new TaskCompletionSource<bool>();
        var secondMessageReceived = new TaskCompletionSource<bool>();

        // Start consumer
        var consumeTask = Task.Run(async () =>
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await consumer.ConsumeAsync(async (msg, ct) =>
            {
                messagesReceived.Add(msg);
                if (messagesReceived.Count == 1)
                    firstMessageReceived.SetResult(true);
                else if (messagesReceived.Count == 2)
                {
                    secondMessageReceived.SetResult(true);
                    cts.Cancel();
                }
                await Task.CompletedTask;
            }, cts.Token);
        });

        // When
        await producer.ProduceAsync(new Message("producer-1", "Message before offline"));
        await firstMessageReceived.Task;

        // Simulate producer going offline (just stop producing)
        await Task.Delay(500);

        // Producer comes back online
        await producer.ProduceAsync(new Message("producer-1", "Message after offline"));
        
        await Task.WhenAny(secondMessageReceived.Task, Task.Delay(TimeSpan.FromSeconds(6)));

        // Then
        messagesReceived.Should().HaveCount(2, "because consumer should continue consuming despite producer offline period");
        messagesReceived[0].Content.Should().Be("Message before offline");
        messagesReceived[1].Content.Should().Be("Message after offline");
    }

    [Test]
    public async Task Should_HandleMultipleProducers_When_EmittingConcurrently()
    {
        // Given
        var producer1 = new RedisProducer(Redis, StreamKey);
        var producer2 = new RedisProducer(Redis, StreamKey);
        var consumer = new RedisConsumer(Redis, StreamKey, "test-multi-group", "test-multi-consumer");
        
        var messagesReceived = new List<Message>();
        var allMessagesReceived = new TaskCompletionSource<bool>();

        // When
        var consumeTask = Task.Run(async () =>
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await consumer.ConsumeAsync(async (msg, ct) =>
            {
                messagesReceived.Add(msg);
                if (messagesReceived.Count >= 4)
                {
                    allMessagesReceived.SetResult(true);
                    cts.Cancel();
                }
                await Task.CompletedTask;
            }, cts.Token);
        });

        // Produce messages from multiple producers concurrently
        await Task.WhenAll(
            producer1.ProduceAsync(new Message("producer-1", "Message 1 from producer 1")),
            producer1.ProduceAsync(new Message("producer-1", "Message 2 from producer 1")),
            producer2.ProduceAsync(new Message("producer-2", "Message 1 from producer 2")),
            producer2.ProduceAsync(new Message("producer-2", "Message 2 from producer 2"))
        );

        await Task.WhenAny(allMessagesReceived.Task, Task.Delay(TimeSpan.FromSeconds(11)));

        // Then
        messagesReceived.Should().HaveCount(4, "because all messages from both producers should be consumed");
        messagesReceived.Should().Contain(m => m.Producer == "producer-1" && m.Content.Contains("producer 1"));
        messagesReceived.Should().Contain(m => m.Producer == "producer-2" && m.Content.Contains("producer 2"));
    }
}
