using System;
using FluentAssertions;
using NUnit.Framework;
using RedisFlow.Domain.ValueObjects;

namespace Tests.Domain;

[TestFixture]
public class ProducerTests
{
    [Test]
    public void Should_CreateProducerReference_WhenValidStringProvided()
    {
        // Given
        var expectedProducerName = "user-service";

        // When
        var producer = ProducerReference.Create(expectedProducerName);

        // Then
        producer.Value.Should().Be(expectedProducerName, "because the factory should create a producer with the given name");
    }

    [Test]
    public void Should_TrimWhitespace_WhenCreatingProducerReference()
    {
        // Given
        var producerNameWithWhitespace = "  payment-service  ";
        var expectedTrimmedName = "payment-service";

        // When
        var producer = ProducerReference.Create(producerNameWithWhitespace);

        // Then
        producer.Value.Should().Be(expectedTrimmedName, "because the factory should trim whitespace from producer names");
    }

    [Test]
    public void Should_ThrowArgumentException_WhenCreatingWithNullString()
    {
        // Given
        string nullProducerName = null!;

        // When
        Action act = () => ProducerReference.Create(nullProducerName);

        // Then
        act.Should().Throw<ArgumentException>("because producer name cannot be null")
            .WithParameterName("value");
    }

    [Test]
    public void Should_ThrowArgumentException_WhenCreatingWithEmptyString()
    {
        // Given
        var emptyProducerName = string.Empty;

        // When
        Action act = () => ProducerReference.Create(emptyProducerName);

        // Then
        act.Should().Throw<ArgumentException>("because producer name cannot be empty")
            .WithParameterName("value");
    }

    [Test]
    public void Should_ThrowArgumentException_WhenCreatingWithWhitespaceString()
    {
        // Given
        var whitespaceProducerName = "   ";

        // When
        Action act = () => ProducerReference.Create(whitespaceProducerName);

        // Then
        act.Should().Throw<ArgumentException>("because producer name cannot be only whitespace")
            .WithParameterName("value");
    }

    [Test]
    public void Should_ImplicitlyConvertFromString_WhenAssigningStringToProducerReference()
    {
        // Given
        var producerName = "stream-service";

        // When
        ProducerReference producer = producerName;

        // Then
        producer.Value.Should().Be(producerName, "because implicit conversion should preserve the string value");
    }

    [Test]
    public void Should_ImplicitlyConvertToString_WhenAssigningProducerReferenceToString()
    {
        // Given
        var producer = ProducerReference.Create("notification-service");
        var expectedString = "notification-service";

        // When
        string producerString = producer;

        // Then
        producerString.Should().Be(expectedString, "because implicit conversion should return the underlying string value");
    }

    [Test]
    public void Should_ReturnProducerName_WhenCallingToString()
    {
        // Given
        var producerName = "order-service";
        var producer = ProducerReference.Create(producerName);

        // When
        var result = producer.ToString();

        // Then
        result.Should().Be(producerName, "because ToString should return the producer name");
    }

    [Test]
    public void Should_BeEqual_WhenTwoProducerReferencesHaveSameValue()
    {
        // Given
        var producer1 = ProducerReference.Create("api-gateway");
        var producer2 = ProducerReference.Create("api-gateway");

        // When
        var areEqual = producer1 == producer2;

        // Then
        areEqual.Should().BeTrue("because two ProducerReferences with the same value should be equal");
        producer1.Should().Be(producer2, "because record structs have structural equality");
    }

    [Test]
    public void Should_NotBeEqual_WhenTwoProducerReferencesHaveDifferentValues()
    {
        // Given
        var producer1 = ProducerReference.Create("auth-service");
        var producer2 = ProducerReference.Create("billing-service");

        // When
        var areEqual = producer1 == producer2;

        // Then
        areEqual.Should().BeFalse("because two ProducerReferences with different values should not be equal");
        producer1.Should().NotBe(producer2, "because they represent different producers");
    }

    [Test]
    public void Should_HaveSameHashCode_WhenTwoProducerReferencesHaveSameValue()
    {
        // Given
        var producer1 = ProducerReference.Create("inventory-service");
        var producer2 = ProducerReference.Create("inventory-service");

        // When
        var hashCode1 = producer1.GetHashCode();
        var hashCode2 = producer2.GetHashCode();

        // Then
        hashCode1.Should().Be(hashCode2, "because equal ProducerReferences should have the same hash code");
    }

    [Test]
    public void Should_SupportVersionedProducerNames_WhenCreatingProducerReference()
    {
        // Given
        var versionedProducerName = "payment-service@v3.1.0";

        // When
        var producer = ProducerReference.Create(versionedProducerName);

        // Then
        producer.Value.Should().Be(versionedProducerName, "because producer names can include version information");
    }
}
