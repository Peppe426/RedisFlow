using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace RedisFlow.Domain.ValueObjects;

public static class MessageMapper
{
    public static MessageProto ToProto(this Message message)
    {
        return new MessageProto
        {
            Producer = message.Producer,
            Content = message.Content,
            CreatedAt = Timestamp.FromDateTime(message.CreatedAt.ToUniversalTime())
        };
    }

    public static Message FromProto(this MessageProto proto)
    {
        return new Message(
            proto.Producer,
            proto.Content
        );
    }

    public static byte[] Serialize(this Message message)
    {
        return message.ToProto().ToByteArray();
    }

    public static Message Deserialize(byte[] data)
    {
        var proto = MessageProto.Parser.ParseFrom(data);
        return proto.FromProto();
    }
}
