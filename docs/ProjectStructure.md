# Project Structure & Conventions

This document outlines the organizational structure and development conventions for the RedisFlow project.

---

## 📁 Directory Structure

### Source Projects (`src/RedisFlow/`)

#### **RedisFlow.AppHost**
- **Purpose:** .NET Aspire orchestration host
- **Responsibilities:**
  - Provision Redis container for local development
  - Configure service discovery and connection strings
  - Expose telemetry and monitoring endpoints
- **Key Files:**
  - `AppHost.cs` - Main entry point with resource definitions
  - `appsettings.json` - Aspire configuration

#### **RedisFlow.Domain**
- **Purpose:** Shared domain models and contracts
- **Responsibilities:**
  - Define message schemas (Protocol Buffers)
  - Contain domain entities and value objects
  - No dependencies on infrastructure or services
- **Guidelines:**
  - Keep models immutable (readonly/init properties)
  - Use `record` types where appropriate
  - No business logic in domain models

#### **RedisFlow.Services**
- **Purpose:** Business logic and service implementations
- **Responsibilities:**
  - Producer service implementations
  - Consumer service implementations
  - Stream management logic
- **Guidelines:**
  - Depend on `RedisFlow.Domain` for contracts
  - Use dependency injection
  - Follow SOLID principles

#### **RedisFlow.ServiceDefaults**
- **Purpose:** Common Aspire service configuration
- **Responsibilities:**
  - OpenTelemetry setup (tracing, metrics, logging)
  - Health checks configuration
  - Service discovery defaults
  - Resilience policies
- **Note:** Referenced by all service projects

#### **Tests.Producer** & **Tests.Consumers**
- **Purpose:** Console apps for manual testing and demonstration
- **Type:** Test harness / sample implementations
- **Responsibilities:**
  - Demonstrate producer/consumer patterns
  - Provide interactive testing capabilities
  - Serve as reference implementations
- **Note:** These are NOT unit tests - see `tests/` directory for integration tests

---

### Test Projects (`tests/`)

#### **RedisFlow.Integration**
- **Purpose:** Integration tests using Aspire.Hosting.Testing
- **Responsibilities:**
  - Verify produce/consume/replay flows
  - Test consumer group mechanics
  - Validate pending message handling
  - Ensure Redis stream operations work end-to-end
- **Guidelines:**
  - Use NUnit + FluentAssertions
  - Follow Given/When/Then structure (see `docs/Test structure.md`)
  - Launch Aspire Redis instance automatically
  - Clean up resources after each test

---

### Documentation (`docs/`)

#### **schemas/**
- **Purpose:** Protocol Buffer schema definitions
- **Files:**
  - `*.proto` - Message schema files
  - `CHANGELOG.md` - Schema versioning history
- **Guidelines:**
  - Never reuse tag numbers
  - Use `snake_case` for field names
  - Document breaking changes in CHANGELOG

#### **Test structure.md**
- Test writing conventions and examples

---

## 🔌 Aspire Wiring Requirements

### Redis Container Orchestration

The `RedisFlow.AppHost` project **must**:
1. Use `Aspire.Hosting.Redis` package
2. Define Redis resource with a stable name (`redis`)
3. Configure persistent container lifetime for local development
4. Expose connection information via service discovery

Example:
```csharp
var builder = DistributedApplication.CreateBuilder(args);
var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);
builder.Build().Run();
```

### Docker Compose Alternatives

Docker Compose files for Redis are **gated behind feature flags** and should only be used when:
- Aspire is not available in the environment
- Specific Redis Enterprise features are required
- Running in CI/CD pipelines without Aspire support

**Default:** Always use Aspire AppHost for local development.

---

## 🛡️ Resilience Expectations

### Consumer Implementations

All consumer implementations **MUST**:

1. **Support Pending Message Replay**
   - Use `XPENDING` to check for unacknowledged messages on startup
   - Implement `XCLAIM` logic to reclaim messages from failed consumers
   - Reprocess messages from the PEL before consuming new messages

2. **Handle Producer-Offline Scenarios**
   - Continue consuming existing messages even when producers are unavailable
   - Don't fail or exit if stream is empty
   - Implement appropriate polling/blocking strategies

