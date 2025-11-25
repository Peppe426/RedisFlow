using System.Text.RegularExpressions;

namespace RedisFlow.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for the service/application that produced the message
/// </summary>
public readonly record struct ProducerReference(string Value)
{
    private const int MaxLength = 100;

    /// <summary>
    /// Recommended factory — validates and normalizes
    /// </summary>
    public static ProducerReference Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Producer cannot be null or empty.", nameof(value));

        var trimmed = value.Trim();

        if (trimmed.Length == 0)
            throw new ArgumentException("Producer cannot be empty after trimming.", nameof(value));

        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Producer name must not exceed {MaxLength} characters.", nameof(value));

        // Reasonable safe character set for service names in distributed systems
        if (!Regex.IsMatch(trimmed, @"^[\w\.\-@:/]+$"))
            throw new ArgumentException(
                "Producer name contains invalid characters. Allowed: letters, digits, and . - _ @ : /",
                nameof(value));

        return new ProducerReference(trimmed);
    }

    /// <summary>
    /// Allows nice syntax: ProducerReference producer = "user-service";
    /// </summary>
    public static implicit operator ProducerReference(string value) => Create(value);

    /// <summary>
    /// Allows easy use as string in logs, JSON, Redis keys, etc.
    /// </summary>
    public static implicit operator string(ProducerReference id) => id.Value;

    public override string ToString() => Value;
}