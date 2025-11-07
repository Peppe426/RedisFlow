using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using RedisFlow.Domain.ValueObjects;

namespace RedisFlow.Domain.Messages;

/// <summary>
/// Extension methods for converting between protobuf Message and domain Message
/// </summary>
public static class MessageExtensions
{
    /// <summary>
    /// Converts a domain Message to a protobuf Message for serialization
    /// </summary>
    public static Message ToProto(this ValueObjects.Message domainMessage)
    {
        return new Message
        {
            Producer = domainMessage.Producer,
            Content = domainMessage.Content,
            CreatedAt = Timestamp.FromDateTime(domainMessage.CreatedAt)
        };
    }

    /// <summary>
    /// Converts a protobuf Message to a domain Message for consumption
    /// </summary>
    public static ValueObjects.Message ToDomain(this Message protoMessage)
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
    /// Deserializes a protobuf Message from binary format to domain Message
    /// </summary>
    public static ValueObjects.Message FromBytes(byte[] bytes)
    {
        return Message.Parser.ParseFrom(bytes).ToDomain();
    }
}
