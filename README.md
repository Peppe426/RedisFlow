# RedisFlow - Aspire-based Redis Streams POC

A proof-of-concept demonstrating Redis Streams with .NET Aspire orchestration, focusing on resilience, message persistence, and consumer group patterns.

---

## 🎯 Overview

RedisFlow showcases a production-ready pattern for building distributed message processing systems using:
- **Redis Streams** for reliable message queuing
- **.NET Aspire** for local development orchestration
- **Protocol Buffers** for efficient binary serialization
- **Consumer Groups** for parallel processing and failover

---

## ⚙️ Architecture

### Components

1. **Redis Server**
   - Orchestrated via .NET Aspire
   - Provides stream and consumer group capabilities
   - Automatically managed lifecycle

2. **Producers** (2 instances)
   - `Producer1`: Sends messages every 5 seconds
   - `Producer2`: Sends messages every 3 seconds
   - Use Protocol Buffers for serialization
   - Resilient to Redis restarts

3. **Consumer**
   - Reads from consumer group `default-group`
   - Automatically processes pending messages (PEL replay)
   - Acknowledges messages upon successful processing
   - Resilient to crashes with message replay

### Message Flow

```
Producer1 ──┐
            ├──> Redis Stream ──> Consumer Group ──> Consumer
Producer2 ──┘
```

---

## 🔄 Serialization Strategy

Messages use **Protocol Buffers (protobuf)** for efficient binary serialization:

### Benefits
- **Compact**: Much smaller than JSON
- **Fast**: Extremely efficient serialization/deserialization
- **Typed**: Schema-defined with versioning support
- **Cross-platform**: Works across languages and platforms

### Schema

See [`docs/schemas/message.proto`](docs/schemas/message.proto):

```protobuf
message EventMessage {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
}
```

All schema changes are tracked in [`docs/schemas/CHANGELOG.md`](docs/schemas/CHANGELOG.md).

---

## 🚀 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Redis container)
- [.NET Aspire Workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)

### Install .NET Aspire

```bash
dotnet workload update
dotnet workload install aspire
```

### Running the Application

1. **Start the Aspire AppHost:**

```bash
cd src/RedisFlow
dotnet run --project RedisFlow.AppHost
```

2. **Open the Aspire Dashboard:**

Navigate to `https://localhost:17169` (or the URL shown in console) to view:
- Running services status
- Logs from all components
- Redis metrics
- Distributed tracing

3. **Observe the System:**

- Watch `Producer1` and `Producer2` logs sending messages
- See `Consumer` processing messages in real-time
- Monitor Redis stream length and consumer group lag

---

## 🧪 Testing

### Running Integration Tests

The integration tests validate resilience scenarios:

```bash
cd tests/RedisFlow.Integration
dotnet test
```

### Test Scenarios

1. **Basic Produce/Consume**
   - Validates messages flow through the system
   - Confirms protobuf serialization

2. **Message Persistence**
   - Verifies messages persist in Redis stream
   - Tests multiple producers writing concurrently

3. **Consumer Restart with Pending Messages**
   - Simulates consumer crash
   - Validates pending messages are replayed (PEL)
   - Ensures no message loss

4. **Offline Producer**
   - Tests system behavior when producer goes offline
   - Validates consumer continues processing available messages

5. **Protobuf Serialization**
   - Confirms messages use protobuf encoding
   - Validates deserialization correctness

6. **Message Acknowledgment**
   - Verifies messages are acknowledged after processing
   - Checks PEL for unacknowledged messages

### Test Execution

```bash
# Run all tests
dotnet test tests/RedisFlow.Integration

# Run with verbose output
dotnet test tests/RedisFlow.Integration -v n

# Run specific test
dotnet test tests/RedisFlow.Integration --filter "FullyQualifiedName~Should_ReplayPendingMessages_When_ConsumerRestarts"
```

---

## 🧩 Technical Objectives

1. ✅ Set up Redis server orchestrated via Aspire
2. ✅ Publish messages using Protocol Buffers serialization
3. ✅ Consume and acknowledge messages using consumer groups
4. ✅ Demonstrate message persistence and replay behavior
5. ✅ Handle resilience scenarios:
   - Producer going offline
   - Consumer restart with pending message replay (PEL)
   - Multiple concurrent producers

