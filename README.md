
---

## ⚙️ Components
- **Redis Server:** Hosted locally or on-premise; provides the stream and consumer group capabilities.
- **Producers:** Two separate .NET console apps pushing serialized messages to the stream.
- **Consumer:** A .NET console app reading from the stream using a consumer group.
- **Developer Tooling:** Scripts and diagnostics tools for monitoring stream traffic.

---

## 🛠️ Developer Tooling

RedisFlow includes comprehensive tooling for observing Redis stream traffic during development:

- **Interactive Diagnostics Tool** - Rich console UI for stream inspection
- **Shell Scripts** - Quick CLI-based inspection (`inspect-stream.sh`, `inspect-pending.sh`, `monitor-stream.sh`)
- **Redis Commander** - Web UI automatically included with Aspire
- **Direct Redis CLI Commands** - For advanced troubleshooting

See [docs/Developer-Tooling.md](docs/Developer-Tooling.md) for complete usage guide.

### Quick Start

```bash
# Start Aspire with Redis
cd src/RedisFlow/RedisFlow.AppHost
dotnet run

# In another terminal, run diagnostics tool
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream

# Or use shell scripts
./scripts/inspect-stream.sh mystream
```

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
