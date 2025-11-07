# Aspire Setup Guide

This document explains the .NET Aspire setup for RedisFlow and how to use it for local development.

---

## What is .NET Aspire?

.NET Aspire is an opinionated, cloud-ready stack for building observable, production-ready distributed applications. It provides:
- **Orchestration**: Manage dependencies like Redis, databases, and services
- **Service Discovery**: Automatic connection string management
- **Observability**: Built-in OpenTelemetry for logs, traces, and metrics
- **Dashboard**: Real-time monitoring UI for all resources

---

## Architecture

```
┌─────────────────────────────────────────────────┐
│          RedisFlow.AppHost (Orchestrator)       │
│  - Provisions Redis container via Docker        │
│  - Exposes service discovery                    │
│  - Runs Aspire Dashboard                        │
└────────────────┬────────────────────────────────┘
                 │
                 ├─────────────┐
                 │             │
         ┌───────▼──────┐  ┌──▼────────────┐
         │    Redis     │  │   Dashboard   │
         │  (Container) │  │ (localhost:   │
         │ port: dynamic│  │     15888)    │
         └───────┬──────┘  └───────────────┘
                 │
         ┌───────▼──────────────┐
         │  Service Discovery    │
         │  - redis: "localhost: │
         │           {port}"     │
         └───────┬───────────────┘
                 │
     ┌───────────┴───────────┐
     │                       │
┌────▼────────┐      ┌──────▼──────┐
│  Producers  │      │  Consumers  │
│  (Console)  │      │  (Console)  │
└─────────────┘      └─────────────┘
```

---

## Prerequisites

### Required
- **.NET 9 SDK** (9.0 or later)
- **Docker Desktop** (or Docker Engine)

### Installation

1. **Install .NET 9 SDK:**
   ```bash
   # Download from https://dotnet.microsoft.com/download/dotnet/9.0
   dotnet --version  # Should show 9.0.x
   ```

2. **Install Aspire Workload:**
   ```bash
   dotnet workload install aspire
   ```

3. **Verify Docker:**
   ```bash
   docker --version
   docker ps  # Should list running containers
   ```

---

## Running the AppHost

### Method 1: Command Line

```bash
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

**Expected Output:**
```
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 9.5.2
      ...
info: Aspire.Hosting.Dashboard[0]
      Now listening on: http://localhost:15888
```

### Method 2: Visual Studio / Rider

1. Set `RedisFlow.AppHost` as the startup project
2. Press F5 or click Run
3. Dashboard opens automatically in browser

### What Happens

1. **DCP Starts**: Distributed Application Coordinator initializes
2. **Redis Container Pulled**: If not already cached, Docker pulls `redis:latest`
3. **Redis Starts**: Container runs on a dynamic port (e.g., 6379)
4. **Dashboard Launches**: Opens at `http://localhost:15888`
5. **Service Discovery Active**: Connection strings available to other projects

---

## Accessing the Dashboard

Navigate to `http://localhost:15888` to see:

- **Resources**: All running containers and services
- **Console Logs**: Real-time output from each resource
- **Structured Logs**: Filtered, searchable logs with levels
- **Traces**: Distributed tracing across services
- **Metrics**: Performance counters and custom metrics

### Dashboard Features

| Tab | Purpose |
|-----|---------|
| **Resources** | Overview of all resources (Redis, producers, consumers) |
| **Console** | Raw console output from each resource |
| **Logs** | Structured logs with filtering by level, source, etc. |
| **Traces** | OpenTelemetry traces for request flows |
| **Metrics** | Performance metrics and custom counters |

---

## Connecting to Redis from Code

### Automatic Service Discovery

Services that reference `RedisFlow.ServiceDefaults` automatically get:
- Connection string resolution via `IConfiguration`
- Health checks for Redis
- Resilience policies (retries, circuit breakers)

### Example: Producer

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (includes service discovery)
builder.AddServiceDefaults();

// Get Redis connection string by resource name
var connectionString = builder.Configuration.GetConnectionString("redis");

// Create Redis connection
var redis = await ConnectionMultiplexer.ConnectAsync(connectionString!);