3. **Include Diagnostics for Stream IDs**
   - Log stream IDs for every message consumed
   - Track correlation between produce and consume events
   - Include stream IDs in error messages and telemetry

Example logging:
```csharp
_logger.LogInformation(
    "Consumed message {MessageId} from stream {StreamKey} at {Timestamp}",
    messageId, streamKey, timestamp);
```

### Producer Implementations

All producer implementations **SHOULD**:

1. **Handle Redis Connection Failures**
   - Implement retry logic with exponential backoff
   - Don't lose messages due to transient failures
   - Log connection state changes

2. **Log Stream IDs**
   - Capture and log the stream ID returned by `XADD`
   - Include stream IDs in telemetry for end-to-end tracing
   - Use stream IDs for debugging message flow

Example:
```csharp
var streamId = await db.StreamAddAsync(streamKey, messageData);
_logger.LogInformation(
    "Produced message {MessageId} to stream {StreamKey}",
    streamId, streamKey);
```

3. **Respect Rate Limits**
   - Implement throttling if required
   - Don't overwhelm Redis with unbounded writes
   - Monitor stream length and apply backpressure

---

## 🧪 Testing Mandate

### Integration Tests Requirements

Integration tests **MUST**:

1. **Launch Aspire Redis Instance**
   - Use `Aspire.Hosting.Testing` package
   - Start full Aspire host with Redis resource
   - No mocking of Redis operations

2. **Verify Complete Flows**
   - Produce → Consume → Acknowledge
   - Pending message replay after simulated failure
   - Consumer group mechanics (multiple consumers)

3. **Be Runnable via `dotnet test`**
   ```bash
   dotnet test tests/RedisFlow.Integration/
   ```

4. **Clean Up Resources**
   - Use `[OneTimeTearDown]` or equivalent
   - Dispose connections and Aspire host
   - Don't leave orphaned containers

### Example Test Structure
```csharp
[TestFixture]
public class RedisStreamIntegrationTests
{
    private DistributedApplication? _app;
    private IConnectionMultiplexer? _redis;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.RedisFlow_AppHost>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();
        
        var connectionString = await _app.GetConnectionStringAsync("redis");
        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString!);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _redis?.Dispose();
        if (_app != null) await _app.DisposeAsync();
    }

    [Test]
    public async Task Should_ProduceAndConsume_When_UsingConsumerGroup()
    {
        // Given / When / Then...
    }
}
```

---

## 🔒 Security & Best Practices

1. **No Secrets in Code**
   - Use User Secrets for local development
   - Use Azure Key Vault or similar for production
   - Never commit connection strings or credentials

2. **Immutable Configuration**
   - Prefer configuration over code for environment-specific settings
   - Use `appsettings.{Environment}.json` pattern

3. **Dependency Injection**
   - Register all services via DI
   - Avoid `new` keyword for services
   - Use constructor injection

4. **OpenTelemetry**
   - All services should inherit from `ServiceDefaults`
   - Emit structured logs with correlation IDs
   - Track custom metrics for stream operations

---

## 🚦 Development Workflow

1. **Start Aspire Host:** `dotnet run --project src/RedisFlow/RedisFlow.AppHost`
2. **Run Producers/Consumers:** Start from IDE or separate terminal
3. **Monitor via Dashboard:** Open Aspire dashboard URL
4. **Run Integration Tests:** `dotnet test tests/RedisFlow.Integration/`
5. **Check Logs/Traces:** Use dashboard or Application Insights

---

## 📦 NuGet Package Guidelines

### Allowed Packages
- `Aspire.*` - Aspire hosting and testing
- `StackExchange.Redis` - Redis client
- `Google.Protobuf` / `Grpc.Tools` - Protobuf serialization
- `Microsoft.Extensions.*` - DI, logging, configuration
- `OpenTelemetry.*` - Observability
- `NUnit` / `FluentAssertions` - Testing

### Prohibited Packages
- `Newtonsoft.Json` - Use System.Text.Json for admin tools only
- `MessagePack` - Use Protocol Buffers instead
- Any JSON serializers for stream payloads

---

## 📚 Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Redis Streams Documentation](https://redis.io/docs/data-types/streams/)
- [Protocol Buffers Guide](https://protobuf.dev/)
- [StackExchange.Redis Guide](https://stackexchange.github.io/StackExchange.Redis/)
