# RedisFlow

A production-ready .NET 9 demonstration of Redis Streams with Protocol Buffers serialization, featuring producers, consumers, and resilient message processing.

---

## 📋 Table of Contents
- [Components](#️-components)
- [Project Structure](#-project-structure)
- [Serialization Strategy](#-serialization-strategy)
- [Aspire and Infrastructure](#-aspire-and-infrastructure)
- [Technical Objectives](#-technical-objectives)
- [Resilience and Error Handling](#️-resilience-and-error-handling)
- [Testing](#-testing)
- [Schema Evolution](#-schema-evolution)

---

## ⚙️ Components
- **Redis Server:** Hosted via .NET Aspire (or Docker Compose with feature flags); provides stream and consumer group capabilities.
- **Producers:** Console applications in `src/` pushing serialized protobuf messages to the stream.
- **Consumer:** Console application in `src/` reading from the stream using consumer groups.
- **Shared Domain:** `RedisFlow.Domain` contains the protobuf schemas and serialization logic.

---

## 📁 Project Structure

```
src/
├── RedisFlow.AppHost/          # .NET Aspire AppHost for orchestration
├── RedisFlow.Domain/           # Shared message contracts and protobuf schemas
├── RedisFlow.Services/         # Producer/Consumer contracts
├── RedisFlow.ServiceDefaults/  # Aspire service defaults
├── Tests.Producer/             # Producer integration tests
└── Tests.Consumers/            # Consumer integration tests

tests/
└── (Future integration test projects)

docs/
└── schemas/
    ├── message.proto           # Protobuf schema definition
    ├── CHANGELOG.md           # Schema version history
    └── SAMPLE_PAYLOAD.md      # Sample payloads and debugging guide
```

### Key Conventions
- **Producers and consumers** are console apps under `src/`
- **Shared contracts** live in `RedisFlow.Domain`
- **Integration tests** should be in `tests/` directory (to be created)
- **Protobuf schemas** are versioned in `docs/schemas/`

---

## 🔄 Serialization Strategy

To ensure high performance and low overhead, messages written to the Redis stream use **Protocol Buffers (protobuf)** for serialization.

### ✅ Protocol Buffers (Required)
- **Schema-driven**: `.proto` files define message structure with versioning support
- **Compact and efficient**: Binary format optimized for performance
- **Strongly-typed**: Code generation provides compile-time type safety
- **Evolution-friendly**: Backward and forward compatibility through field tags

### Message Schema

The canonical message schema is defined in [`docs/schemas/message.proto`](docs/schemas/message.proto):

```protobuf
syntax = "proto3";

message Message {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
}
```

### Usage Example

```csharp
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Domain.Messages;

// Create a message
var message = new Message("Producer1", "Hello, Redis!");

// Serialize to binary
byte[] bytes = message.ToBytes();

// Deserialize from binary
var deserialized = MessageExtensions.FromBytes(bytes);
```

For sample payloads and debugging, see [`docs/schemas/SAMPLE_PAYLOAD.md`](docs/schemas/SAMPLE_PAYLOAD.md).

---

## ☁️ Aspire and Infrastructure

### .NET Aspire (Primary)

The project uses **.NET Aspire** for local development and orchestration:

- **AppHost** (`RedisFlow.AppHost`) wires up Redis container and applications
- **Service Defaults** provide common configuration (telemetry, health checks)
- **Redis Container** is automatically provisioned via Aspire

**Running with Aspire:**
```bash
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

### Docker Compose (Alternative)

Docker Compose support is available as a fallback, gated behind feature flags:

```bash
# Using Docker Compose (feature flag required)
docker-compose up -d redis
```

**Note:** Aspire is the recommended approach for development and testing.

---

## 🧩 Technical Objectives
1. Set up Redis server via Aspire (or Docker alternative).
2. Publish messages to a Redis stream using protobuf binary format.
3. Consume and acknowledge messages using consumer groups.
4. Demonstrate message persistence and replay behavior.
5. Observe system behavior when:
   - One producer goes offline
   - The consumer restarts (confirm pending messages are reprocessed)
6. Validate serialization/deserialization consistency

---

## 🛡️ Resilience and Error Handling

### Pending Entry List (PEL) Management
Consumer implementations **must** support:
- **Pending message replay**: Re-process messages not acknowledged before restart
- **XPENDING checks**: Monitor pending entries for each consumer
- **XCLAIM logic**: Claim stale pending messages from inactive consumers

### Producer Offline Scenarios
- Producers should handle Redis unavailability gracefully
- Implement retry with exponential backoff
- Log failures for diagnostics

### Stream ID Diagnostics
- Producers should log stream IDs upon successful publish
- Consumers should log stream IDs when processing/acknowledging messages
- Enables correlation and debugging of message flow

---

## 🧪 Testing

### Integration Tests

Integration tests should:
1. **Launch Aspire Redis instance** for isolated test environment
2. **Verify produce/consume flows** with real Redis streams
3. **Test replay scenarios** (consumer restart, pending messages)
4. **Validate serialization** (round-trip protobuf encode/decode)

**Running integration tests:**
```bash
dotnet test tests/RedisFlow.Integration
```

### Test Structure
- Follow **Given/When/Then** pattern (see [`docs/Test structure.md`](docs/Test%20structure.md))
- Use **NUnit** and **FluentAssertions**
- Tests should be isolated and repeatable

---

## 🔄 Schema Evolution

### Principles
1. **Never reuse tag numbers** - Tags are permanent identifiers
2. **Never change field types** - Add new fields, deprecate old ones
3. **Use `reserved` for deleted fields** - Prevents accidental reuse
4. **Document all changes** in `docs/schemas/CHANGELOG.md`

### Adding a New Field (Backward Compatible)

```protobuf
message Message {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
  string metadata = 4;  // New field - old readers ignore it
}
```

### Deprecating a Field

```protobuf
message Message {
  reserved 2;  // Mark as reserved
  reserved "old_content";
  
  string producer = 1;
  string content_v2 = 4;  // Replacement field
  google.protobuf.Timestamp created_at = 3;
}
```

### Reference Documentation
- Schema definitions: [`docs/schemas/message.proto`](docs/schemas/message.proto)
- Version history: [`docs/schemas/CHANGELOG.md`](docs/schemas/CHANGELOG.md)
- Sample payloads: [`docs/schemas/SAMPLE_PAYLOAD.md`](docs/schemas/SAMPLE_PAYLOAD.md)
- Protocol Buffers guide: https://protobuf.dev/programming-guides/proto3/

---

## 🤝 Contributing

- Follow C# conventions (PascalCase for public, camelCase for private)
- Write tests for new features (Given/When/Then structure)
- Update schema CHANGELOG.md for any `.proto` changes
- Ensure protobuf schemas maintain backward compatibility
- Run tests before submitting PRs: `dotnet test`

---

## 📄 License

See [LICENSE](LICENSE) for details
