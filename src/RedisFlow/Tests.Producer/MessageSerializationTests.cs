using FluentAssertions;
using RedisFlow.Domain.Messages;

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
        var protoMessage = domainMessage.ToProto();

        // Then
        protoMessage.Should().NotBeNull("because conversion should produce a valid protobuf message");
        protoMessage.Producer.Should().Be(domainMessage.Producer, "because the producer should be converted correctly");
        protoMessage.Content.Should().Be(domainMessage.Content, "because the content should be converted correctly");
    }

    [Test]
    public void Should_ConvertToDomain_When_ProtoMessageIsProvided()
    {
        // Given
        var protoMessage = new RedisFlow.Domain.Messages.Message
        {
            Producer = "Producer1",
            Content = "Test content",
            CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        };

        // When
        var domainMessage = protoMessage.ToDomain();

        // Then
        domainMessage.Should().NotBeNull("because conversion should produce a valid domain message");
        domainMessage.Producer.Should().Be(protoMessage.Producer, "because the producer should be converted correctly");
        domainMessage.Content.Should().Be(protoMessage.Content, "because the content should be converted correctly");
    }
}
