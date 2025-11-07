# RedisFlow Integration Tests

## Overview

This project contains integration tests that demonstrate the Redis stream developer tooling capabilities. These tests verify that:
- Redis streams can be created and inspected
- Consumer groups function correctly
- Pending messages can be tracked
- Developer diagnostics tools work as expected

## Prerequisites

- .NET 9 SDK
- Docker Desktop running
- Aspire DCP (Developer Control Plane) - included with .NET Aspire workload

### Install .NET Aspire Workload

```bash
dotnet workload install aspire
```

## Running the Tests

### Option 1: Run all integration tests

```bash
cd src/RedisFlow/RedisFlow.Integration.Tests
dotnet test
```

### Option 2: Run specific test

```bash
dotnet test --filter "FullyQualifiedName~Should_CreateAndInspectStream_When_MessagesProduced"
```

### Option 3: Run from solution

```bash
cd src/RedisFlow
dotnet test RedisFlow.Integration.Tests/RedisFlow.Integration.Tests.csproj
```

## Test Categories

All tests are marked with:
- `[Category("Integration")]` - Indicates they are integration tests
- `[Explicit]` - Must be run explicitly, not part of automated CI/CD

## What The Tests Demonstrate

### 1. Stream Creation and Inspection
- Creating streams with messages
- Retrieving stream length
- Viewing stream entries with XRANGE

### 2. Consumer Groups
- Creating consumer groups
- Listing consumer groups
- Viewing consumers in a group

### 3. Pending Messages
- Tracking pending (unacknowledged) messages
- Using XPENDING command
- Consumer restart scenarios

### 4. Real-time Monitoring
- Stream growth monitoring
- Latest entry retrieval with XREVRANGE
- Continuous production scenarios

## Troubleshooting

### Error: "Aspire DCP not found"

Ensure the Aspire workload is installed:
```bash
dotnet workload list
# Should show 'aspire' in the list

# If not installed:
dotnet workload install aspire
```

### Error: "Docker connection failed"

Ensure Docker Desktop is running:
- Windows/Mac: Check Docker Desktop is running in system tray
- Linux: `sudo systemctl status docker`

### Error: "Port already in use"

The tests may conflict with existing Redis instances. Stop other Redis containers:
```bash
docker ps | grep redis
docker stop <container-id>
```

## Manual Testing Alternative

If you prefer to test manually without running integration tests:

1. Start the Aspire AppHost:
   ```bash
   cd src/RedisFlow/RedisFlow.AppHost
   dotnet run
   ```

2. In another terminal, use the diagnostics tool:
   ```bash
   cd src/RedisFlow/RedisFlow.Diagnostics
   dotnet run localhost:6379 mystream
   ```

3. Or use the shell scripts:
   ```bash
   ./scripts/inspect-stream.sh mystream localhost 6379
   ```

## Integration with CI/CD

These tests are marked `[Explicit]` and will **not** run automatically in CI/CD pipelines. To include them in CI/CD:

1. Ensure your CI environment has:
   - Docker available
   - .NET Aspire workload installed
   - Sufficient permissions to run containers

2. Explicitly run them in your pipeline:
   ```yaml
   - name: Run Integration Tests
     run: dotnet test --filter "Category=Integration" --no-build
   ```

## See Also

- [Developer Tooling Documentation](../../../docs/Developer-Tooling.md)
- [Test Structure Guidelines](../../../docs/Test%20structure.md)
