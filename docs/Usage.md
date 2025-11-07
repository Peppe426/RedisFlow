# RedisFlow Usage Guide

## Overview

RedisFlow provides Redis Streams-based producer and consumer implementations using Protocol Buffers for efficient message serialization.

## Architecture

- **Producer**: `RedisStreamProducer` - Publishes messages to Redis Streams using protobuf serialization
- **Consumer**: `RedisStreamConsumer` - Consumes messages from Redis Streams using consumer groups with automatic acknowledgment
- **Serialization**: Protocol Buffers (protobuf) for compact, schema-aware message encoding

## Getting Started

### 1. Start Redis using Aspire

The AppHost is configured to run Redis in a container:

```bash
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

This will:
- Start Redis container with persistent lifetime
- Expose Redis on the default port
- Provide the Aspire dashboard for monitoring

### 2. Using the Producer

```csharp
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

// Connect to Redis
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

// Create producer
var logger = loggerFactory.CreateLogger<RedisStreamProducer>();
var producer = new RedisStreamProducer(redis, logger, streamKey: "messages");

// Produce a message
var message = new Message("producer-1", "Hello, Redis Streams!");
await producer.ProduceAsync(message);
```

### 3. Using the Consumer

```csharp
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;

// Connect to Redis
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

// Create consumer
var logger = loggerFactory.CreateLogger<RedisStreamConsumer>();
var consumer = new RedisStreamConsumer(
    redis, 
    logger, 
    streamKey: "messages",
    groupName: "consumer-group-1",
    consumerName: "consumer-1"); // Optional, auto-generated if not provided

// Define handler
async Task MessageHandler(Message message, CancellationToken ct)
{
    Console.WriteLine($"Received from {message.Producer}: {message.Content}");
    // Process the message
    // Message will be automatically acknowledged after successful processing
}

// Start consuming
using var cts = new CancellationTokenSource();
await consumer.ConsumeAsync(MessageHandler, cts.Token);
```

## Consumer Group Behavior

### Automatic Acknowledgment

Messages are automatically acknowledged (ACK) after the handler completes successfully. If the handler throws an exception, the message remains in the pending list for retry.

### Pending Message Recovery

The consumer automatically:
1. Checks for pending messages on startup and periodically
2. Claims messages that have been idle for more than 5 seconds
3. Reprocesses claimed messages
4. Acknowledges them on successful processing

### Graceful Shutdown

When cancellation is requested:
- The consumer stops reading new messages
- Completes processing of the current message
- Exits cleanly without leaving the consumer group in an inconsistent state

## Message Schema

The protobuf schema is defined in `docs/schemas/message.proto`:

```protobuf
syntax = "proto3";

option csharp_namespace = "RedisFlow.Domain.Proto";

import "google/protobuf/timestamp.proto";

message MessageProto {
    string producer = 1;
    string content = 2;
    google.protobuf.Timestamp created_at = 3;
}
```

## Integration Testing

Integration tests should:
1. Use Aspire to launch a Redis instance
2. Verify produce/consume/replay flows
3. Test consumer group behavior and pending message handling

Example structure:

```bash
dotnet test tests/RedisFlow.Integration
```

## Resilience Features

### Producer Offline Scenarios

- Messages are queued in Redis Streams
- Consumers continue processing existing messages
- New messages appear when producer comes back online

### Consumer Restart Scenarios

- Pending messages are automatically claimed and reprocessed
- Consumer group state is maintained in Redis
- No message loss on consumer restart

### Stream Diagnostics

Both producer and consumer log message IDs for observability:
- Producer logs: Message ID on successful publish
- Consumer logs: Message ID on receive, process, and acknowledge

## Configuration Options

### Producer Options

- `streamKey`: Redis stream key (default: "messages")

### Consumer Options

- `streamKey`: Redis stream key (default: "messages")
- `groupName`: Consumer group name (default: "default-group")
- `consumerName`: Consumer identifier (default: auto-generated GUID)

## Best Practices

1. **Use consumer groups** for parallel processing across multiple consumers
2. **Monitor pending lists** using Redis commands: `XPENDING stream-key group-name`
3. **Set appropriate claim timeouts** based on expected message processing time
4. **Handle exceptions** in message handlers to avoid message loss
5. **Use structured logging** to track message flow and diagnose issues
6. **Keep protobuf messages small** for optimal performance

## Redis Commands for Monitoring

```bash
# View stream info
XINFO STREAM messages

# View consumer group info
XINFO GROUPS messages

# View consumers in a group
XINFO CONSUMERS messages consumer-group-1

# View pending messages
XPENDING messages consumer-group-1

# Manual message acknowledgment (if needed)
XACK messages consumer-group-1 1234-0
```

## Troubleshooting

### Messages not being consumed

1. Check consumer group exists: `XINFO GROUPS messages`
2. Check consumer is registered: `XINFO CONSUMERS messages consumer-group-1`
3. Check pending list: `XPENDING messages consumer-group-1`

### Messages stuck in pending list

- Verify consumer is running and not throwing exceptions
- Check claim timeout settings
- Manually claim messages if needed: `XCLAIM`

### Consumer not starting

- Verify Redis connection string
- Check Redis is running: `redis-cli PING`
- Review logs for connection errors
