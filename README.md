# RedisFlow

A .NET 9 demonstration project showcasing Redis Streams with producer/consumer patterns, resilience testing, and message persistence using Protocol Buffers.

# RedisFlow

A .NET 9 demonstration project showcasing Redis Streams with Protocol Buffers serialization, using .NET Aspire for orchestration.

---

## ⚙️ Components
- **Redis Server:** Hosted via .NET Aspire; provides stream and consumer group capabilities.
- **Producers:** .NET implementations pushing serialized messages to Redis Streams.
- **Consumer:** .NET consumer reading from the stream using consumer groups with automatic pending message recovery.
- **Producers:** Two separate .NET console apps pushing serialized messages to the stream.
  - `RedisFlow.Producer1`: Sends messages every 2 seconds
  - `RedisFlow.Producer2`: Sends messages every 3 seconds
- **Consumer:** A .NET console app reading from the stream using a consumer group *(coming soon)*.

---

## 🚀 Getting Started with Aspire

### Prerequisites
- .NET 9 SDK
- Docker Desktop (for running Redis container)
- Visual Studio 2022 17.9+ or JetBrains Rider 2024.1+ (recommended for Aspire dashboard)

### Running the Application

1. **Start the Aspire AppHost:**
   ```bash
   cd src/RedisFlow/RedisFlow.AppHost
   dotnet run
   ```

   This will:
   - Spin up a Redis container locally via Docker
   - Start the Aspire Dashboard (typically at `http://localhost:15888`)
   - Expose Redis connection information to other projects

2. **Access the Aspire Dashboard:**
   - Open your browser to the URL shown in the console (usually `http://localhost:15888`)
   - View logs, traces, and metrics for all resources
   - Monitor Redis container health and connection status

3. **Stop the Aspire Host:**
   - Press `Ctrl+C` in the terminal where AppHost is running
   - Redis container will be stopped automatically

### Connection Information

The Redis connection string is automatically discoverable by producers and consumers through Aspire's service discovery:
- Resource name: `redis`
- Connection string format: `localhost:{dynamicPort}`

---

## 🧩 Technical Objectives
1. Set up Redis server via .NET Aspire AppHost.
2. Publish messages to a Redis stream using Protocol Buffers (protobuf) binary format.
1. Set up a Redis server using .NET Aspire orchestration.
2. Publish messages to a Redis stream using Protocol Buffers binary serialization.
3. Consume and acknowledge messages using consumer groups.
4. Demonstrate message persistence and replay behavior.
5. Validate system resilience when:
   - One producer goes offline.
   - The consumer restarts (confirm pending messages are reprocessed).

---

## 🔄 Serialization Strategy

This project uses **Protocol Buffers (protobuf)** for message serialization to ensure:
- Type safety with versioned schemas
- High performance and low overhead
- Schema evolution support
- Cross-platform compatibility

### Message Schema

Messages are defined in `docs/schemas/message.proto`:

```protobuf
syntax = "proto3";

package redisflow;

import "google/protobuf/timestamp.proto";

option csharp_namespace = "RedisFlow.Domain.Proto";

message MessageProto {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
}
```

All schema changes are documented in `docs/schemas/CHANGELOG.md`.

---

## 🏗️ Project Structure

```
src/RedisFlow/
├── RedisFlow.AppHost/          # Aspire AppHost for orchestrating Redis
├── RedisFlow.Domain/            # Domain models and protobuf schemas
├── RedisFlow.Services/          # Producer and Consumer implementations
│   ├── RedisProducer.cs        # Redis Streams producer
│   └── RedisConsumer.cs        # Redis Streams consumer with PEL support
├── RedisFlow.ServiceDefaults/  # Aspire service defaults
├── TestBase/                    # Base classes for testing
├── Tests.Consumers/            # Consumer integration tests
│   └── ResilienceIntegrationTests.cs  # Resilience scenario tests
└── Tests.Producer/             # Producer tests (placeholder)

docs/schemas/                    # Protobuf schema definitions
```

---

## 🚀 Getting Started

### Prerequisites

- .NET 9 SDK
- Docker Desktop (for Aspire Redis container)
- .NET Aspire workload: `dotnet workload install aspire`

### Running with Aspire

1. **Start the AppHost** (which launches Redis):
   ```bash
   cd src/RedisFlow/RedisFlow.AppHost
   dotnet run
   ```

2. **Access the Aspire Dashboard** at `http://localhost:15000` to monitor resources.

---

## 🧪 Resilience Testing

### Automated Integration Tests

The project includes comprehensive integration tests in `Tests.Consumers/ResilienceIntegrationTests.cs` that validate:

1. **One Producer Offline Scenario**
   - Multiple producers send messages
   - One producer stops sending
   - Verify the system continues processing remaining producer messages

2. **Consumer Restart with Pending Messages**
   - Consumer processes some messages then crashes
   - Consumer restarts with same identity
   - Verify pending messages are reprocessed via XPENDING/XCLAIM

3. **Combined Resilience Scenario**
   - Producer offline + consumer restart
   - Verify full system recovery

4. **Consumer Group Failover**
   - Different consumer joins the same group
   - Verify pending messages are claimed and processed

### Running Resilience Tests

**Prerequisites:** Start Redis container first via Aspire AppHost or standalone Docker:

```bash
# Option 1: Via Aspire (recommended)
cd src/RedisFlow/RedisFlow.AppHost
dotnet run

# Option 2: Standalone Redis Docker
docker run -d -p 6379:6379 redis:latest
```

**Run the tests:**

```bash
cd src/RedisFlow
dotnet test Tests.Consumers --filter "Category=Integration test"
```

### Manual Resilience Validation

You can manually validate resilience scenarios using the implemented services:

**Scenario 1: One Producer Offline**

```csharp
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var producer1 = new RedisProducer(redis, "my-stream");
var producer2 = new RedisProducer(redis, "my-stream");

// Both producers send messages
await producer1.ProduceAsync(new Message("P1", "Message1"));
await producer2.ProduceAsync(new Message("P2", "Message2"));

// Producer 1 stops (simulated offline)
// Producer 2 continues
await producer2.ProduceAsync(new Message("P2", "Message3"));

// Verify consumer still processes all messages
```

**Scenario 2: Consumer Restart**

```csharp
var consumer = new RedisConsumer(redis, "my-stream", "my-group", "consumer1");

// Consumer processes messages then crashes (CancellationToken cancelled)
await consumer.ConsumeAsync(async (msg, ct) => {
    // Process message...
}, cts.Token);

// Restart consumer with same identity
var consumerRestarted = new RedisConsumer(redis, "my-stream", "my-group", "consumer1");
// Pending messages automatically recovered via ProcessPendingMessagesAsync
await consumerRestarted.ConsumeAsync(handler, newCts.Token);
```

---

## 📊 Observability & Diagnostics

### Stream Inspection

Check stream status using Redis CLI or via code:

```csharp
var db = redis.GetDatabase();

// Get stream length
var streamInfo = await db.StreamInfoAsync("my-stream");
Console.WriteLine($"Stream length: {streamInfo.Length}");

// Check pending messages
var pendingInfo = await db.StreamPendingAsync("my-stream", "my-group");
Console.WriteLine($"Pending messages: {pendingInfo.PendingMessageCount}");

// Get consumer group details
var groups = await db.StreamGroupInfoAsync("my-stream");
foreach (var group in groups)
{
    Console.WriteLine($"Group: {group.Name}, Pending: {group.PendingMessageCount}");
}
```

### Logging Best Practices

Enable structured logging in your producer/consumer implementations:

```csharp
_logger.LogInformation("Message produced to stream {StreamName} with ID {MessageId}", 
    streamName, messageId);

_logger.LogWarning("Processing pending message {MessageId} after consumer restart", 
    messageId);
```

### Metrics to Monitor

- **Stream length**: Total messages in stream
- **Pending messages count**: Unacknowledged messages per consumer group
- **Consumer lag**: Time since last message processing
- **Message production rate**: Messages/second per producer
- **Acknowledgment rate**: Messages acknowledged/second

---

## 🔧 Troubleshooting

### Redis Connection Issues

**Problem:** Cannot connect to Redis at `localhost:6379`

**Solutions:**
- Verify Redis is running: `docker ps | grep redis`
- Check Aspire Dashboard for Redis resource status
- Verify port 6379 is not in use by another service
- Ensure Docker Desktop is running

### Consumer Not Processing Messages

**Problem:** Consumer starts but doesn't receive messages

**Solutions:**
1. Check if consumer group exists:
   ```bash
   redis-cli XINFO GROUPS my-stream
   ```
2. Verify messages are in stream:
   ```bash
   redis-cli XLEN my-stream
   ```
3. Check for pending messages:
   ```bash
   redis-cli XPENDING my-stream my-group
   ```
