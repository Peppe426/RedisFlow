# Running the RedisFlow POC

This guide explains how to run and observe the RedisFlow proof-of-concept.

## Prerequisites

Before running the POC, ensure you have:

1. **.NET 9 SDK** installed
   ```bash
   dotnet --version
   # Should show 9.0.x
   ```

2. **Docker Desktop** running
   ```bash
   docker version
   # Should show both client and server versions
   ```

3. **.NET Aspire Workload** installed
   ```bash
   dotnet workload list
   # Should show 'aspire' in the list
   ```

   If not installed:
   ```bash
   dotnet workload install aspire
   ```

## Running the Application

### Method 1: Using Aspire AppHost (Recommended)

This is the primary way to run the POC as it orchestrates all components together.

1. **Navigate to the AppHost directory:**
   ```bash
   cd src/RedisFlow
   ```

2. **Run the AppHost:**
   ```bash
   dotnet run --project RedisFlow.AppHost
   ```

3. **Access the Aspire Dashboard:**
   - The console will display a URL like: `https://localhost:17169`
   - Open this URL in your browser
   - You may need to accept the development certificate

4. **Observe the System:**

   In the Aspire Dashboard, you'll see:
   
   - **Resources Tab:**
     - `redis` - Redis container (should show "Running")
     - `producer1` - First producer application
     - `producer2` - Second producer application
     - `consumer` - Consumer application
   
   - **Console Logs Tab:**
     Click on each resource to see its logs:
     - **Producer1**: Messages sent every 5 seconds
     - **Producer2**: Messages sent every 3 seconds
     - **Consumer**: Messages being received and processed
   
   - **Traces Tab:**
     View distributed tracing information
   
   - **Metrics Tab:**
     Monitor performance metrics

### Method 2: Running Components Individually

For debugging or testing individual components:

1. **Start Redis manually:**
   ```bash
   docker run -d -p 6379:6379 redis:latest
   ```

2. **Run components in separate terminals:**

   Terminal 1 - Producer1:
   ```bash
   cd src/RedisFlow/Producer1
   dotnet run
   ```

   Terminal 2 - Producer2:
   ```bash
   cd src/RedisFlow/Producer2
   dotnet run
   ```

   Terminal 3 - Consumer:
   ```bash
   cd src/RedisFlow/Consumer
   dotnet run
   ```

   **Note:** When running manually, you'll need to configure connection strings via environment variables or `appsettings.json`.

## What You Should See

### Expected Behavior

1. **Startup:**
   - Redis container starts first
   - Producers and consumer connect to Redis
   - Consumer creates the consumer group automatically

2. **Normal Operation:**
   - Producer1 sends messages every 5 seconds
   - Producer2 sends messages every 3 seconds
   - Consumer processes messages from both producers
   - All logs visible in Aspire Dashboard

3. **Sample Log Output:**

   **Producer1:**
   ```
   info: ProducerWorker[0]
         Producer1 starting...
   info: RedisFlow.Services.RedisStreamProducer[0]
         Produced message to stream 'redisflow:stream' with ID '1699...' from producer 'Producer1'
   info: ProducerWorker[0]
         Producer1 sent message #1
   ```

   **Consumer:**
   ```
   info: ConsumerWorker[0]
         Consumer starting...
   info: RedisFlow.Services.RedisStreamConsumer[0]
         Starting consumer 'hostname' in group 'default-group' on stream 'redisflow:stream'
   info: ConsumerWorker[0]
         Received: [Producer1] Message #1 from Producer1 at 14:32:10
   ```

## Testing Resilience Scenarios

### Scenario 1: Producer Goes Offline

1. In the Aspire Dashboard, stop Producer1 (click Stop button)
2. Observe:
   - Producer2 continues sending messages
   - Consumer continues processing Producer2 messages
   - No errors or message loss

3. Restart Producer1
4. Observe:
   - Producer1 reconnects and resumes sending
   - All messages are processed

### Scenario 2: Consumer Restart

1. Stop the Consumer in Aspire Dashboard
2. Wait for producers to send a few messages (check stream length in Redis CLI)
3. Restart the Consumer
4. Observe in Consumer logs:
   - "Checking for pending messages in PEL..."
   - Consumer processes pending messages first
   - Then processes new messages

### Scenario 3: Redis Restart

1. Stop Redis container
2. Observe:
   - Producers log connection errors
   - Consumer logs connection errors
3. Restart Redis
4. Observe:
   - All components reconnect automatically
   - System resumes normal operation

## Inspecting Redis Directly

You can connect to the Redis container to inspect the stream:

```bash
# Get container ID
docker ps | grep redis

# Connect to Redis CLI
docker exec -it <container-id> redis-cli

# View stream information
XINFO STREAM redisflow:stream

# View consumer group info
XINFO GROUPS redisflow:stream

# View consumers in group
XINFO CONSUMERS redisflow:stream default-group

# View pending messages
XPENDING redisflow:stream default-group

# View all messages (first 10)
XRANGE redisflow:stream - + COUNT 10

# View stream length
XLEN redisflow:stream
```

### Understanding Redis Commands Output

- **XINFO STREAM**: Shows stream length, first/last entry IDs, etc.
- **XINFO GROUPS**: Shows consumer groups and their lag
- **XPENDING**: Shows messages that are delivered but not acknowledged
- **XRANGE**: Shows actual messages in the stream

## Running Integration Tests

The integration tests validate the entire system:

```bash
cd tests/RedisFlow.Integration
dotnet test
```

Tests will:
1. Start a Redis container via Aspire
2. Run various scenarios
3. Validate behavior
4. Clean up resources

### Test Execution Time

- Each test takes 5-15 seconds
- Full test suite: ~1-2 minutes
- Tests run in parallel where possible

### Verbose Test Output

For detailed logs:
```bash
dotnet test --verbosity normal
```

## Troubleshooting

### Issue: Aspire Dashboard Not Loading

**Symptoms:** Browser shows connection refused

**Solutions:**
1. Check if AppHost is still running
2. Verify the URL matches console output
3. Try using `http` instead of `https` if certificate issues
4. Check firewall isn't blocking the port

### Issue: "Docker daemon not running"

**Symptoms:** Error on startup about Docker

**Solutions:**
1. Start Docker Desktop
2. Verify with: `docker version`
3. Wait for Docker to fully initialize

### Issue: Port Conflicts

**Symptoms:** "Port already in use" errors

**Solutions:**
1. Stop other Redis instances
2. Check Aspire isn't already running
3. Kill processes using required ports:
   ```bash
   # Find process using port 6379 (Redis)
   lsof -i :6379
   # or
   netstat -ano | grep 6379
   ```

### Issue: Consumer Not Processing Messages

**Symptoms:** Messages appear in stream but consumer logs show no activity

**Solutions:**
1. Check consumer is running in Aspire Dashboard
2. Verify consumer group exists:
   ```bash
   docker exec -it <redis-container> redis-cli XINFO GROUPS redisflow:stream
   ```
3. Check consumer logs for errors
4. Restart consumer to trigger PEL replay

### Issue: Test Failures

**Symptoms:** Integration tests failing

**Solutions:**
1. Ensure Docker has enough resources (2GB+ RAM)
2. Close other applications using Docker
3. Run tests individually to isolate issues:
   ```bash
   dotnet test --filter "Should_ProduceAndConsumeMessage_When_UsingRedisStream"
   ```
4. Check test logs for specific error messages

## Performance Tuning

### Adjusting Message Rates

Edit producer `Program.cs` files:

```csharp
// Change delay in milliseconds
await Task.Delay(5000, stoppingToken); // 5 seconds
```

### Consumer Processing Time

Edit consumer `Program.cs`:

```csharp
// Simulate processing time
await Task.Delay(500, cancellationToken); // 500ms
```

### Batch Size

Edit `RedisStreamConsumer.cs`:

```csharp
var entries = await db.StreamReadGroupAsync(
    _streamKey,
    _consumerGroup,
    _consumerName,
    ">",
    count: 10); // Change batch size
```

## Stopping the Application

1. In Aspire Dashboard, click "Stop" for all resources
2. Or press `Ctrl+C` in the AppHost terminal
3. Containers are automatically cleaned up
4. To manually remove containers:
   ```bash
   docker ps -a | grep redis
   docker rm -f <container-id>
   ```

## Next Steps

- Modify producer/consumer code and observe behavior
- Add more producers or consumers
- Implement custom message handlers
- Explore Aspire Dashboard features (traces, metrics)
- Try scaling scenarios with multiple consumer instances

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Redis Streams Documentation](https://redis.io/docs/data-types/streams/)
- [Project README](../README.md)
