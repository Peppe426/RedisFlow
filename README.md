
---

## ⚙️ Components
- **Redis Server:** Hosted locally or on-premise; provides the stream and consumer group capabilities.
- **Producers:** Two separate .NET console apps pushing serialized messages to the stream.
- **Consumer:** A .NET console app reading from the stream using a consumer group.

---

## 🧩 Technical Objectives
1. Set up a Redis server (local Docker or native installation).
2. Publish messages to a Redis stream using an optimized binary format.
3. Consume and acknowledge messages using consumer groups.
4. Demonstrate message persistence and replay behavior.
5. Observe system behavior when:
   - One producer goes offline.
   - The consumer restarts (confirm pending messages are reprocessed).

---

## 🔄 Serialization Strategy
To ensure high performance and low overhead, the messages written to the Redis stream **should not be serialized as JSON**.

Instead, use a compact **binary serialization protocol** such as:

### ✅ MessagePack (recommended)
- Schema-less, extremely fast, and integrates seamlessly with C# objects.
- Perfect for prototypes and production scenarios.
- No `.proto` schema files required.

Example:
```csharp
[MessagePackObject]
public class EventData
{
    [Key(0)] public string Producer { get; set; }
    [Key(1)] public string Message { get; set; }
    [Key(2)] public DateTime Timestamp { get; set; }
}
