
---

## ⚙️ Components
- **Redis Server:** Hosted locally or on-premise; provides the stream and consumer group capabilities.
- **Producers:** Two separate .NET console apps pushing serialized messages to the stream.
- **Consumer:** A .NET console app reading from the stream using a consumer group.

---

## 🧩 Technical Objectives
1. Set up a Redis server using .NET Aspire or Docker.
2. Publish messages to a Redis stream using **Protocol Buffers** for optimized binary serialization.
3. Consume and acknowledge messages using consumer groups.
4. Demonstrate message persistence and replay behavior.
5. Observe system behavior when:
   - One producer goes offline.
   - The consumer restarts (confirm pending messages are reprocessed).

---

## 🔄 Serialization Strategy
All messages written to Redis Streams use **Protocol Buffers (protobuf)** for efficient binary serialization.

### ✅ Why Protocol Buffers?
- Compact binary format with schema versioning
- Type-safe with generated C# classes
- Efficient serialization/deserialization
- Schema evolution support (backward/forward compatibility)

### 📋 Schema Management
- Proto schemas are stored in `docs/schemas/`
- All schema changes are documented in `docs/schemas/CHANGELOG.md`
- Generated C# types are created at build time (not checked in)

---

## 🔄 Message Replay Workflow

### Overview
Redis Streams with consumer groups provide built-in message persistence and replay capabilities through the **Pending Entries List (PEL)**. When a consumer reads a message but doesn't acknowledge it (e.g., due to a crash), the message remains in the PEL and can be reprocessed when the consumer restarts.

### Replay Scenarios

#### 1. **Consumer Restart - Automatic Pending Message Replay**
When a consumer restarts, it automatically:
1. Checks for pending messages in its consumer group
2. Reprocesses all pending messages before consuming new ones
3. Acknowledges messages after successful processing

```csharp
// Consumer automatically handles replay on startup
await consumer.ConsumeAsync(async (message, ct) =>
{
    // Process message
    await ProcessMessageAsync(message);
    // Message is acknowledged automatically after handler completes
}, cancellationToken);
```

#### 2. **Producer Offline - Message Persistence**
Messages produced while the consumer is offline:
1. Are stored persistently in the Redis stream
2. Remain available for consumption when the consumer comes back online
3. Are processed in the order they were produced

#### 3. **Consumer Crash - Pending Message Recovery**
If a consumer crashes after reading but before acknowledging:
1. The message remains in the PEL
2. On restart, the consumer reprocesses the pending message
3. The message is acknowledged only after successful processing

### Usage Example

#### Producer
```csharp
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services;
using StackExchange.Redis;

// Connect to Redis
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

// Create producer
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var producer = new RedisStreamProducer(
    redis,
    loggerFactory.CreateLogger<RedisStreamProducer>(),
    streamKey: "messages");

// Produce messages
await producer.ProduceAsync(
    new Message("producer-1", "Hello from producer!"),
    CancellationToken.None);
```

#### Consumer
```csharp
using Microsoft.Extensions.Logging;
using RedisFlow.Services;
using StackExchange.Redis;

// Connect to Redis
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

// Create consumer
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var consumer = new RedisStreamConsumer(
    redis,
    loggerFactory.CreateLogger<RedisStreamConsumer>(),
    consumerGroup: "my-group",
    consumerName: "consumer-1",
    streamKey: "messages");

// Consume messages (automatically handles replay on startup)
await consumer.ConsumeAsync(async (message, ct) =>
{
    Console.WriteLine($"Received from {message.Producer}: {message.Content}");
    // Message is automatically acknowledged after handler completes
}, cancellationToken);
```

### Running Integration Tests

Integration tests demonstrate all replay scenarios:

```bash
cd tests/RedisFlow.Integration
dotnet test
```

Tests automatically:
- Start a Redis container using Testcontainers
- Verify produce/consume flows
- Test pending message replay
- Verify message persistence across consumer restarts
- Clean up resources after each test

### Setting Up Redis

#### Using .NET Aspire (Recommended)
```csharp
var builder = DistributedApplication.CreateBuilder(args);
var redis = builder.AddRedis("redis")
    .WithDataVolume(); // Persist data across container restarts
```

#### Using Docker Compose (Alternative)
```yaml
services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
volumes:
  redis-data:
