# Manual Resilience Testing Guide

This document provides step-by-step instructions for manually validating the resilience scenarios in RedisFlow.

---

## Prerequisites

1. Redis running (via Aspire or Docker):
   ```bash
   # Option 1: Aspire AppHost
   cd src/RedisFlow/RedisFlow.AppHost
   dotnet run
   
   # Option 2: Docker
   docker run -d -p 6379:6379 --name redis-resilience redis:latest
   ```

2. Redis CLI for inspection:
   ```bash
   # Install if needed
   # macOS: brew install redis
   # Ubuntu: sudo apt-get install redis-tools
   # Windows: Use Docker or WSL
   ```

---

## Test Scenario 1: One Producer Goes Offline

**Objective:** Verify the system continues processing messages when one producer stops sending.

### Setup

1. Open 3 terminal windows
2. Start Redis (see Prerequisites)

### Steps

**Terminal 1: Start Consumer**

```bash
cd src/RedisFlow
dotnet run --project RedisFlow.Services
# Or create a simple consumer app:
```

Create a file `ManualConsumerApp.csx`:
```csharp
#r "nuget: StackExchange.Redis, 2.9.32"
using StackExchange.Redis;
using RedisFlow.Services;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var consumer = new RedisConsumer(redis, "test-stream", "test-group", "consumer1");

Console.WriteLine("Consumer started. Waiting for messages...");
await consumer.ConsumeAsync(async (msg, ct) => {
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Received: {msg.Producer} -> {msg.Content}");
    await Task.CompletedTask;
}, CancellationToken.None);
```

**Terminal 2: Producer 1**

Create `Producer1.csx`:
```csharp
#r "nuget: StackExchange.Redis, 2.9.32"
using StackExchange.Redis;
using RedisFlow.Services;
using RedisFlow.Domain.ValueObjects;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var producer = new RedisProducer(redis, "test-stream");

for (int i = 1; i <= 10; i++)
{
    await producer.ProduceAsync(new Message("Producer1", $"Message {i}"));
    Console.WriteLine($"[Producer1] Sent Message {i}");
    await Task.Delay(1000);
}
Console.WriteLine("[Producer1] Going offline...");
```

**Terminal 3: Producer 2**

Create `Producer2.csx`:
```csharp
#r "nuget: StackExchange.Redis, 2.9.32"
using StackExchange.Redis;
using RedisFlow.Services;
using RedisFlow.Domain.ValueObjects;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var producer = new RedisProducer(redis, "test-stream");

await Task.Delay(5000); // Wait 5 seconds

for (int i = 1; i <= 10; i++)
{
    await producer.ProduceAsync(new Message("Producer2", $"Message {i}"));
    Console.WriteLine($"[Producer2] Sent Message {i}");
    await Task.Delay(1000);
}
```

### Execute

1. Start Consumer (Terminal 1)
2. Start Producer 1 (Terminal 2) - sends 10 messages then stops
3. Start Producer 2 (Terminal 3) - continues sending after Producer 1 stops

### Expected Results

- Consumer receives all 20 messages total
- After Producer 1 stops at message 10, Producer 2 messages still arrive
- Consumer shows no interruption in processing

### Verification

Check stream and consumer group status:

```bash
# Total messages in stream
redis-cli XLEN test-stream

# Consumer group info
redis-cli XINFO GROUPS test-stream

# Pending messages (should be 0 if all acknowledged)
redis-cli XPENDING test-stream test-group
```

**Expected Output:**
```
XLEN test-stream
-> 20

XINFO GROUPS test-stream
-> [Group: test-group, Pending: 0, ...]

XPENDING test-stream test-group
-> [0, nil, nil, nil]
```

---

## Test Scenario 2: Consumer Restart with Pending Messages

**Objective:** Verify pending messages are recovered when a consumer restarts.

### Setup

1. Clean previous test data:
   ```bash
   redis-cli DEL test-stream
   ```

### Steps

**Step 1: Produce Messages**

```bash
cd src/RedisFlow
```

Create `ProduceMessages.csx`:
```csharp
#r "nuget: StackExchange.Redis, 2.9.32"
using StackExchange.Redis;
using RedisFlow.Services;
using RedisFlow.Domain.ValueObjects;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var producer = new RedisProducer(redis, "test-stream");

for (int i = 1; i <= 5; i++)
{
    await producer.ProduceAsync(new Message("Producer", $"Message {i}"));
    Console.WriteLine($"Produced Message {i}");
}
Console.WriteLine("All messages produced.");
```

Run: `dotnet script ProduceMessages.csx`

**Step 2: Start Consumer That Crashes**

Create `CrashingConsumer.csx`:
```csharp
#r "nuget: StackExchange.Redis, 2.9.32"
using StackExchange.Redis;
using RedisFlow.Services;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var consumer = new RedisConsumer(redis, "test-stream", "test-group", "consumer1");

var count = 0;
var cts = new CancellationTokenSource();

try 
{
    await consumer.ConsumeAsync(async (msg, ct) => {
        count++;
        Console.WriteLine($"Processing: {msg.Content}");
        
        if (count >= 3) 
        {
            Console.WriteLine("!!! SIMULATING CRASH - NOT ACKNOWLEDGING !!!");
            cts.Cancel();
            throw new Exception("Simulated crash");
        }
        
        await Task.CompletedTask;
    }, cts.Token);
}
catch (Exception ex)
{
    Console.WriteLine($"Consumer crashed: {ex.Message}");
}
```

