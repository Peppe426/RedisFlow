
# RedisFlow

A .NET 9 demonstration project showcasing Redis Streams with Protocol Buffers serialization, using .NET Aspire for orchestration.

---

## ⚙️ Components
- **Redis Server:** Hosted via .NET Aspire; provides stream and consumer group capabilities.
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
1. Set up a Redis server using .NET Aspire orchestration.
2. Publish messages to a Redis stream using Protocol Buffers binary serialization.
3. Consume and acknowledge messages using consumer groups.
4. Demonstrate message persistence and replay behavior.
5. Observe system behavior when:
   - One producer goes offline.
   - The consumer restarts (confirm pending messages are reprocessed).

---

## 🔄 Serialization Strategy

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
