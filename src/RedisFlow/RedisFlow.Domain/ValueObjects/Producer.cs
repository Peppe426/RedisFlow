using System;

namespace RedisFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for the service/application that produced the message
/// </summary>
public readonly record struct ProducerReference
{
    /// <summary>
    /// The actual producer name (e.g. "user-service", "stream-service", "payment-service@v3.1.0")
    /// </summary>
    public string Value
    {
        get;
    }

    /// <summary>
    /// Private constructor forces use of the Create factory or implicit string conversion
    /// </summary>
    private ProducerReference(string value) => Value = value;

    /// <summary>
    /// Recommended factory — validates and normalizes
    /// </summary>
    public static ProducerReference Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Producer cannot be null or empty.", nameof(value));

        return new ProducerReference(value.Trim());
    }

    /// <summary>
    /// Allows nice syntax: ProducerId producer = "user-service";
    /// </summary>
    public static implicit operator ProducerReference(string value) => Create(value);

    /// <summary>
    /// Allows easy use as string in logs, JSON, etc.
    /// </summary>
    public static implicit operator string(ProducerReference id) => id.Value;

    public override string ToString() => Value;
}
