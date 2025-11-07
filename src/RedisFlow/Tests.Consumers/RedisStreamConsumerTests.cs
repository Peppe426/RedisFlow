using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;
using TestBase;

namespace Tests.Consumers;

[TestFixture]
public class RedisStreamConsumerTests : UnitTest
{
    [Test]
    public void Should_ThrowArgumentNullException_When_RedisIsNull()
    {
        // Given
        var logger = Mock.Of<ILogger<RedisStreamConsumer>>();

        // When
        Action act = () => new RedisStreamConsumer(null!, logger);

        // Then
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("redis");
    }

    [Test]
    public void Should_ThrowArgumentNullException_When_LoggerIsNull()
    {
        // Given
        var redis = Mock.Of<IConnectionMultiplexer>();

        // When
        Action act = () => new RedisStreamConsumer(redis, null!);

        // Then
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Test]
    public void Should_ThrowArgumentNullException_When_StreamKeyIsNull()
    {
        // Given
        var redis = Mock.Of<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RedisStreamConsumer>>();

        // When
        Action act = () => new RedisStreamConsumer(redis, logger, null!);

        // Then
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("streamKey");
    }

    [Test]
    public void Should_ThrowArgumentNullException_When_GroupNameIsNull()
    {
        // Given
        var redis = Mock.Of<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RedisStreamConsumer>>();

        // When
        Action act = () => new RedisStreamConsumer(redis, logger, "stream", null!);

        // Then
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("groupName");
    }

    [Test]
    public async Task Should_ThrowArgumentNullException_When_HandlerIsNull()
    {
        // Given
        var redis = Mock.Of<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RedisStreamConsumer>>();
        var consumer = new RedisStreamConsumer(redis, logger);

        // When
        Func<Task> act = async () => await consumer.ConsumeAsync(null!);

        // Then
        await act.Should()
            .ThrowAsync<ArgumentNullException>()
            .WithParameterName("handler");
    }

    [Test]
    public async Task Should_StopGracefully_When_CancellationIsRequested()
    {
        // Given
        var mockDb = new Mock<IDatabase>();
        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);

        // Setup consumer group creation to succeed
        mockDb.Setup(db => db.StreamCreateConsumerGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Setup to return empty pending messages
        mockDb.Setup(db => db.StreamPendingMessagesAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamPendingMessageInfo>());

        // Setup to return no messages
        mockDb.Setup(db => db.StreamReadGroupAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<StreamEntry>());

        var logger = Mock.Of<ILogger<RedisStreamConsumer>>();
        var consumer = new RedisStreamConsumer(mockRedis.Object, logger);

        var handlerCalled = false;
        Func<Message, CancellationToken, Task> handler = (msg, ct) =>
        {
            handlerCalled = true;
            return Task.CompletedTask;
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // When
        await consumer.ConsumeAsync(handler, cts.Token);

        // Then
        handlerCalled.Should().BeFalse("because no messages were available");
    }
}
