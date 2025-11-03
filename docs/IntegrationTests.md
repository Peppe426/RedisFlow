# Running Integration Tests

This document describes how to run the Redis stream integration tests locally.

## Prerequisites

- **.NET 9 SDK** installed
- **Docker** installed and running (required for Aspire to spin up Redis container)
- **Aspire Workload** installed: `dotnet workload install aspire`

## Integration Test Project

The integration tests are located in `tests/RedisFlow.Integration/` and cover:

- Producer emitting messages to Redis streams
- Consumer processing messages from streams
- MessagePack serialization/deserialization fidelity
- Message acknowledgment handling
- Pending message replay (consumer restart scenarios)
- Producer offline scenarios
- Multiple producer coordination

## Running Tests Locally

### Option 1: Using dotnet CLI

From the repository root:

```bash
dotnet test tests/RedisFlow.Integration/RedisFlow.Integration.csproj
```

From the solution directory:

```bash
cd src/RedisFlow
dotnet test ../../tests/RedisFlow.Integration/RedisFlow.Integration.csproj
```

### Option 2: Using dotnet test with filters

Run only integration tests:

```bash
dotnet test tests/RedisFlow.Integration/ --filter "Category=Integration test"
```

Run a specific test:

```bash
dotnet test tests/RedisFlow.Integration/ --filter "FullyQualifiedName~Should_ProduceMessageToStream_When_ProducerEmitsMessage"
```

### Option 3: From Visual Studio / Rider

1. Open the solution `src/RedisFlow/RedisFlow.slnx`
2. Build the solution
3. Open Test Explorer
4. Run tests in the `RedisFlow.Integration` project

## How It Works

### Aspire-based Redis Setup

The integration tests use **Aspire.Hosting.Testing** to automatically:

1. Spin up a Redis container via Docker
2. Configure connection strings
3. Provide a `IConnectionMultiplexer` for test use
4. Clean up resources after tests complete

The Redis instance is shared across all tests in a test class but the stream is cleaned up after each test to ensure isolation.

### Test Structure

All integration tests inherit from `RedisIntegrationTestBase` which:

- Sets up Redis via Aspire AppHost (`RedisFlow.AppHost`)
- Provides a configured `IConnectionMultiplexer` instance
- Cleans up the test stream after each test
- Disposes resources after all tests complete

### MessagePack Serialization

Messages are serialized using **MessagePack** (not JSON) for:

- High performance
- Low overhead
- Binary format suitable for Redis streams

The `Message` class in `RedisFlow.Domain.ValueObjects` is annotated with `[MessagePackObject]` and uses `[Key(n)]` attributes for proper serialization.

## Troubleshooting

### Docker Not Running

**Error**: `Docker is not running` or connection failures

**Solution**: Ensure Docker Desktop is running before executing tests.

### Port Conflicts

**Error**: Redis port already in use

**Solution**: Stop any locally running Redis instances or change the port in the AppHost configuration.

### Aspire Workload Missing

**Error**: `Aspire.Hosting` types not found

**Solution**: Install the Aspire workload:
```bash
dotnet workload install aspire
```

### Test Timeouts

**Error**: Tests timing out waiting for messages

**Solution**: 
- Ensure Docker has adequate resources allocated
- Check Docker logs: `docker logs <container-id>`
- Increase timeout values in tests if running on slower hardware

## CI/CD Integration

To run integration tests in CI:

1. Ensure Docker is available in the CI environment
2. Install Aspire workload: `dotnet workload install aspire`
3. Run tests: `dotnet test tests/RedisFlow.Integration/`

Example GitHub Actions workflow:

```yaml
- name: Setup Aspire
  run: dotnet workload install aspire

- name: Run Integration Tests
  run: dotnet test tests/RedisFlow.Integration/ --logger "trx;LogFileName=test-results.trx"
```

## Test Coverage

The integration test suite validates:

| Test | Description |
|------|-------------|
| **Should_ProduceMessageToStream_When_ProducerEmitsMessage** | Verifies producer can emit messages to stream |
| **Should_ConsumeMessage_When_ProducerEmitsMessage** | Validates end-to-end produce → consume flow |
| **Should_SerializeAndDeserializeCorrectly_When_UsingMessagePack** | Ensures MessagePack serialization fidelity |
| **Should_AcknowledgeMessage_When_ConsumerProcessesSuccessfully** | Confirms messages are properly acknowledged |
| **Should_ReplayPendingMessages_When_ConsumerRestarts** | Tests pending message replay after consumer restart |
| **Should_ContinueConsuming_When_ProducerGoesOffline** | Validates resilience when producer disconnects |
| **Should_HandleMultipleProducers_When_EmittingConcurrently** | Tests concurrent producer coordination |

## Architecture Notes

### Producer Implementation

- Located in `RedisFlow.Services.RedisProducer`
- Uses `StackExchange.Redis` to write to Redis streams
- Serializes messages with MessagePack before writing
- Implements `IProducer` interface from `RedisFlow.Services.Contracts`

### Consumer Implementation

- Located in `RedisFlow.Services.RedisConsumer`
- Implements consumer groups for parallel processing
- Handles pending entry list (PEL) management
- Implements claim/retry logic with `XCLAIM` and `XPENDING`
- Acknowledges messages after successful processing
- Implements `IConsumer` interface from `RedisFlow.Services.Contracts`

### Stream Workflow

1. **Producer** serializes `Message` to MessagePack bytes
2. **Producer** writes bytes to Redis stream via `XADD`
3. **Consumer** reads from stream using consumer group via `XREADGROUP`
4. **Consumer** deserializes MessagePack bytes to `Message`
5. **Consumer** invokes handler with message
6. **Consumer** acknowledges message via `XACK`

### Resilience Features

- **Pending replay**: On startup, consumers check for pending messages and replay them
- **Consumer groups**: Multiple consumers can process messages in parallel
- **Message acknowledgment**: Ensures at-least-once delivery
- **Stream IDs**: Redis generates unique, monotonically increasing IDs for diagnostics

## Feature Flags

The integration tests are designed to run with **Aspire** by default. Alternative orchestration (e.g., Docker Compose) could be gated behind feature flags in the future.

For Docker Compose integration:

1. Add `docker-compose.yml` to repository root
2. Implement conditional setup in test base class
3. Use environment variable to toggle: `REDISFLOW_USE_DOCKER_COMPOSE=true`

## Future Enhancements

- [ ] Add stress tests for high-throughput scenarios
- [ ] Implement chaos engineering tests (network failures, container restarts)
- [ ] Add benchmarks for MessagePack vs JSON serialization
- [ ] Test Redis Cluster configurations
- [ ] Add metrics and observability validation
