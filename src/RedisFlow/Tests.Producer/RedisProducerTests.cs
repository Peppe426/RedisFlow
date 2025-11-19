using FluentAssertions;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Moq;
using RedisFlow.Services.Contracts;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;

namespace Tests.Producer;

[TestFixture]
public class RedisProducerTests
{
    [Test]
    public void Should_ThrowArgumentNullException_When_RedisIsNull()
    {
        // Given
        IConnectionMultiplexer? redis = null;
        var logger = Mock.Of<ILogger<RedisProducer>>();

        // When
        Action act = () => new RedisProducer(redis!, logger);

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
        ILogger<RedisProducer>? logger = null;

        // When
        Action act = () => new RedisProducer(redis, logger!);

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
        var logger = Mock.Of<ILogger<RedisProducer>>();
        string? streamKey = null;

        // When
        Action act = () => new RedisProducer(redis, logger, streamKey!);

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
        var logger = Mock.Of<ILogger<RedisProducer>>();
        var sut = new RedisProducer(redis, logger);
        Message? message = null;

        // When
        Func<Task> act = async () => await sut.ProduceAsync(message!);

        // Then
        await act.Should()
            .ThrowAsync<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Test]
    public async Task Should_CallStreamAddAsync_When_MessageIsValid()
    {
        // Given
        var mockDatabase = new Mock<IDatabase>();
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var logger = Mock.Of<ILogger<RedisProducer>>();
        
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);
        
        mockDatabase.Setup(db => db.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>()))
            .ReturnsAsync("1234567890-0");

        var sut = new RedisProducer(mockRedis.Object, logger, "test:stream");
        var message = new Message("TestProducer", "Test content");

        // When
        await sut.ProduceAsync(message);

        // Then
        mockDatabase.Verify(db => db.StreamAddAsync(
            It.Is<RedisKey>(k => k == "test:stream"),
            It.Is<NameValueEntry[]>(entries => 
                entries.Length == 1 && 
                entries[0].Name == "data" &&
                entries[0].Value.HasValue)), Times.Once);
    }
}
