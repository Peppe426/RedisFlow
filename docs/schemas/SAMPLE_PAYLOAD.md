# Sample Payload Documentation

This document provides sample serialized payloads for the `Message` protobuf schema, useful for debugging and integration testing.

## Message Schema

The `Message` type is defined in `message.proto`:

```protobuf
message Message {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
}
```

## Sample Message

### Domain Object (C#)
```csharp
var message = new RedisFlow.Domain.ValueObjects.Message(
    producer: "Producer1",
    content: "Hello, Redis!",
    createdAt: new DateTime(2025, 11, 3, 22, 0, 0, DateTimeKind.Utc)
);
```

### Protobuf Binary (Hex)

```
0a 09 50 72 6f 64 75 63 65 72 31 12 0d 48 65 6c 6c 6f 2c 20 52 65 64 69 73 21 1a 0c 08 d1 d9 cd b3 06 10 00
```

**Breakdown:**
- `0a 09`: Field 1 (producer), length 9 bytes
- `50 72 6f 64 75 63 65 72 31`: "Producer1" (UTF-8)
- `12 0d`: Field 2 (content), length 13 bytes
- `48 65 6c 6c 6f 2c 20 52 65 64 69 73 21`: "Hello, Redis!" (UTF-8)
- `1a 0c`: Field 3 (created_at), length 12 bytes
- `08 d1 d9 cd b3 06 10 00`: Timestamp (seconds + nanos)

### Protobuf Binary (Base64)

```
CglQcm9kdWNlcjESDUhlbGxvLCBSZWRpcyEaDAgB0dnNswYQAA==
```

## Usage Examples

### Serialization (C#)

```csharp
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Messages;

// Create a domain message
var message = new Message("Producer1", "Hello, Redis!");

// Serialize to binary using protobuf
byte[] bytes = message.ToBytes();

// Get hex representation for debugging
string hex = BitConverter.ToString(bytes).Replace("-", " ");
Console.WriteLine($"Hex: {hex}");

// Get base64 representation
string base64 = Convert.ToBase64String(bytes);
Console.WriteLine($"Base64: {base64}");
```

### Deserialization (C#)

```csharp
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Messages;

// Deserialize from binary
byte[] bytes = /* received from Redis stream */;
var message = MessageExtensions.FromBytes(bytes);

Console.WriteLine($"Producer: {message.Producer}");
Console.WriteLine($"Content: {message.Content}");
Console.WriteLine($"Created At: {message.CreatedAt:O}");
```

### Redis Stream Entry

When stored in Redis Streams, the message is typically stored as:

```
XADD mystream * payload <binary_data>
```

Example using StackExchange.Redis:

```csharp
var db = redis.GetDatabase();
var message = new Message("Producer1", "Hello, Redis!");
var bytes = message.ToBytes();

var streamId = await db.StreamAddAsync(
    "mystream",
    new NameValueEntry[] { new("payload", bytes) }
);
```

## Testing and Debugging

### Verify Serialization Round-Trip

```csharp
var original = new Message("TestProducer", "Test content");
var bytes = original.ToBytes();
var deserialized = MessageExtensions.FromBytes(bytes);

Assert.AreEqual(original.Producer, deserialized.Producer);
Assert.AreEqual(original.Content, deserialized.Content);
Assert.AreEqual(original.CreatedAt, deserialized.CreatedAt);
```

### Inspect Binary with protoc

If you have `protoc` installed, you can decode a binary payload:

```bash
# Save binary to file
echo "CglQcm9kdWNlcjESDUhlbGxvLCBSZWRpcyEaDAgB0dnNswYQAA==" | base64 -d > message.bin

# Decode using protoc
protoc --decode=redisflow.domain.Message message.proto < message.bin
```

Expected output:
```
producer: "Producer1"
content: "Hello, Redis!"
created_at {
  seconds: 1730674800
}
```

## Notes

- All timestamps are stored in UTC using `google.protobuf.Timestamp`
- Binary payloads are compact and efficient for Redis streams
- Protobuf maintains backward compatibility when following schema evolution rules
- Use the provided extension methods for seamless conversion between domain and protobuf types
