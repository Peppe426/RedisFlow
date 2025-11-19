using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Proto;

namespace RedisFlow.Domain.Messages;

/// <summary>
/// Extension methods for converting between protobuf MessageProto and domain Message
/// </summary>
public static class MessageExtensions
{
    /// <summary>
    /// Converts a domain Message to a protobuf MessageProto for serialization
    /// </summary>
    public static MessageProto ToProto(this ValueObjects.Message domainMessage)
    {
        return new MessageProto
        {
            Producer = domainMessage.Producer,
            Content = domainMessage.Content,
            CreatedAt = Timestamp.FromDateTime(domainMessage.CreatedAt)
        };
    }

    /// <summary>
    /// Converts a protobuf MessageProto to a domain Message for consumption
    /// </summary>
    public static ValueObjects.Message ToDomain(this MessageProto protoMessage)
    {
        return new ValueObjects.Message(
            protoMessage.Producer,
            protoMessage.Content,
            protoMessage.CreatedAt.ToDateTime()
        );
    }

    /// <summary>
    /// Serializes a domain Message to binary format using protobuf
    /// </summary>
    public static byte[] ToBytes(this ValueObjects.Message domainMessage)
    {
        return domainMessage.ToProto().ToByteArray();
    }

    /// <summary>
    /// Deserializes a protobuf MessageProto from binary format to domain Message
    /// </summary>
    public static ValueObjects.Message FromBytes(byte[] bytes)
    {
        return MessageProto.Parser.ParseFrom(bytes).ToDomain();
    }
}
