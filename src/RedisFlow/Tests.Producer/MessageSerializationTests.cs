using FluentAssertions;
using RedisFlow.Domain.Messages;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Proto;
using Google.Protobuf.WellKnownTypes;
using System;

namespace Tests.Producer;

[TestFixture]
public class MessageSerializationTests
{
    [Test]
    public void Should_SerializeAndDeserialize_When_MessageIsValid()
    {
        // Given
        var originalMessage = new RedisFlow.Domain.ValueObjects.Message("Producer1", "Hello, Redis!");

        // When
        var bytes = originalMessage.ToBytes();
        var deserializedMessage = MessageExtensions.FromBytes(bytes);

        // Then
        deserializedMessage.Producer.Should().Be(originalMessage.Producer, "because the producer should be preserved during serialization");
        deserializedMessage.Content.Should().Be(originalMessage.Content, "because the content should be preserved during serialization");
        deserializedMessage.CreatedAt.Should().BeCloseTo(originalMessage.CreatedAt, TimeSpan.FromMilliseconds(1), "because the timestamp should be preserved during serialization");
    }

    [Test]
    public void Should_ProduceNonEmptyBytes_When_MessageIsSerialized()
    {
        // Given
        var message = new RedisFlow.Domain.ValueObjects.Message("Producer1", "Test content");

        // When
        var bytes = message.ToBytes();

        // Then
        bytes.Should().NotBeEmpty("because a valid message should produce a non-empty byte array");
        bytes.Length.Should().BeGreaterThan(0, "because the serialized message should have content");
    }

    [Test]
    public void Should_PreserveTimestamp_When_MessageHasSpecificCreatedAt()
    {
        // Given
        var specificTime = new DateTime(2025, 11, 3, 22, 0, 0, DateTimeKind.Utc);
        var message = new RedisFlow.Domain.ValueObjects.Message("Producer1", "Test content", specificTime);

        // When
        var bytes = message.ToBytes();
        var deserializedMessage = MessageExtensions.FromBytes(bytes);

        // Then
        deserializedMessage.CreatedAt.Should().BeCloseTo(specificTime, TimeSpan.FromMilliseconds(1), "because the specific timestamp should be preserved");
    }

    [Test]
    public void Should_HandleEmptyContent_When_MessageContentIsEmpty()
    {
        // Given
        var message = new RedisFlow.Domain.ValueObjects.Message("Producer1", string.Empty);

        // When
        var bytes = message.ToBytes();
        var deserializedMessage = MessageExtensions.FromBytes(bytes);

        // Then
        deserializedMessage.Content.Should().BeEmpty("because empty content should be preserved during serialization");
        deserializedMessage.Producer.Should().Be("Producer1", "because the producer should still be preserved");
    }

    [Test]
    public void Should_ConvertToProto_When_DomainMessageIsProvided()
    {
        // Given
        var domainMessage = new RedisFlow.Domain.ValueObjects.Message("Producer1", "Test content");

        // When
        var protoMessage = RedisFlow.Domain.Messages.MessageExtensions.ToProto(domainMessage);

        // Then
        protoMessage.Should().NotBeNull("because conversion should produce a valid protobuf message");
        protoMessage.Producer.Should().Be(domainMessage.Producer, "because the producer should be converted correctly");
        protoMessage.Content.Should().Be(domainMessage.Content, "because the content should be converted correctly");
    }

    [Test]
    public void Should_ConvertToDomain_When_ProtoMessageIsProvided()
    {
        // Given
        var originalMessage = new RedisFlow.Domain.ValueObjects.Message("Producer1", "Test content");
        var bytes = RedisFlow.Domain.Messages.MessageExtensions.ToBytes(originalMessage);

        // When
        var deserializedMessage = RedisFlow.Domain.Messages.MessageExtensions.FromBytes(bytes);

        // Then
        deserializedMessage.Should().NotBeNull("because deserialization should produce a valid domain message");
        deserializedMessage.Producer.Should().Be(originalMessage.Producer, "because the producer should be preserved during serialization");
        deserializedMessage.Content.Should().Be(originalMessage.Content, "because the content should be preserved during serialization");
    }
}