Run: `dotnet script CrashingConsumer.csx`

**Step 3: Verify Pending Messages**

```bash
# Check pending messages
redis-cli XPENDING test-stream test-group

# Should show 2-3 pending messages
```

**Step 4: Restart Consumer**

Create `RestartedConsumer.csx`:
```csharp
#r "nuget: StackExchange.Redis, 2.9.32"
using StackExchange.Redis;
using RedisFlow.Services;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var consumer = new RedisConsumer(redis, "test-stream", "test-group", "consumer1");

Console.WriteLine("Restarted consumer - processing pending messages...");

var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await consumer.ConsumeAsync(async (msg, ct) => {
    Console.WriteLine($"[RECOVERED] {msg.Content}");
    await Task.CompletedTask;
}, cts.Token);
```

Run: `dotnet script RestartedConsumer.csx`

### Expected Results

- CrashingConsumer processes 2 messages successfully
- Consumer crashes before acknowledging message 3
- `XPENDING` shows 2-3 pending messages
- RestartedConsumer automatically recovers and processes all pending messages
- After restart, `XPENDING` shows 0 pending messages

### Verification

```bash
# After restart, pending should be 0
redis-cli XPENDING test-stream test-group

# All messages acknowledged
redis-cli XINFO CONSUMERS test-stream test-group
```

---

## Test Scenario 3: Combined - Producer Offline + Consumer Restart

**Objective:** Validate full system resilience under multiple failure conditions.

### Steps

**Step 1: Start Initial State**

1. Clean Redis: `redis-cli DEL test-stream`
2. Start Consumer (long-running)
3. Start Producer 1 (sends 5 messages, then stops)
4. Start Producer 2 (sends 5 messages)

**Step 2: Simulate Failures**

1. Stop Consumer after it processes 3 messages (Ctrl+C)
2. Stop Producer 1 (simulated offline)
3. Producer 2 continues sending

**Step 3: Recovery**

1. Restart Consumer
2. Verify it processes:
   - Pending messages from before crash
   - New messages from Producer 2

### Expected Results

- Consumer processes all 10 messages (5 from each producer)
- Pending messages recovered after consumer restart
- No message loss despite failures

### Verification

```bash
# Total messages
redis-cli XLEN test-stream  # Should be 10

# All acknowledged
redis-cli XPENDING test-stream test-group  # Should be [0, nil, nil, nil]

# Check stream entries
redis-cli XRANGE test-stream - +
```

---

## Test Scenario 4: Multiple Consumers (Load Balancing)

**Objective:** Demonstrate consumer group load balancing and failover.

### Steps

1. Clean Redis: `redis-cli DEL test-stream`

2. Start 2 consumers in same group:
   - Consumer A: `consumer-group`, `consumer-A`
   - Consumer B: `consumer-group`, `consumer-B`

3. Send 10 messages from a producer

4. Observe messages distributed between consumers

5. Stop Consumer A

6. Send 5 more messages

7. Observe Consumer B processes all new messages

8. Restart Consumer A

9. Verify Consumer A claims any pending messages from before it stopped

### Expected Results

- Messages load-balanced between active consumers
- After Consumer A stops, Consumer B handles all new messages
- Consumer A recovers pending messages on restart

---

## Observability Commands

### Stream Inspection

```bash
# Stream length
redis-cli XLEN test-stream

# First 10 messages
redis-cli XRANGE test-stream - + COUNT 10

# Last 10 messages
redis-cli XREVRANGE test-stream + - COUNT 10

# Stream info
redis-cli XINFO STREAM test-stream
```

### Consumer Group Inspection

```bash
# List consumer groups
redis-cli XINFO GROUPS test-stream

# List consumers in a group
redis-cli XINFO CONSUMERS test-stream test-group

# Pending messages summary
redis-cli XPENDING test-stream test-group

# Pending messages detail
redis-cli XPENDING test-stream test-group - + 10
```

### Manual Message Claiming

```bash
# Claim a specific pending message
redis-cli XCLAIM test-stream test-group consumer1 0 <message-id>

# Claim idle messages (idle for 60000ms)
redis-cli XCLAIM test-stream test-group consumer1 60000 <message-id>
```

### Cleanup

```bash
# Delete stream
redis-cli DEL test-stream

# Delete multiple keys
redis-cli DEL test-stream test-stream-2 test-stream-3
```

---

## Troubleshooting Tips

### Issue: Consumer doesn't see messages

**Check:**
```bash
# Verify messages exist
redis-cli XLEN test-stream

# Check consumer is reading from correct position
# Consumer should read with ">" for new messages
# Or specific ID for pending messages
```

### Issue: Pending messages not recovered

**Check:**
```bash
# Verify consumer name matches
redis-cli XINFO CONSUMERS test-stream test-group

# Manually claim if needed
redis-cli XCLAIM test-stream test-group consumer1 0 <pending-message-id>
```

### Issue: BUSYGROUP error when creating consumer group

**This is expected** - it means the consumer group already exists. The code handles this gracefully.

---

## Summary

These manual tests validate:

✅ System continues operating when producers go offline  
✅ Pending messages are recovered after consumer restart  
✅ Consumer groups provide load balancing and failover  
✅ Redis Streams PEL (Pending Entries List) management works correctly  
✅ Message serialization with protobuf is reliable  

For automated validation, run:
```bash
dotnet test src/RedisFlow/Tests.Consumers --filter "Category=Integration test"
```
