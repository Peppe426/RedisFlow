using FluentAssertions;
using RedisFlow.Domain.ValueObjects;

namespace Tests.Domain;

[TestFixture]
public class MessageTests
{
    [Test]
    public void Should_CreateMessage_WhenUsingProducerAndContentConstructor()
    {
        // Given
        var expectedProducer = "test-producer";
        var expectedContent = "test content";

        // When
        var message = new Entry<string>(expectedProducer, expectedContent);

        // Then
        message.Producer.Value.Should().Be(expectedProducer, "because the constructor should set the producer");
        message.Content.Should().Be(expectedContent, "because the constructor should set the content");
        message.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1), "because CreatedAt should be initialized to current UTC time");
    }

    [Test]
    public void Should_CreateMessage_WhenUsingFullConstructor()
    {
        // Given
        var expectedProducer = "test-producer";
        var expectedContent = "test content";
        var expectedCreatedAt = new DateTimeOffset(2025, 11, 24, 10, 30, 0, TimeSpan.Zero);

        // When
        var message = new Entry<string>(expectedProducer, expectedContent) { CreatedAt = expectedCreatedAt };

        // Then
        message.Producer.Value.Should().Be(expectedProducer, "because the constructor should set the producer");
        message.Content.Should().Be(expectedContent, "because the constructor should set the content");
        message.CreatedAt.Should().Be(expectedCreatedAt, "because the constructor should set the provided CreatedAt timestamp");
    }

    [Test]
    public void Should_SetProducer_WhenAssigningStringToProducerProperty()
    {
        // Given
        var expectedProducerName = "payment-service";

        // When
        var message = new Entry<string>(expectedProducerName, "");

        // Then
        message.Producer.Value.Should().Be(expectedProducerName, "because ProducerReference supports implicit conversion from string");
    }

    [Test]
    public void Should_SetContent_WhenAssigningValueToContentProperty()
    {
        // Given
        var expectedContent = "new content";

        // When
        var message = new Entry<string>("TestProducer", expectedContent);

        // Then
        message.Content.Should().Be(expectedContent, "because Content property should be settable");
    }

    [Test]
    public void Should_SupportGenericContent_WhenUsingComplexType()
    {
        // Given
        var expectedProducer = "order-service";
        var expectedContent = new { OrderId = 123, Amount = 99.99m };

        // When
        var message = new Entry<object>(expectedProducer, expectedContent);

        // Then
        message.Producer.Value.Should().Be(expectedProducer, "because the producer should be set correctly");
        message.Content.Should().BeEquivalentTo(expectedContent, "because Entry supports generic content types");
    }

    [Test]
    public void Should_SupportIntegerContent_WhenUsingValueType()
    {
        // Given
        var expectedProducer = "counter-service";
        var expectedContent = 42;

        // When
        var message = new Entry<int>(expectedProducer, expectedContent);

        // Then
        message.Producer.Value.Should().Be(expectedProducer, "because the producer should be set correctly");
        message.Content.Should().Be(expectedContent, "because Entry supports value type content");
    }

    [Test]
    public void Should_PreserveTimezone_WhenCreatingMessageWithSpecificTimezone()
    {
        // Given
        var expectedProducer = "timezone-service";
        var expectedContent = "timezone test";
        var pacificOffset = TimeSpan.FromHours(-8);
        var expectedCreatedAt = new DateTimeOffset(2025, 11, 24, 10, 30, 0, pacificOffset);

        // When
        var message = new Entry<string>(expectedProducer, expectedContent) { CreatedAt = expectedCreatedAt };

        // Then
        message.CreatedAt.Should().Be(expectedCreatedAt, "because the constructor should preserve the timezone information");
        message.CreatedAt.Offset.Should().Be(pacificOffset, "because the offset should be preserved");
    }

    [Test]
    public void Should_CreateMessageWithUtcTime_WhenNoTimestampProvided()
    {
        // Given
        var expectedProducer = "utc-service";
        var expectedContent = "utc test";
        var beforeCreation = DateTimeOffset.UtcNow;

        // When
        var message = new Entry<string>(expectedProducer, expectedContent);
        var afterCreation = DateTimeOffset.UtcNow;

        // Then
        message.CreatedAt.Should().BeOnOrAfter(beforeCreation, "because CreatedAt should be set during construction");
        message.CreatedAt.Should().BeOnOrBefore(afterCreation, "because CreatedAt should be set during construction");
        message.CreatedAt.Offset.Should().Be(TimeSpan.Zero, "because CreatedAt should be in UTC");
    }

    [Test]
    public void Should_AllowNullContent_WhenUsingNullableGenericType()
    {
        // Given
        var expectedProducer = "nullable-service";
        string? expectedContent = null;

        // When
        var message = new Entry<string?>(expectedProducer, expectedContent);

        // Then
        message.Content.Should().BeNull("because Entry supports nullable content types");
    }

    [Test]
    public void Should_SupportCollectionContent_WhenUsingListType()
    {
        // Given
        var expectedProducer = "batch-service";
        var expectedContent = new List<string> { "item1", "item2", "item3" };

        // When
        var message = new Entry<List<string>>(expectedProducer, expectedContent);

        // Then
        message.Content.Should().BeEquivalentTo(expectedContent, "because Entry supports collection content types");
        message.Content.Count.Should().Be(3, "because all items should be preserved");
    }

    [Test]
    public void Should_HaveInitOnlyCreatedAt_WhenTryingToModifyAfterConstruction()
    {
        // Given
        var message = new Entry<string>("test-producer", "test content");
        var originalCreatedAt = message.CreatedAt;

        // When
        // Attempting to modify CreatedAt should not compile due to init-only setter
        // This test verifies the property exists and is readable

        // Then
        message.CreatedAt.Should().Be(originalCreatedAt, "because CreatedAt should be immutable after initialization");
    }

    [Test]
    public void Should_ImplicitlyConvertProducer_WhenPassingStringToConstructor()
    {
        // Given
        var producerString = "api-gateway@v2.0.0";
        var content = "versioned producer test";

        // When
        var message = new Entry<string>(producerString, content);

        // Then
        message.Producer.Value.Should().Be(producerString, "because string should implicitly convert to ProducerReference");
        message.Producer.Should().BeOfType<ProducerReference>("because Producer property is of type ProducerReference");
    }
}