var app = builder.Build();
await app.RunAsync();
```

### Manual Connection (if needed)

```csharp
// Connection string format: localhost:{port}
// Port is dynamically assigned by Docker
var config = builder.Configuration.GetConnectionString("redis");
// Example: "localhost:6379"
```

---

## Stopping the AppHost

### Command Line
Press `Ctrl+C` in the terminal where AppHost is running.

### Visual Studio / Rider
Click Stop or press Shift+F5.

### What Happens
1. Aspire sends shutdown signal to all resources
2. Redis container stops (but is NOT removed due to `Persistent` lifetime)
3. DCP shuts down
4. Dashboard becomes unavailable

**Note:** Redis container persists data between runs. To reset:
```bash
docker ps -a | grep redis  # Find container ID
docker rm -f <container-id>
```

---

## AppHost Configuration

### Current Setup

```csharp
// src/RedisFlow/RedisFlow.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

builder.Build().Run();
```

### Configuration Options

| Method | Description |
|--------|-------------|
| `.WithLifetime(Persistent)` | Container persists between runs (default) |
| `.WithLifetime(Session)` | Container removed when AppHost stops |
| `.WithDataVolume()` | Mount persistent volume for data |
| `.WithImageTag("7-alpine")` | Use specific Redis version |

### Adding Environment Variables

```csharp
var redis = builder.AddRedis("redis")
    .WithEnvironment("REDIS_MAXMEMORY", "256mb")
    .WithLifetime(ContainerLifetime.Persistent);
```

---

## Integration Tests

Integration tests use `Aspire.Hosting.Testing` to programmatically start the AppHost.

### Running Tests Locally

```bash
cd tests/RedisFlow.Integration
dotnet test
```

**Requirements:**
- Docker must be running
- Aspire workload must be installed
- Tests will start AppHost automatically

### Test Structure

```csharp
[OneTimeSetUp]
public async Task OneTimeSetUp()
{
    // Start Aspire host
    var appHost = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.RedisFlow_AppHost>();
    _app = await appHost.BuildAsync();
    await _app.StartAsync();
    
    // Get Redis connection
    var connectionString = await _app.GetConnectionStringAsync("redis");
    _redis = await ConnectionMultiplexer.ConnectAsync(connectionString!);
}
```

---

## Troubleshooting

### "Docker is not running"

**Error:**
```
Aspire.Hosting.DistributedApplicationException: Docker is not running
```

**Solution:**
1. Start Docker Desktop
2. Verify: `docker ps`
3. Restart AppHost

### "Port already in use"

**Error:**
```
Failed to bind to address http://localhost:15888: address already in use
```

**Solution:**
1. Check for existing AppHost: `ps aux | grep RedisFlow.AppHost`
2. Kill process or use different port:
   ```bash
   dotnet run --launch-profile=http --urls="http://localhost:15889"
   ```

### "Aspire workload not found"

**Error:**
```
error : The Aspire.AppHost.Sdk Sdk is not found
```

**Solution:**
```bash
dotnet workload install aspire
```

### Tests Timeout in CI/CD

**Expected:** Integration tests require Docker and DCP, which may not be available in CI/CD.

**Solution:**
- Use Docker service containers in CI/CD instead of Aspire
- Or run tests only on self-hosted runners with Docker
- See `tests/RedisFlow.Integration/README.md` for details

---

## Redis Configuration

### Default Settings

- **Image:** `redis:latest` (pulled from Docker Hub)
- **Port:** Dynamically assigned by Docker (typically 6379)
- **Persistence:** Container data persists between AppHost runs
- **Config:** Default Redis configuration (no password, no TLS)

### Production Considerations

For production, consider:
1. **Authentication:** Add password protection
2. **TLS:** Enable encrypted connections
3. **Persistence:** Configure RDB or AOF persistence
4. **Clustering:** Set up Redis Cluster or Sentinel
5. **Resource Limits:** Set memory limits

**Note:** This setup is for local development only. Production deployments should use managed Redis services (Azure Cache for Redis, AWS ElastiCache, etc.) or properly secured on-premise installations.

---

## Next Steps

1. **Start AppHost:** `dotnet run --project src/RedisFlow/RedisFlow.AppHost`
2. **Open Dashboard:** Navigate to `http://localhost:15888`
3. **Implement Producers:** See `docs/ProjectStructure.md` for guidelines
4. **Implement Consumers:** Follow resilience patterns in documentation
5. **Run Integration Tests:** `dotnet test tests/RedisFlow.Integration/`

---

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Aspire Redis Hosting](https://learn.microsoft.com/en-us/dotnet/aspire/caching/stackexchange-redis-component)
- [Service Discovery](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)
- [Testing with Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/testing)
