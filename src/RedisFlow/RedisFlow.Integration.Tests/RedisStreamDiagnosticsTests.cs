using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;
using StackExchange.Redis;

namespace RedisFlow.Integration.Tests;

/// <summary>
/// Integration tests demonstrating Redis stream inspection and diagnostics capabilities.
/// These tests verify that the developer tooling can properly interact with Redis streams.
/// 
/// NOTE: These tests require Aspire DCP to be available and are designed to run in a local
/// development environment. They are marked as [Explicit] to prevent them from running
/// in CI/CD pipelines. To run these tests:
/// 1. Ensure Docker is running
/// 2. Run: dotnet test --filter "Category=Integration&FullyQualifiedName~RedisStreamDiagnosticsTests"
/// </summary>
[TestFixture]
[Category("Integration")]
[Explicit("Requires Aspire DCP and Docker - run manually in local environment")]
public class RedisStreamDiagnosticsTests
{
    private DistributedApplication? _app;
    private IConnectionMultiplexer? _redis;
    private const string TestStreamName = "test-stream";
    private const string TestGroupName = "test-group";
    private const string TestConsumerName = "test-consumer";

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        // Given: Aspire app with Redis is running
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.RedisFlow_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Connect to Redis
        var redisEndpoint = _app.GetEndpoint("redis");
        var connectionString = $"{redisEndpoint.Host}:{redisEndpoint.Port}";
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
    }

    [OneTimeTearDown]
    public async Task OneTimeTeardown()
    {
        // Cleanup
        _redis?.Dispose();
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    [SetUp]
    public async Task Setup()
    {
        // Clean up test data before each test
        var db = _redis!.GetDatabase();
        await db.KeyDeleteAsync(TestStreamName);
    }

    [Test]
    public async Task Should_CreateAndInspectStream_When_MessagesProduced()
    {
        // Given
        var db = _redis!.GetDatabase();

        // When: Produce some messages to the stream
        var messageId1 = await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("producer", "test-producer"), new("content", "message 1") });
        var messageId2 = await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("producer", "test-producer"), new("content", "message 2") });

        // Then: Stream should be inspectable
        var streamLength = await db.StreamLengthAsync(TestStreamName);
        streamLength.Should().Be(2, "because we added 2 messages");

        var entries = await db.StreamRangeAsync(TestStreamName);
        entries.Should().HaveCount(2, "because we added 2 messages");
        entries[0].Id.Should().Be(messageId1);
        entries[1].Id.Should().Be(messageId2);
    }

    [Test]
    public async Task Should_RetrieveStreamInfo_When_UsingXINFO()
    {
        // Given: Stream with messages
        var db = _redis!.GetDatabase();
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("producer", "test-producer"), new("content", "test message") });

        // When: Execute XINFO STREAM command
        var info = await db.ExecuteAsync("XINFO", "STREAM", TestStreamName);

        // Then: Should return stream information
        info.IsNull.Should().BeFalse("because the stream exists");
        info.Resp2Type.Should().Be(ResultType.Array, "because XINFO returns array of key-value pairs");
    }

    [Test]
    public async Task Should_ListConsumerGroups_When_GroupsExist()
    {
        // Given: Stream with consumer group
        var db = _redis!.GetDatabase();
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("producer", "test-producer"), new("content", "message 1") });
        
        // Create consumer group starting from beginning
        await db.ExecuteAsync("XGROUP", "CREATE", TestStreamName, TestGroupName, "0", "MKSTREAM");

        // When: List consumer groups
        var groups = await db.ExecuteAsync("XINFO", "GROUPS", TestStreamName);

        // Then: Should find the created group
        groups.IsNull.Should().BeFalse("because we created a consumer group");
        groups.Resp2Type.Should().Be(ResultType.Array, "because XINFO GROUPS returns an array");

        var groupsArray = (RedisResult[])groups!;
        groupsArray.Should().NotBeEmpty("because we created at least one group");
    }

    [Test]
    public async Task Should_InspectPendingMessages_When_MessagesPending()
    {
        // Given: Stream with consumer group and pending message
        var db = _redis!.GetDatabase();
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("producer", "test-producer"), new("content", "pending message") });
        await db.ExecuteAsync("XGROUP", "CREATE", TestStreamName, TestGroupName, "0", "MKSTREAM");

        // Read message without acknowledging (creates pending entry)
        var messages = await db.StreamReadGroupAsync(TestStreamName, TestGroupName, TestConsumerName, ">", 1);
        messages.Should().NotBeEmpty("because there's a message in the stream");

        // When: Check pending messages
        var pending = await db.ExecuteAsync("XPENDING", TestStreamName, TestGroupName);

        // Then: Should show pending message
        pending.IsNull.Should().BeFalse("because there's a pending message");
        pending.Resp2Type.Should().Be(ResultType.Array, "because XPENDING returns an array");

        var pendingInfo = (RedisResult[])pending!;
        pendingInfo.Should().HaveCountGreaterOrEqualTo(4, "because XPENDING returns at least 4 elements");

        var pendingCount = long.Parse(pendingInfo[0].ToString()!);
        pendingCount.Should().BeGreaterThan(0, "because we have an unacknowledged message");
    }

    [Test]
    public async Task Should_ViewStreamEntries_When_UsingXRANGE()
    {
        // Given: Stream with multiple messages
        var db = _redis!.GetDatabase();
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("field1", "value1") });
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("field2", "value2") });
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("field3", "value3") });

        // When: Query entries with XRANGE
        var entries = await db.StreamRangeAsync(TestStreamName, "-", "+");

        // Then: Should retrieve all entries
        entries.Should().HaveCount(3, "because we added 3 messages");
        entries[0].Values.Should().ContainSingle(v => v.Name == "field1" && v.Value == "value1");
        entries[1].Values.Should().ContainSingle(v => v.Name == "field2" && v.Value == "value2");
        entries[2].Values.Should().ContainSingle(v => v.Name == "field3" && v.Value == "value3");
    }

    [Test]
    public async Task Should_ViewLatestEntries_When_UsingXREVRANGE()
    {
        // Given: Stream with messages
        var db = _redis!.GetDatabase();
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("seq", "1") });
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("seq", "2") });
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("seq", "3") });

        // When: Query latest entries
        var entries = await db.StreamRangeAsync(TestStreamName, "+", "-", count: 2, messageOrder: Order.Descending);

        // Then: Should get latest 2 entries in reverse order
        entries.Should().HaveCount(2, "because we requested 2 entries");
        entries[0].Values[0].Value.Should().Be("3", "because it's the latest message");
        entries[1].Values[0].Value.Should().Be("2", "because it's the second latest message");
    }

    [Test]
    public async Task Should_ListConsumersInGroup_When_ConsumersActive()
    {
        // Given: Stream with consumer group and active consumer
        var db = _redis!.GetDatabase();
        await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("producer", "test-producer"), new("content", "message") });
        await db.ExecuteAsync("XGROUP", "CREATE", TestStreamName, TestGroupName, "0", "MKSTREAM");

        // Consumer reads a message (becomes active)
        await db.StreamReadGroupAsync(TestStreamName, TestGroupName, TestConsumerName, ">");

        // When: List consumers in the group
        var consumers = await db.ExecuteAsync("XINFO", "CONSUMERS", TestStreamName, TestGroupName);

        // Then: Should find our consumer
        consumers.IsNull.Should().BeFalse("because we have an active consumer");
        consumers.Resp2Type.Should().Be(ResultType.Array, "because XINFO CONSUMERS returns an array");

        var consumersArray = (RedisResult[])consumers!;
        consumersArray.Should().NotBeEmpty("because we created a consumer");
    }

    [Test]
    public async Task Should_DetectNoStreams_When_DatabaseEmpty()
    {
        // Given: Empty database (cleaned in setup)
        var db = _redis!.GetDatabase();

        // When: Check stream length
        var length = await db.StreamLengthAsync(TestStreamName);

        // Then: Should return 0
        length.Should().Be(0, "because the stream doesn't exist or is empty");
    }

    [Test]
    public async Task Should_HandleConsumerRestart_When_PendingMessagesExist()
    {
        // Given: Stream with pending message from a consumer
        var db = _redis!.GetDatabase();
        var messageId = await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("data", "important") });
        await db.ExecuteAsync("XGROUP", "CREATE", TestStreamName, TestGroupName, "0", "MKSTREAM");

        // Consumer reads but doesn't acknowledge
        var messages = await db.StreamReadGroupAsync(TestStreamName, TestGroupName, TestConsumerName, ">", 1);
        messages.Should().HaveCount(1);

        // When: Same consumer restarts and claims pending messages
        var pending = await db.ExecuteAsync("XPENDING", TestStreamName, TestGroupName, "-", "+", 10, TestConsumerName);
        pending.IsNull.Should().BeFalse("because there's a pending message");

        // Then: Consumer can process pending message
        var pendingMessages = await db.StreamReadGroupAsync(TestStreamName, TestGroupName, TestConsumerName, "0", 10);
        pendingMessages.Should().HaveCount(1, "because the pending message is available");
        pendingMessages[0].Id.Should().Be(messageId);

        // Acknowledge the message
        var acked = await db.StreamAcknowledgeAsync(TestStreamName, TestGroupName, messageId);
        acked.Should().Be(1, "because we acknowledged 1 message");

        // Verify no more pending
        var pendingAfter = await db.ExecuteAsync("XPENDING", TestStreamName, TestGroupName);
        var pendingInfoAfter = (RedisResult[])pendingAfter!;
        var pendingCountAfter = long.Parse(pendingInfoAfter[0].ToString()!);
        pendingCountAfter.Should().Be(0, "because we acknowledged the pending message");
    }

    [Test]
    public async Task Should_MonitorStreamGrowth_When_ContinuouslyProducing()
    {
        // Given: Empty stream
        var db = _redis!.GetDatabase();
        var initialLength = await db.StreamLengthAsync(TestStreamName);
        initialLength.Should().Be(0);

        // When: Continuously produce messages
        for (int i = 0; i < 10; i++)
        {
            await db.StreamAddAsync(TestStreamName, new NameValueEntry[] { new("counter", i.ToString()) });
        }

        // Then: Stream length should increase
        var finalLength = await db.StreamLengthAsync(TestStreamName);
        finalLength.Should().Be(10, "because we added 10 messages");

        // And we can monitor the latest entry
        var latestEntry = await db.StreamRangeAsync(TestStreamName, "+", "-", 1, Order.Descending);
        latestEntry.Should().HaveCount(1);
        latestEntry[0].Values[0].Value.Should().Be("9", "because the last counter value was 9");
    }
}
