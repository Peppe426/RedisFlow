# RedisFlow Architecture

## Overview
RedisFlow demonstrates message persistence and replay semantics using Redis Streams with .NET 9, Protocol Buffers, and .NET Aspire.

## Architecture Diagram

```
┌─────────────┐         ┌─────────────────────┐         ┌──────────────┐
│  Producer 1 │────────▶│                     │◀───────▶│  Consumer 1  │
└─────────────┘         │   Redis Streams     │         └──────────────┘
                        │   (with Consumer    │
┌─────────────┐         │    Groups & PEL)    │         ┌──────────────┐
│  Producer 2 │────────▶│                     │◀───────▶│  Consumer 2  │
└─────────────┘         └─────────────────────┘         └──────────────┘
                                  │
                        ┌─────────▼─────────┐
                        │  Protobuf Schema  │
                        │  (docs/schemas/)  │
                        └───────────────────┘
```

## Components

### 1. Domain Layer (`RedisFlow.Domain`)
**Purpose**: Core business entities and value objects

**Key Classes**:
- `Message`: Immutable value object representing a stream message
  - `Producer`: String identifier of the producer
  - `Content`: Message payload
  - `CreatedAt`: UTC timestamp

**Dependencies**:
- `Google.Protobuf`: For protobuf type generation
- `Grpc.Tools`: Build-time protobuf compiler

**Protobuf Schema** (`docs/schemas/message.proto`):
```protobuf
message StreamMessage {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
}
```

### 2. Services Layer (`RedisFlow.Services`)
**Purpose**: Infrastructure implementations for producers and consumers

**Key Classes**:

#### `RedisStreamProducer` : `IProducer`
Publishes messages to Redis Streams using protobuf serialization.

**Features**:
- Serializes `Message` to protobuf bytes
- Publishes to Redis Stream with `XADD`
- Logs message IDs for diagnostics

**Configuration**:
- `streamKey`: Name of the Redis Stream (default: "messages")

#### `RedisStreamConsumer` : `IConsumer`
Consumes messages from Redis Streams using consumer groups.

**Features**:
- **Automatic Consumer Group Creation**: Creates group if it doesn't exist
- **Pending Message Replay**: Processes PEL messages on startup
- **Message Acknowledgment**: Auto-acknowledges after successful processing
- **Error Handling**: Failed messages remain in PEL for retry
- **Continuous Processing**: Polls for new messages with backoff

**Configuration**:
- `consumerGroup`: Name of the consumer group (default: "default-group")
- `consumerName`: Unique consumer identifier (default: auto-generated GUID)
- `streamKey`: Name of the Redis Stream (default: "messages")

**Processing Flow**:
1. Ensure consumer group exists
2. Process pending messages from PEL (`XREADGROUP` with "0")
3. Continuously read new messages (`XREADGROUP` with ">")
4. Deserialize protobuf → Domain `Message`
5. Invoke message handler
6. Acknowledge with `XACK` on success
7. Leave in PEL on failure for retry

**Dependencies**:
- `StackExchange.Redis`: Redis client
- `Google.Protobuf`: Protobuf deserialization
- `Microsoft.Extensions.Logging`: Structured logging

### 3. AppHost (`RedisFlow.AppHost`)
**Purpose**: .NET Aspire orchestration for local development

**Configuration**:
```csharp
var redis = builder.AddRedis("redis")
    .WithDataVolume(); // Enables data persistence
```

**Features**:
- Automatic Redis container provisioning
- Connection string management
- Service discovery
- Dashboard for monitoring

### 4. Integration Tests (`tests/RedisFlow.Integration`)
**Purpose**: End-to-end validation of replay semantics

**Test Infrastructure**:
- **Testcontainers**: Manages Docker containers for Redis
- **NUnit**: Test framework
- **FluentAssertions**: Expressive assertions

**Test Scenarios**:

| Test | Validates |
|------|-----------|
| `Should_ProduceAndConsumeMessage_When_ConsumerIsOnline` | Basic produce/consume flow |
| `Should_PersistMessages_When_ConsumerIsOffline` | Messages persist when consumer is down |
| `Should_ReplayPendingMessages_When_ConsumerRestarts` | PEL replay on restart |
| `Should_MaintainPendingMessages_When_ConsumerCrashesBeforeAcknowledge` | Messages remain pending if not acknowledged |
| `Should_ContinueProcessingNewMessages_After_ReplayingPending` | New messages processed after replay |