4. Review consumer logs for exceptions

### Pending Messages Not Recovered

**Problem:** Messages remain pending after consumer restart

**Solutions:**
- Consumer must use the **same consumer name** to claim its own pending messages
- Check `XPENDING` output to verify messages are assigned to your consumer
- Ensure `ProcessPendingMessagesAsync` is called during consumer startup
- Use `XCLAIM` manually if automatic recovery fails:
  ```bash
  redis-cli XCLAIM my-stream my-group consumer1 0 message-id
  ```

### Protobuf Serialization Errors

**Problem:** `InvalidProtocolBufferException` when consuming messages

**Solutions:**
- Ensure producer and consumer use the same schema version
- Check `docs/schemas/CHANGELOG.md` for breaking changes
- Verify generated C# code is up-to-date: `dotnet build RedisFlow.Domain`
- Never reuse protobuf field tag numbers

---

## 🔐 Best Practices

### Producer Resilience
- Implement retry logic with exponential backoff
- Log all message IDs returned by Redis for tracking
- Monitor stream length to detect backlogs
- Use connection pooling (`IConnectionMultiplexer` is thread-safe)

### Consumer Resilience
- Always acknowledge messages after successful processing
- Implement idempotency for message handlers
- Use consumer groups for parallel processing
- Set reasonable timeouts for `XREADGROUP` operations
- Monitor PEL (Pending Entries List) size
- Implement claim/retry logic for stale pending messages

### Schema Evolution
- Never reuse field tag numbers
- Use reserved fields for deleted tags
- Add new optional fields instead of changing types
- Document all changes in `CHANGELOG.md`
- Test compatibility between schema versions

---

## 📖 Further Reading

- [Redis Streams Introduction](https://redis.io/docs/data-types/streams/)
- [Redis Consumer Groups](https://redis.io/docs/data-types/streams-tutorial/)
- [Protocol Buffers Guide](https://protobuf.dev/programming-guides/proto3/)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)

---

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.
**This project uses Protocol Buffers (protobuf)** for all messages written to Redis Streams.

### ✅ Protocol Buffers
- Schema-based, compact binary serialization
- Strong typing and version compatibility
- Schema files stored in `docs/schemas/`
- C# types generated at build time

**Note:** JSON or schema-less formats (e.g., MessagePack) are NOT used for stream payloads, per project guidelines.

Example `.proto` file:
```protobuf
syntax = "proto3";

message EventData {
    string producer = 1;
    string message = 2;
    int64 timestamp = 3;
}
```

---

## 📁 Project Structure

```
RedisFlow/
├── src/
│   └── RedisFlow/
│       ├── RedisFlow.AppHost/          # Aspire orchestration host
│       ├── RedisFlow.Domain/           # Shared contracts and domain models
│       ├── RedisFlow.Services/         # Business logic
│       ├── RedisFlow.ServiceDefaults/  # Common Aspire services
│       ├── Tests.Producer/             # Producer console apps (test harness)
│       └── Tests.Consumers/            # Consumer console apps (test harness)
├── tests/
│   └── RedisFlow.Integration/          # Integration tests with Aspire
├── docs/
│   ├── schemas/                        # Protobuf schema definitions
│   └── Test structure.md               # Testing conventions
└── README.md
```

---

## 🧪 Running Integration Tests

Integration tests verify the complete produce/consume/replay flow using the Aspire Redis instance:

```bash
dotnet test tests/RedisFlow.Integration/
```

These tests will:
- Automatically start the Aspire host and Redis container
- Execute stream operations (produce, consume, acknowledge)
- Verify pending message replay scenarios
- Clean up resources after completion

---

## 💡 Resilience Expectations

### Consumer Implementations Must Support:
1. **Pending Message Replay:** Reprocess messages from the PEL (Pending Entries List) on restart
2. **Producer-Offline Scenarios:** Continue consuming existing messages even when producers are unavailable
3. **Stream ID Diagnostics:** Log and track stream IDs for debugging and monitoring

### Producer Implementations Should:
1. Handle Redis connection failures gracefully
2. Implement retry logic with exponential backoff
3. Log stream IDs for correlation with consumer logs

---

## 📖 Additional Documentation

- [Test Structure Guidelines](docs/Test%20structure.md) - NUnit + FluentAssertions conventions
- [Schema Evolution](docs/schemas/CHANGELOG.md) - Protobuf schema versioning rules
