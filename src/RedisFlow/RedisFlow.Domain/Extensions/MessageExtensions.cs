using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using RedisFlow.Domain.Proto;

namespace RedisFlow.Domain.Extensions;

/// <summary>
/// Extension methods for converting between protobuf MessageProto and domain Message
/// </summary>
public static class MessageExtensions
{
    /// <summary>
    /// Converts a domain Message to a protobuf MessageProto for serialization
    /// </summary>
    public static MessageProto ToProto(this ValueObjects.Message<string> domainMessage)
    {
        return new MessageProto
        {
            Producer = domainMessage.Producer,
            Content = domainMessage.Content,
            // Ensure we pass a UTC DateTime when converting DateTimeOffset -> Timestamp
            CreatedAt = Timestamp.FromDateTime(domainMessage.CreatedAt.UtcDateTime)
        };
    }

    /// <summary>
    /// Converts a protobuf MessageProto to a domain Message for consumption
    /// </summary>
    public static ValueObjects.Message<string> ToDomain(this MessageProto protoMessage)
    {
        var utcDateTime = protoMessage.CreatedAt.ToDateTime(); // should be Kind=Utc
        var dto = utcDateTime.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(utcDateTime)
            : new DateTimeOffset(utcDateTime, TimeSpan.Zero);
        return new ValueObjects.Message<string>(
            protoMessage.Producer,
            protoMessage.Content
        ) { CreatedAt = dto };
    }

    /// <summary>
    /// Converts a protobuf MessageProto to a domain Message (alias for ToDomain)
    /// </summary>
    public static ValueObjects.Message<string> FromProto(this MessageProto protoMessage)
    {
        return protoMessage.ToDomain();
    }

    /// <summary>
    /// Serializes a domain Message to binary format using protobuf
    /// </summary>
    public static byte[] ToBytes(this ValueObjects.Message<string> domainMessage)
    {
        return domainMessage.ToProto().ToByteArray();
    }

    /// <summary>
    /// Serializes a domain Message to binary format using protobuf (alias for ToBytes)
    /// </summary>
    public static byte[] Serialize(this ValueObjects.Message<string> domainMessage)
    {
        return domainMessage.ToBytes();
    }

    /// <summary>
    /// Deserializes a protobuf MessageProto from binary format to domain Message
    /// </summary>
    public static ValueObjects.Message<string> FromBytes(byte[] bytes)
    {
        return MessageProto.Parser.ParseFrom(bytes).ToDomain();
    }

    /// <summary>
    /// Deserializes a protobuf MessageProto from binary format to domain Message (alias for FromBytes)
    /// </summary>
    public static ValueObjects.Message<string> Deserialize(byte[] bytes)
    {
        return FromBytes(bytes);
    }
}
