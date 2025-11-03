# RedisFlow POC

A proof-of-concept for Redis stream processing using .NET 9 and .NET Aspire, demonstrating message persistence, consumer groups, and resilience patterns.

---

## 📋 Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Running the POC](#running-the-poc)
- [Resilience Scenarios](#resilience-scenarios)
- [Integration Tests](#integration-tests)
- [Development Tips](#development-tips)
- [Serialization Strategy](#serialization-strategy)

---

## 🎯 Overview

RedisFlow demonstrates a production-ready Redis stream implementation with:

- **Redis Streams**: Event streaming with consumer groups
- **Aspire Orchestration**: Container management and service discovery
- **Protocol Buffers**: Efficient binary serialization for stream messages
- **Resilience Patterns**: Message persistence, replay, and failure handling
- **Integration Testing**: Aspire-hosted Redis for automated testing

> **📝 Note on Serialization**: This project uses **Protocol Buffers** (protobuf) for message serialization per project standards, ensuring schema evolution support and type safety. While some GitHub issues reference MessagePack, the implementation follows the protobuf approach as documented in [`.github/copilot-instructions.md`](/.github/copilot-instructions.md).

### Components

- **Redis Server**: Managed by Aspire via Docker; provides stream and consumer group capabilities
- **Producers**: Console applications that publish serialized messages to Redis streams
- **Consumer**: Console application using consumer groups for parallel message processing
- **Shared Contracts**: Common interfaces and domain models in `RedisFlow.Domain`
- **Integration Tests**: Automated tests verifying produce/consume/replay flows

---

## ✅ Prerequisites

### Required Software

1. **.NET 9 SDK** or later
   ```bash
   dotnet --version  # Should be 9.0.x or later
   ```

2. **Docker Desktop** (for Aspire Redis container)
   - [Download Docker Desktop](https://www.docker.com/products/docker-desktop)
   - Ensure Docker daemon is running before starting the AppHost

3. **.NET Aspire Workload**
   ```bash
   dotnet workload install aspire
   ```
   
   > The Aspire workload includes all necessary tooling and dashboard components.

### Verification

```bash
# Verify .NET version
dotnet --version

# Verify Docker is running
docker ps

# Verify Aspire workload
dotnet workload list | grep aspire
```

---

## 📁 Project Structure

The repository follows these conventions:

```
/src/RedisFlow/
├── RedisFlow.AppHost/          # Aspire orchestration host
│   └── AppHost.cs              # Configures Redis and application services
├── RedisFlow.ServiceDefaults/  # Shared Aspire service configurations
├── RedisFlow.Domain/           # Shared domain models and value objects
│   └── ValueObjects/Message.cs # Core message contract
├── RedisFlow.Services/         # Service contracts and implementations
│   └── Contracts/
│       ├── IProducer.cs        # Producer interface
│       └── IConsumer.cs        # Consumer interface
├── Tests.Producer/             # Producer test/demo applications
├── Tests.Consumers/            # Consumer test/demo applications
└── TestBase/                   # Shared test infrastructure

/tests/                          # (Future) Integration test projects
└── RedisFlow.Integration/      # Integration tests using Aspire Redis

/docs/
├── schemas/                    # Protocol buffer schemas (.proto files)
│   └── CHANGELOG.md           # Schema evolution tracking
└── Test structure.md          # Testing conventions and patterns
```

### Key Conventions

- **Producers and consumers**: Console applications under `src/` for demonstration
- **Shared contracts**: Interfaces in `RedisFlow.Services.Contracts`
- **Domain models**: Immutable value objects in `RedisFlow.Domain`
- **Integration tests**: Tests in `/tests/` directory using NUnit + FluentAssertions
- **Protocol Buffers**: Schema files in `/docs/schemas/` with version tracking

---

## 🚀 Getting Started

### 1. Clone and Build

```bash
git clone https://github.com/Peppe426/RedisFlow.git
cd RedisFlow

# Build all projects
cd src/RedisFlow/RedisFlow.AppHost
dotnet build
```

### 2. Configure the AppHost

The `RedisFlow.AppHost` project orchestrates all services. To add Redis support:

```csharp
// src/RedisFlow/RedisFlow.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

// Add Redis container
var redis = builder.AddRedis("redis")
    .WithRedisCommander();  // Optional: adds Redis Commander UI

// Add producer projects (when implemented)
builder.AddProject<Projects.Tests_Producer>("producer1")
    .WithReference(redis);

builder.AddProject<Projects.Tests_Producer>("producer2")
    .WithReference(redis);

// Add consumer project (when implemented)
builder.AddProject<Projects.Tests_Consumers>("consumer")
    .WithReference(redis);

builder.Build().Run();
```

### 3. Run the AppHost

```bash
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

This will:
- Start the Aspire dashboard (typically at `http://localhost:15888`)
- Launch a Redis container via Docker
- Start configured producer and consumer applications
- Provide service discovery and connection string management

### 4. Access the Aspire Dashboard

Open your browser to the URL shown in the console (e.g., `http://localhost:15888`). The dashboard provides:
- Real-time service status
- Log aggregation from all services
- Redis connection information
- Container health monitoring

---

## 🎮 Running the POC

### Starting Producers

Producers implement the `IProducer` interface and publish messages to a Redis stream:

```bash
# Run producer manually (when implemented)
cd src/RedisFlow/Tests.Producer
dotnet run

# Or let Aspire manage it via AppHost
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

**Expected behavior**:
- Producers serialize messages using Protocol Buffers
- Messages are appended to the configured Redis stream
- Each message includes: producer ID, content, timestamp
- Stream IDs are logged for diagnostics

### Starting the Consumer

The consumer implements `IConsumer` and processes messages using consumer groups:

```bash
# Run consumer manually (when implemented)
cd src/RedisFlow/Tests.Consumers
dotnet run

# Or let Aspire manage it
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

**Expected behavior**:
- Consumer joins a Redis consumer group
- Processes pending messages (unacknowledged) first
- Acknowledges messages after successful handling
- Supports graceful shutdown with cancellation

### Message Flow

1. **Producers** → Serialize `Message` using protobuf → `XADD` to Redis stream
2. **Redis Stream** → Stores messages persistently with unique IDs
3. **Consumer Group** → Distributes messages across consumers
4. **Consumer** → `XREADGROUP` → Deserialize → Process → `XACK`

---

## 💪 Resilience Scenarios

The POC demonstrates several resilience patterns:

### Scenario 1: Producer Goes Offline

**Steps**:
1. Start both producers and consumer via AppHost
2. Stop one producer (Ctrl+C or via Aspire dashboard)
3. Observe the remaining producer continues publishing
4. Verify consumer processes messages from the active producer

**Expected outcome**:
- Stream continues accepting messages from active producer
- Consumer processes all available messages
- No message loss or duplication

### Scenario 2: Consumer Restart with Pending Messages

**Steps**:
1. Start producers and consumer
2. Stop the consumer while producers are running
3. Producers continue writing to the stream
4. Restart the consumer

**Expected outcome**:
- Messages written during consumer downtime remain in the stream
- Consumer processes pending messages from the Pending Entries List (PEL)
- All messages are eventually acknowledged
- Stream IDs show no gaps in processing

**Verification**:
```bash
# Connect to Redis via CLI
docker exec -it <redis-container-id> redis-cli

# Check stream length
XLEN mystream

# Check pending messages
XPENDING mystream mygroup

# Inspect consumer group info
XINFO GROUPS mystream
```

### Scenario 3: Message Persistence Across Restarts

**Steps**:
1. Produce messages to the stream
2. Stop all producers and consumers
3. Stop the AppHost (stops Redis container)
4. Restart AppHost and consumer

**Expected outcome**:
- Redis stream data is persisted (if configured with volume mounting)
- Consumer reprocesses any messages not previously acknowledged
- Message replay demonstrates durability

---

## 🧪 Integration Tests

Integration tests verify the full produce/consume/replay workflow using an Aspire-hosted Redis instance.

### Running Integration Tests

```bash
# Navigate to integration test project (when implemented)
cd tests/RedisFlow.Integration

# Run all integration tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test category
dotnet test --filter Category="Redis"
```

### Test Structure

Integration tests follow the conventions in [`docs/Test structure.md`](/docs/Test%20structure.md):

```csharp
[TestFixture]
[Category("Integration")]
public class RedisStreamIntegrationTests
{
    [Test]
    public async Task Should_ConsumeMessage_When_ProducerPublishes()
    {
        // Given
        var producer = CreateProducer();
        var consumer = CreateConsumer();
        var message = new Message("producer1", "test content");

        // When
        await producer.ProduceAsync(message);
        var consumed = await consumer.ConsumeNextAsync();

        // Then
        consumed.Should().NotBeNull();
        consumed.Content.Should().Be("test content");
    }
}
```

### Test Coverage

Integration tests should verify:
- ✅ Producer serializes and publishes messages
- ✅ Consumer deserializes and processes messages
- ✅ Consumer group acknowledgment works correctly
- ✅ Pending messages are replayed on consumer restart
- ✅ Multiple consumers distribute load correctly
- ✅ Message ordering within a stream is preserved

### Setting Up Test Redis

Tests use Aspire to provision a Redis instance:

```csharp
// Example test base class
public class RedisIntegrationTestBase
{
    protected IDistributedApplicationTestingBuilder Builder { get; private set; }
    
    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        Builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.RedisFlow_AppHost>();
        await Builder.StartAsync();
    }
    
    [OneTimeTearDown]
    public async Task OneTimeTeardown()
    {
        await Builder.DisposeAsync();
    }
}
```

---

## 🛠️ Development Tips

### Inspecting Redis Streams

**Using Redis CLI**:
```bash
# Get Redis container ID
docker ps | grep redis

# Connect to Redis CLI
docker exec -it <container-id> redis-cli

# List all streams
SCAN 0 MATCH * TYPE stream

# Read stream contents
XRANGE mystream - +

# Check stream info
XINFO STREAM mystream

# Monitor live commands
MONITOR
```

**Using Redis Commander** (if configured in AppHost):
- Access via browser: `http://localhost:8081`
- Visual interface for exploring keys, streams, and consumer groups

### Viewing Consumer Groups

```bash
# List consumer groups for a stream
XINFO GROUPS mystream

# List consumers in a group
XINFO CONSUMERS mystream mygroup

# Check pending messages
XPENDING mystream mygroup - + 10
```

### Diagnostics and Logging

**Enable detailed logging** in `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "RedisFlow": "Trace",
      "StackExchange.Redis": "Debug"
    }
  }
}
```

**Stream ID tracking**: Producers should log the Redis-assigned stream ID for each message:
```csharp
var streamId = await redis.StreamAddAsync("mystream", entries);
logger.LogInformation("Message published with ID: {StreamId}", streamId);
```

### Troubleshooting

**Issue**: AppHost fails to start Redis container
- **Solution**: Ensure Docker Desktop is running: `docker ps`

**Issue**: Cannot connect to Redis from producer/consumer
- **Solution**: Check connection string in Aspire dashboard; ensure service references are configured

**Issue**: Messages not being consumed
- **Solution**: Verify consumer group exists: `XINFO GROUPS mystream` in Redis CLI

**Issue**: Pending messages accumulate
- **Solution**: Check consumer acknowledgment logic; ensure `XACK` is called after processing

---

## 🔄 Serialization Strategy

### Protocol Buffers (Protobuf)

RedisFlow uses **Protocol Buffers** for message serialization to ensure:
- **Efficiency**: Compact binary format with minimal overhead
- **Schema Evolution**: Forward/backward compatibility with versioned schemas
- **Type Safety**: Strongly-typed contracts shared across producers and consumers
- **No JSON**: Avoids JSON serialization for stream payloads (JSON is only for admin tools)

### Schema Definition

Proto schemas are stored in [`docs/schemas/`](/docs/schemas/) and tracked for changes:

```protobuf
// docs/schemas/message.proto
syntax = "proto3";

package redisflow;

import "google/protobuf/timestamp.proto";

message Message {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
}
```

### Schema Evolution Rules

As documented in [`docs/schemas/CHANGELOG.md`](/docs/schemas/CHANGELOG.md):
- ❌ **Never reuse tag numbers** (reserve deleted fields)
- ❌ **Never change field types** (add new fields instead)
- ✅ **Use `reserved` for deleted fields**
- ✅ **Add new optional fields** for backward compatibility
- ✅ **Document all schema changes** in CHANGELOG

### Code Generation

Proto files are compiled to C# during build using `Grpc.Tools`:

```xml
<!-- In RedisFlow.Domain.csproj or shared project -->
<ItemGroup>
  <Protobuf Include="..\..\..\docs\schemas\message.proto" GrpcServices="None" />
</ItemGroup>
```

> **Note**: The path is relative to the project file location. Adjust based on your project structure (e.g., from `src/RedisFlow/RedisFlow.Domain/` to `docs/schemas/`).

Generated types are **not checked into source control** (regenerated on build).

### Example Usage

```csharp
// Producer
var message = new Message
{
    Producer = "producer1",
    Content = "Hello, Redis!",
    CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
};

byte[] payload = message.ToByteArray();
await redis.StreamAddAsync("mystream", "data", payload);

// Consumer
var entries = await redis.StreamReadGroupAsync("mystream", "mygroup", "consumer1");
foreach (var entry in entries)
{
    var payload = entry.Values.First(v => v.Name == "data").Value;
    var message = Message.Parser.ParseFrom((byte[])payload);
    
    // Process message...
    
    await redis.StreamAcknowledgeAsync("mystream", "mygroup", entry.Id);
}
```

---

## 📚 Related Resources

- **Issues**: See [GitHub Issues](https://github.com/Peppe426/RedisFlow/issues) for detailed requirements
  - [#1 - Set up Aspire host and Redis infrastructure](https://github.com/Peppe426/RedisFlow/issues/1)
  - [#2 - Implement producers](https://github.com/Peppe426/RedisFlow/issues/2)
  - [#3 - Implement consumer group processing](https://github.com/Peppe426/RedisFlow/issues/3)
  - [#4 - Demonstrate message persistence](https://github.com/Peppe426/RedisFlow/issues/4)
  - [#5 - Validate resilience scenarios](https://github.com/Peppe426/RedisFlow/issues/5)
- **Test Structure**: [`docs/Test structure.md`](/docs/Test%20structure.md)
- **Schema Evolution**: [`docs/schemas/CHANGELOG.md`](/docs/schemas/CHANGELOG.md)
- **Aspire Documentation**: https://learn.microsoft.com/dotnet/aspire/
- **Redis Streams**: https://redis.io/docs/data-types/streams/

---

## 🤝 Contributing

Contributions are welcome! Please follow:
- [`.github/copilot-instructions.md`](/.github/copilot-instructions.md) for coding standards
- [`docs/Test structure.md`](/docs/Test%20structure.md) for test conventions
- Protocol Buffer schema rules when modifying message contracts

---

## 📄 License

See [LICENSE](LICENSE) file for details.