---

## 📁 Project Structure

```
RedisFlow/
├── src/RedisFlow/
│   ├── RedisFlow.AppHost/          # Aspire orchestration
│   ├── RedisFlow.Domain/           # Domain models + protobuf generation
│   ├── RedisFlow.Services/         # Producer/Consumer implementations
│   ├── RedisFlow.ServiceDefaults/  # Aspire service defaults
│   ├── Producer1/                  # Producer console app #1
│   ├── Producer2/                  # Producer console app #2
│   └── Consumer/                   # Consumer console app
├── tests/
│   └── RedisFlow.Integration/      # Integration tests
└── docs/
    └── schemas/                    # Protobuf schemas + changelog
```

---

## 🔍 Key Features

### Consumer Groups

The consumer uses Redis consumer groups for:
- **Parallel processing**: Multiple consumers can process different messages
- **Failover**: If one consumer dies, another can claim its pending messages
- **At-least-once delivery**: Messages stay in PEL until acknowledged

### Pending Entry List (PEL) Management

When a consumer starts:
1. Checks for pending messages (unacknowledged)
2. Claims pending messages using `XCLAIM`
3. Processes and acknowledges them
4. Then reads new messages

### Resilience Patterns

- **Producers**: Retry on connection failures
- **Consumer**: Replays unacknowledged messages on restart
- **Aspire**: Automatically manages container lifecycle

---

## 🔧 Configuration

### Redis Stream Settings

- **Stream Key**: `redisflow:stream`
- **Consumer Group**: `default-group`
- **Consumer Name**: Machine hostname (automatic)

### Message Rates

- **Producer1**: 5 seconds between messages
- **Producer2**: 3 seconds between messages

To modify, edit `Producer1/Program.cs` or `Producer2/Program.cs`:

```csharp
await Task.Delay(5000, stoppingToken); // Change delay here
```

---

## 📊 Monitoring

### Aspire Dashboard

Access at `https://localhost:17169` for:
- Real-time logs from all services
- Resource health checks
- Distributed tracing
- Metrics and telemetry

### Redis CLI

Connect to Redis container:

```bash
docker exec -it <container-id> redis-cli

# View stream info
XINFO STREAM redisflow:stream

# View consumer group info
XINFO GROUPS redisflow:stream

# View pending messages
XPENDING redisflow:stream default-group
```

---

## 🚧 Troubleshooting

### Issue: "Docker is not running"

**Solution**: Start Docker Desktop before running the AppHost.

### Issue: "Port already in use"

**Solution**: Stop other applications using ports 17169, 6379, or check Aspire dashboard for conflicts.

### Issue: Tests failing to connect to Redis

**Solution**: Ensure Docker has enough resources allocated (at least 2GB memory).

### Issue: Consumer not processing messages

**Solution**: Check consumer logs in Aspire dashboard. Verify Redis stream exists using Redis CLI.

---

## 📝 Development Notes

### Adding New Message Fields

1. Update `docs/schemas/message.proto`
2. Add new fields with **new tag numbers** (never reuse)
3. Document changes in `docs/schemas/CHANGELOG.md`
4. Rebuild Domain project to regenerate C# types
5. Update producer/consumer code as needed

### Schema Evolution Best Practices

- ✅ Add new fields with new tag numbers
- ✅ Mark deprecated fields as reserved
- ❌ Never change field types
- ❌ Never reuse tag numbers
- ❌ Never use `required` (not supported in proto3)

---

## 🤝 Contributing

Follow the conventions in [`docs/Test structure.md`](docs/Test%20structure.md) for all tests:
- Use Given/When/Then structure
- Name tests: `Should_[Behavior]_When_[Condition]`
- Use FluentAssertions for assertions
- One logical assertion per test

---

## 📚 References

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Redis Streams Tutorial](https://redis.io/docs/data-types/streams-tutorial/)
- [Protocol Buffers C# Tutorial](https://protobuf.dev/getting-started/csharptutorial/)
- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
