using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;
using TestBase;

namespace Tests.Producer;

[TestFixture]
public class RedisStreamProducerTests : UnitTest
{
    [Test]
    public void Should_ThrowArgumentNullException_When_RedisIsNull()
    {
        // Given
        var logger = Mock.Of<ILogger<RedisStreamProducer>>();

        // When
        Action act = () => new RedisStreamProducer(null!, logger);

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
        Action act = () => new RedisStreamProducer(redis, null!);

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
        var logger = Mock.Of<ILogger<RedisStreamProducer>>();

        // When
        Action act = () => new RedisStreamProducer(redis, logger, null!);

        // Then
        act.Should()
            .Throw<ArgumentNullException>()
            .WithParameterName("streamKey");
    }

    [Test]
    public async Task Should_ThrowArgumentNullException_When_MessageIsNull()
    {
        // Given
        var redis = Mock.Of<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RedisStreamProducer>>();
        var producer = new RedisStreamProducer(redis, logger);

        // When
        Func<Task> act = async () => await producer.ProduceAsync(null!);

        // Then
        await act.Should()
            .ThrowAsync<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Test]
    public async Task Should_CallStreamAddAsync_When_MessageIsValid()
    {
        // Given
        var mockDb = new Mock<IDatabase>();
        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDb.Object);

        mockDb.Setup(db => db.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>()))
            .ReturnsAsync(new RedisValue("1234-0"));

        var logger = Mock.Of<ILogger<RedisStreamProducer>>();
        var producer = new RedisStreamProducer(mockRedis.Object, logger, "test-stream");
        var message = new Message("test-producer", "test content");

        // When
        await producer.ProduceAsync(message);

        // Then
        mockDb.Verify(db => db.StreamAddAsync(
            It.Is<RedisKey>(k => k == "test-stream"),
            It.Is<NameValueEntry[]>(entries => 
                entries.Length == 1 && 
                entries[0].Name == "data" &&
                entries[0].Value.HasValue)), Times.Once);
    }
}