**Test Lifecycle**:
1. **SetUp**: Start Redis container, create unique stream key
2. **Test**: Execute scenario
3. **TearDown**: Clean up stream, stop container

## Message Flow

### Normal Flow (Consumer Online)
```
1. Producer → Serialize Message to Protobuf
2. Producer → XADD to Redis Stream
3. Consumer → XREADGROUP ">" (new messages)
4. Consumer → Deserialize Protobuf to Message
5. Consumer → Invoke Handler
6. Consumer → XACK (acknowledge)
```

### Replay Flow (Consumer Restart)
```
1. Consumer Starts
2. Consumer → Check XPENDING (get pending count)
3. Consumer → XREADGROUP "0" (read pending)
4. Consumer → Process Pending Messages
5. Consumer → XACK each message
6. Consumer → XREADGROUP ">" (continue with new)
```

### Crash Recovery Flow
```
1. Consumer → XREADGROUP (read message)
2. [Consumer Crashes - No XACK]
3. Message → Remains in PEL
4. New Consumer Starts
5. New Consumer → XREADGROUP "0" (reads pending)
6. New Consumer → Processes Message
7. New Consumer → XACK (acknowledges)
```

## Key Design Decisions

### Why Protocol Buffers?
1. **Schema Evolution**: Add fields without breaking consumers
2. **Type Safety**: Compile-time validation
3. **Efficiency**: Smaller payload than JSON
4. **Documentation**: Schema serves as API contract

### Why Consumer Groups?
1. **Parallel Processing**: Multiple consumers share load
2. **Message Persistence**: PEL tracks unacknowledged messages
3. **Fault Tolerance**: Messages survive consumer crashes
4. **At-Least-Once Delivery**: Ensures message processing

### Why .NET Aspire?
1. **Local Development**: Easy Redis provisioning
2. **Service Discovery**: Automatic connection strings
3. **Observability**: Built-in dashboard
4. **Production-Ready**: Aligns with on-prem deployments

## Performance Considerations

### Producer Optimizations
- **Batching**: Use pipelines for high throughput
- **Connection Pooling**: Reuse `IConnectionMultiplexer`
- **Async**: Non-blocking I/O operations

### Consumer Optimizations
- **Batch Reading**: `count` parameter in `XREADGROUP`
- **Parallel Processing**: Multiple consumers in same group
- **Backoff Strategy**: Configurable delay when idle

### Stream Management
- **MAXLEN**: Limit stream size for memory management
- **XTRIM**: Periodic cleanup of old messages
- **TTL**: Consider time-based retention policies

## Monitoring & Observability

### Key Metrics
- **Stream Length**: `XLEN` - Total messages in stream
- **Pending Count**: `XPENDING` - Unacknowledged messages
- **Consumer Lag**: Time between produce and consume
- **Error Rate**: Failed message processing

### Logging
Both producer and consumer log:
- Message IDs for correlation
- Producer/Consumer identifiers
- Acknowledgment status
- Error details for troubleshooting

### Diagnostics
```bash
# Stream info
redis-cli XINFO STREAM messages

# Consumer group info
redis-cli XINFO GROUPS messages

# Pending messages
redis-cli XPENDING messages my-group
```

## Deployment Considerations

### Development
- Use .NET Aspire with local Docker
- Auto-provisioned Redis container
- Volume persistence for testing replay

### Production
- Redis Enterprise or Azure Cache for Redis
- Configure `maxmemory` and eviction policies
- Enable RDB/AOF persistence
- Monitor PEL growth
- Implement XTRIM jobs
- Use multiple consumer instances
- Configure appropriate retention policies

## Future Enhancements

### Potential Improvements
1. **Dead Letter Queue**: Move failed messages after N retries
2. **Metrics Export**: Prometheus/Grafana integration
3. **Message Routing**: Topic-based message distribution
4. **Exactly-Once Semantics**: Idempotency keys
5. **Schema Registry**: Centralized protobuf schema management
6. **Multi-Stream Support**: Route messages to different streams

## References
- [Redis Streams Tutorial](https://redis.io/docs/data-types/streams-tutorial/)
- [Protocol Buffers Documentation](https://protobuf.dev/)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/)
