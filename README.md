
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
To ensure high performance and low overhead, the messages written to the Redis stream use **Protocol Buffers (protobuf)** for binary serialization.

### ✅ Protocol Buffers (implemented)
- Schema-defined, type-safe, and highly efficient binary format.
- Suitable for production scenarios with strong contracts.
- Schema files are versioned in `docs/schemas/`.

The `MessagePayload` protobuf schema:
```protobuf
message MessagePayload {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
}
```

---

## 🚀 Running with .NET Aspire

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Redis container)
- [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)

### Install .NET Aspire workload
```bash
dotnet workload install aspire
```

### Run the Application
Navigate to the AppHost project and run:

```bash
cd src/RedisFlow
dotnet run --project RedisFlow.AppHost
```

This will:
1. Start the Aspire Dashboard (typically at `http://localhost:15224`)
2. Launch a Redis container
3. Start both producer applications (Producer1 and Producer2)
4. Display logs and telemetry in the Aspire Dashboard

### Viewing Logs
- Open the Aspire Dashboard URL shown in the console
- Navigate to the **Resources** tab to see all running services
- Click on individual producers to view their logs and observe messages being produced
- Monitor Redis container health and connections

### Stopping the Application
Press `Ctrl+C` in the terminal where the AppHost is running. This will gracefully shut down all services.

---

## 📊 Monitoring Messages

### Using Redis CLI
Once the Aspire application is running, you can connect to Redis and monitor the stream:

```bash
# Get Redis connection details from Aspire Dashboard
docker exec -it <redis-container-name> redis-cli

# View stream length
XLEN messages:stream

# Read latest messages
XREAD COUNT 10 STREAMS messages:stream 0

# View message details (decode protobuf separately)
XRANGE messages:stream - + COUNT 10
```

### Using Aspire Dashboard
- View producer logs to see message IDs being generated
- Monitor resource metrics for Redis and producers
- Track message throughput over time

---

## 🏗️ Project Structure

```
src/RedisFlow/
├── RedisFlow.AppHost/           # Aspire orchestration host
├── RedisFlow.ServiceDefaults/   # Shared service configuration
├── RedisFlow.Domain/            # Domain models (Message)
├── RedisFlow.Services/          # Service interfaces and implementations
│   ├── Contracts/
│   │   └── IProducer.cs
│   └── Implementations/
│       └── RedisProducer.cs     # Redis Stream producer with protobuf
├── RedisFlow.Producer1/         # First producer console app
├── RedisFlow.Producer2/         # Second producer console app
└── Tests.Producer/              # Unit tests for producer

docs/schemas/
├── message.proto                # Protobuf schema definition
└── CHANGELOG.md                 # Schema version history
```

---

## 🧪 Running Tests

```bash
cd src/RedisFlow
dotnet test
```

Tests include:
- Argument validation for RedisProducer
- Verification of Redis Stream operations
- Protobuf serialization validation

---

## 📝 Schema Evolution

All schema changes must be documented in `docs/schemas/CHANGELOG.md`. Follow protobuf best practices:
- Never reuse tag numbers
- Add new fields instead of modifying existing ones
- Reserve numbers for deleted fields
- Maintain backward compatibility
