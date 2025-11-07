# RedisFlow Example Usage

## Quick Start Example

This example demonstrates how to create a producer and consumer using RedisFlow.

### Producer Example

```csharp
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Connect to Redis
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var logger = loggerFactory.CreateLogger<RedisStreamProducer>();

// Create producer
var producer = new RedisStreamProducer(redis, logger, streamKey: "events");

// Produce some messages
for (int i = 0; i < 10; i++)
{
    var message = new Message(
        producer: "example-producer",
        content: $"Event {i} - {DateTime.UtcNow}");
    
    await producer.ProduceAsync(message);
    
    Console.WriteLine($"Produced message {i}");
    await Task.Delay(1000); // Simulate workload
}

Console.WriteLine("Producer complete!");
```

### Consumer Example

```csharp
using Microsoft.Extensions.Logging;
using RedisFlow.Domain.ValueObjects;
using RedisFlow.Services.Implementations;
using StackExchange.Redis;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Connect to Redis
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var logger = loggerFactory.CreateLogger<RedisStreamConsumer>();

// Create consumer
var consumer = new RedisStreamConsumer(
    redis, 
    logger, 
    streamKey: "events",
    groupName: "example-group",
    consumerName: "consumer-1");

// Define message handler
async Task HandleMessage(Message message, CancellationToken ct)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Received from {message.Producer}: {message.Content}");
    
    // Simulate processing
    await Task.Delay(500, ct);
    
    // Message is automatically acknowledged after successful completion
}

// Start consuming with cancellation support
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nShutting down consumer...");
};

Console.WriteLine("Consumer started. Press Ctrl+C to stop.");
await consumer.ConsumeAsync(HandleMessage, cts.Token);

Console.WriteLine("Consumer stopped gracefully.");
```

### Running with Aspire

1. Start the AppHost which includes Redis:

```bash
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

This will:
- Start a Redis container
- Open the Aspire dashboard
- Display the Redis connection string

2. In separate terminals, run the producer and consumer:

```bash
# Terminal 1 - Producer
cd examples/producer
dotnet run

# Terminal 2 - Consumer
cd examples/consumer
dotnet run
```

## Multiple Consumers Example

You can run multiple consumers in the same group for parallel processing:

```csharp
// Consumer 1
var consumer1 = new RedisStreamConsumer(
    redis, logger, "events", "worker-group", "worker-1");

// Consumer 2  
var consumer2 = new RedisStreamConsumer(
    redis, logger, "events", "worker-group", "worker-2");

// Both consumers will share the workload
var task1 = consumer1.ConsumeAsync(HandleMessage, cts.Token);
var task2 = consumer2.ConsumeAsync(HandleMessage, cts.Token);

await Task.WhenAll(task1, task2);
```

## Error Handling and Retry

Messages are automatically retried if the handler throws an exception:

```csharp
async Task HandleMessageWithRetry(Message message, CancellationToken ct)
{
    try
    {
        // Process message
        await ProcessAsync(message, ct);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing message: {ex.Message}");
        
        // Throw to keep message in pending list for retry
        throw;
    }
}
```

The consumer will:
1. Not acknowledge the failed message
2. Leave it in the pending list
3. Automatically claim and retry it after 5 seconds

## Monitoring with Redis CLI

```bash
# View stream info
redis-cli XINFO STREAM events

# View consumer groups
redis-cli XINFO GROUPS events

# View consumers in a group
redis-cli XINFO CONSUMERS events worker-group

# View pending messages
redis-cli XPENDING events worker-group

# View stream length
redis-cli XLEN events
```

## Testing

### Unit Tests

Unit tests use Moq to mock Redis:

```bash
cd src/RedisFlow
dotnet test --filter "TestCategory!=IntegrationTest"
```

### Integration Tests

Integration tests use Aspire to launch a real Redis instance:

```bash
cd src/RedisFlow
dotnet test --filter "TestCategory=IntegrationTest"
```

Note: Integration tests require Docker to be running.

## Best Practices

1. **Use meaningful consumer names** for debugging and monitoring
2. **Handle exceptions appropriately** - throw to retry, swallow to skip
3. **Monitor pending lists** to detect stuck messages
4. **Use separate groups** for different processing workflows
5. **Set appropriate timeouts** in handlers to prevent blocking
6. **Log message IDs** for traceability
7. **Use cancellation tokens** for graceful shutdown
8. **Test with multiple consumers** to ensure parallel processing works
