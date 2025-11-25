using System;
using System.Text;
using System.Xml.Serialization;
using FluentAssertions;
using NUnit.Framework;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Extensions;
using RedisFlow.Domain.Proto;

namespace Tests.Domain;

[TestFixture]
public class MessageExtensionsTests
{
    [Test]
    public void Should_ConvertToProto_WhenDomainMessageIsProvided()
    {
        // Given
        var domainMessage = new Entry<string>("test-producer", "test content");

        // When
        var protoMessage = domainMessage.ToProto();

        // Then
        protoMessage.Should().NotBeNull("because conversion should produce a valid protobuf message");
        protoMessage.Producer.Should().Be("test-producer", "because the producer should be preserved");
        protoMessage.Content.Should().Be("test content", "because the content should be preserved");
        protoMessage.CreatedAt.Should().NotBeNull("because the timestamp should be converted");
    }

    [Test]
    public void Should_ConvertToDomain_WhenProtoMessageIsProvided()
    {
        // Given
        var protoMessage = new MessageProto
        {
            Producer = "test-producer",
            Content = "test content",
            CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        };

        // When
        var domainMessage = protoMessage.ToDomain();

        // Then
        domainMessage.Should().NotBeNull("because conversion should produce a valid domain message");
        domainMessage.Producer.Value.Should().Be("test-producer", "because the producer should be preserved");
        domainMessage.Content.Should().Be("test content", "because the content should be preserved");
        domainMessage.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2), "because the timestamp should be preserved");
    }

    [Test]
    public void Should_SerializeToBytes_WhenDomainMessageIsProvided()
    {
        // Given
        var domainMessage = new Entry<string>("test-producer", "test content");

        // When
        var bytes = domainMessage.ToBytes();

        // Then
        bytes.Should().NotBeNull("because serialization should produce byte array");
        bytes.Should().NotBeEmpty("because a valid message should produce non-empty bytes");
        bytes.Length.Should().BeGreaterThan(0, "because the serialized message should have content");
    }

    [Test]
    public void Should_DeserializeFromBytes_WhenValidBytesAreProvided()
    {
        // Given
        var originalMessage = new Entry<string>("test-producer", "test content");
        var bytes = originalMessage.ToBytes();

        // When
        var deserializedMessage = MessageExtensions.FromBytes(bytes);

        // Then
        deserializedMessage.Should().NotBeNull("because deserialization should produce a valid message");
        deserializedMessage.Producer.Value.Should().Be(originalMessage.Producer.Value, "because the producer should be preserved");
        deserializedMessage.Content.Should().Be(originalMessage.Content, "because the content should be preserved");
    }

    [Test]
    public void Should_RoundTripSuccessfully_WhenSerializingAndDeserializing()
    {
        // Given
        var originalProducer = "round-trip-producer";
        var originalContent = "round-trip content with special chars: !@#$%^&*()";
        var originalMessage = new Entry<string>(originalProducer, originalContent);

        // When
        var bytes = originalMessage.ToBytes();
        var deserializedMessage = MessageExtensions.FromBytes(bytes);

        // Then
        deserializedMessage.Producer.Value.Should().Be(originalProducer, "because the producer should survive round-trip");
        deserializedMessage.Content.Should().Be(originalContent, "because the content should survive round-trip");
    }

    [Test]
    public void Should_ProduceCompactBytes_WhenSerializingWithProtobuf()
    {
        // Given
        var producer = "service";
        var content = "Hello, Redis!";
        var message = new Entry<string>(producer, content);
        
        // Calculate equivalent JSON size for comparison
        var jsonEquivalent = $"{{\"producer\":\"{producer}\",\"content\":\"{content}\",\"createdAt\":\"{message.CreatedAt:O}\"}}";
        var jsonBytes = Encoding.UTF8.GetBytes(jsonEquivalent);

        // When
        var protobufBytes = message.ToBytes();

        // Then
        protobufBytes.Length.Should().BeLessThan(jsonBytes.Length, 
            "because protobuf should be more compact than JSON");
        
        // Log sizes for visibility
        TestContext.WriteLine($"Protobuf size: {protobufBytes.Length} bytes");
        TestContext.WriteLine($"JSON equivalent size: {jsonBytes.Length} bytes");
        TestContext.WriteLine($"Size reduction: {((jsonBytes.Length - protobufBytes.Length) * 100.0 / jsonBytes.Length):F1}%");
    }

    [Test]
    public void Should_MeasureProtobufSize_WhenSerializingDifferentMessageSizes()
    {
        // Given - Small message
        var smallMessage = new Entry<string>("svc", "Hi");
        var smallBytes = smallMessage.ToBytes();

        // Given - Medium message
        var mediumMessage = new Entry<string>("payment-service", "Processing payment for order #12345");
        var mediumBytes = mediumMessage.ToBytes();

        // Given - Large message
        var largeContent = new string('A', 1000); // 1KB of 'A' characters
        var largeMessage = new Entry<string>("data-processor-service", largeContent);
        var largeBytes = largeMessage.ToBytes();

        // Then - Log size measurements
        TestContext.WriteLine("=== Protobuf Size Measurements ===");
        TestContext.WriteLine($"Small message:  {smallBytes.Length} bytes (producer: '{smallMessage.Producer}', content: '{smallMessage.Content}')");
        TestContext.WriteLine($"Medium message: {mediumBytes.Length} bytes (producer: '{mediumMessage.Producer}', content: '{mediumMessage.Content}')");
        TestContext.WriteLine($"Large message:  {largeBytes.Length} bytes (producer: '{largeMessage.Producer}', content length: {largeContent.Length} chars)");

        // Then - Validate size relationships
        smallBytes.Length.Should().BeLessThan(mediumBytes.Length, "because small messages should be smaller");
        mediumBytes.Length.Should().BeLessThan(largeBytes.Length, "because medium messages should be smaller than large");
        
        // Then - Validate overhead is reasonable (protobuf header + timestamp + field tags)
        smallBytes.Length.Should().BeLessThan(100, "because small messages should have minimal overhead");
    }

    [Test]
    public void Should_PreserveUtcTimestamp_WhenSerializingAndDeserializing()
    {
        // Given
        var message = new Entry<string>("time-service", "UTC test") { CreatedAt = new DateTimeOffset(2025, 11, 24, 10, 30, 0, TimeSpan.Zero) };

        // When
        var bytes = message.ToBytes();
        var deserialized = MessageExtensions.FromBytes(bytes);

        // Then
        deserialized.CreatedAt.Should().Be(message.CreatedAt, "because UTC timestamp should be preserved exactly");
        deserialized.CreatedAt.Offset.Should().Be(TimeSpan.Zero, "because timestamp should remain in UTC");
    }

    [Test]
    public void Should_ConvertNonUtcToUtc_WhenSerializingNonUtcTimestamp()
    {
        // Given
        var pacificOffset = TimeSpan.FromHours(-8);
        var pacificTimestamp = new DateTimeOffset(2025, 11, 24, 10, 30, 0, pacificOffset);
        var message = new Entry<string>("timezone-service", "PST test") { CreatedAt = pacificTimestamp };

        // When
        var bytes = message.ToBytes();
        var deserialized = MessageExtensions.FromBytes(bytes);

        // Then
        deserialized.CreatedAt.UtcDateTime.Should().Be(pacificTimestamp.ToUniversalTime().UtcDateTime, "because protobuf stores timestamps in UTC");
        deserialized.CreatedAt.Offset.Should().Be(TimeSpan.Zero, "because deserialized timestamp should be in UTC");
    }

    [Test]
    public void Should_UseFromProtoAlias_WhenConvertingFromProtobuf()
    {
        // Given
        var protoMessage = new MessageProto
        {
            Producer = "alias-test",
            Content = "testing alias",
            CreatedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        };

        // When
        var domainMessage = protoMessage.FromProto();

        // Then
        domainMessage.Should().NotBeNull("because FromProto should work as an alias to ToDomain");
        domainMessage.Producer.Value.Should().Be("alias-test", "because conversion should work correctly");
    }

    [Test]
    public void Should_UseSerializeAlias_WhenSerializingMessage()
    {
        // Given
        var message = new Entry<string>("serialize-test", "alias test");

        // When
        var bytesFromSerialize = message.Serialize();
        var bytesFromToBytes = message.ToBytes();

        // Then
        bytesFromSerialize.Should().Equal(bytesFromToBytes, "because Serialize should be an alias to ToBytes");
    }

    [Test]
    public void Should_UseDeserializeAlias_WhenDeserializingBytes()
    {
        // Given
        var message = new Entry<string>("deserialize-test", "alias test");
        var bytes = message.ToBytes();

        // When
        var messageFromDeserialize = MessageExtensions.Deserialize(bytes);
        var messageFromFromBytes = MessageExtensions.FromBytes(bytes);

        // Then
        messageFromDeserialize.Producer.Value.Should().Be(messageFromFromBytes.Producer.Value, 
            "because Deserialize should be an alias to FromBytes");
        messageFromDeserialize.Content.Should().Be(messageFromFromBytes.Content,
            "because both methods should produce identical results");
    }

    [Test]
    public void Should_HandleEmptyContent_WhenSerializingAndDeserializing()
    {
        // Given
        var message = new Entry<string>("empty-content-producer", string.Empty);

        // When
        var bytes = message.ToBytes();
        var deserialized = MessageExtensions.FromBytes(bytes);

        // Then
        deserialized.Content.Should().BeEmpty("because empty content should be preserved");
        deserialized.Producer.Value.Should().Be("empty-content-producer", "because producer should be preserved");
    }

    [Test]
    public void Should_HandleUnicodeContent_WhenSerializingAndDeserializing()
    {
        // Given
        var unicodeContent = "Hello 世界 🚀 Здравствуй мир";
        var message = new Entry<string>("unicode-service", unicodeContent);

        // When
        var bytes = message.ToBytes();
        var deserialized = MessageExtensions.FromBytes(bytes);

        // Then
        deserialized.Content.Should().Be(unicodeContent, "because Unicode content should be preserved correctly");
    }

    [Test]
    public void Should_HandleLongContent_WhenSerializingLargeMessages()
    {
        // Given
        var longContent = new string('X', 10000); // 10KB content
        var message = new Entry<string>("large-content-producer", longContent);

        // When
        var bytes = message.ToBytes();
        var deserialized = MessageExtensions.FromBytes(bytes);

        // Then
        deserialized.Content.Should().HaveLength(10000, "because long content should be preserved");
        deserialized.Content.Should().Be(longContent, "because all characters should be preserved");
        
        TestContext.WriteLine($"10KB message serialized to {bytes.Length} bytes");
    }

   

   
}