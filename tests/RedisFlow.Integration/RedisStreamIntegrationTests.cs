using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;
using StackExchange.Redis;

namespace RedisFlow.Integration;

[TestFixture]
public class RedisStreamIntegrationTests
{
    private DistributedApplication? _app;
    private IConnectionMultiplexer? _redis;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Given - Start the Aspire host with Redis
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.RedisFlow_AppHost>();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Get Redis connection string from the resource
        var connectionString = await _app.GetConnectionStringAsync("redis");
        connectionString.Should().NotBeNullOrEmpty("because Redis should be provisioned");

        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString!);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        // Clean up resources
        _redis?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    [Test]
    public async Task Should_ConnectToRedis_When_AspireHostIsRunning()
    {
        // Given
        var db = _redis!.GetDatabase();

        // When
        var result = await db.PingAsync();

        // Then
        result.TotalMilliseconds.Should().BeGreaterThan(0, "because ping should return a valid response time");
    }

    [Test]
    public async Task Should_CreateStream_When_AddingFirstMessage()
    {
        // Given
        var db = _redis!.GetDatabase();
        var streamKey = $"test-stream-{Guid.NewGuid()}";
        var messageData = new NameValueEntry[]
        {
            new("producer", "test-producer"),
            new("message", "hello world"),
            new("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
        };

        // When
        var messageId = await db.StreamAddAsync(streamKey, messageData);

        // Then
        messageId.Should().NotBeNull("because StreamAdd should return a message ID");
        var streamLength = await db.StreamLengthAsync(streamKey);
        streamLength.Should().Be(1, "because we added one message to the stream");

        // Cleanup
        await db.KeyDeleteAsync(streamKey);
    }

    [Test]
    public async Task Should_ConsumeMessages_When_UsingConsumerGroup()
    {
        // Given
        var db = _redis!.GetDatabase();
        var streamKey = $"test-stream-{Guid.NewGuid()}";
        var groupName = "test-group";
        var consumerName = "test-consumer";

        // Add a message to the stream
        var messageData = new NameValueEntry[]
        {
            new("producer", "test-producer"),
            new("message", "test message"),
            new("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
        };
        var messageId = await db.StreamAddAsync(streamKey, messageData);

        // Create consumer group
        await db.StreamCreateConsumerGroupAsync(streamKey, groupName, StreamPosition.Beginning);

        // When - Read from the stream using consumer group
        var messages = await db.StreamReadGroupAsync(streamKey, groupName, consumerName, StreamPosition.NewMessages, 1);

        // Then
        messages.Should().NotBeEmpty("because we added a message to the stream");
        messages.Length.Should().Be(1, "because we requested 1 message");
        messages[0].Id.Should().Be(messageId, "because we should read the message we added");

        // Acknowledge the message
        var ackCount = await db.StreamAcknowledgeAsync(streamKey, groupName, messageId);
        ackCount.Should().Be(1, "because we acknowledged one message");

        // Cleanup
        await db.KeyDeleteAsync(streamKey);
    }

    [Test]
    public async Task Should_ReplayPendingMessages_When_ConsumerRestarts()
    {
        // Given
        var db = _redis!.GetDatabase();
        var streamKey = $"test-stream-{Guid.NewGuid()}";
        var groupName = "test-group";
        var consumerName = "test-consumer";

        // Add a message to the stream
        var messageData = new NameValueEntry[]
        {
            new("producer", "test-producer"),
            new("message", "pending message"),
            new("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
        };
        var messageId = await db.StreamAddAsync(streamKey, messageData);

        // Create consumer group and read message (but don't acknowledge)
        await db.StreamCreateConsumerGroupAsync(streamKey, groupName, StreamPosition.Beginning);
        await db.StreamReadGroupAsync(streamKey, groupName, consumerName, StreamPosition.NewMessages, 1);

        // When - Check pending messages
        var pendingInfo = await db.StreamPendingAsync(streamKey, groupName);

        // Then
        pendingInfo.Should().NotBeNull("because there should be pending messages");
        pendingInfo.PendingMessageCount.Should().Be(1, "because we read but didn't acknowledge one message");

        // Read pending messages again (simulate consumer restart)
        var pendingMessages = await db.StreamReadGroupAsync(
            streamKey, 
            groupName, 
            consumerName, 
            StreamPosition.Beginning, 
            1);

        pendingMessages.Should().NotBeEmpty("because we should be able to replay pending messages");
        pendingMessages[0].Id.Should().Be(messageId, "because we should read the same pending message");

        // Cleanup
        await db.KeyDeleteAsync(streamKey);
    }
}
