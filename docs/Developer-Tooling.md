# Developer Tooling for Redis Streams

This document describes the developer tooling available to monitor and inspect Redis stream traffic during development and testing.

## 🎯 Overview

RedisFlow provides multiple tools for observing Redis stream activity:

1. **Shell Scripts** - Quick command-line inspection using Redis CLI
2. **Diagnostics Console App** - Interactive tool with rich UI
3. **Redis Commander** - Web-based Redis browser (via Aspire)
4. **Redis CLI Commands** - Direct Redis commands for advanced use

## 🚀 Getting Started

### Prerequisites

- .NET 9 SDK
- Docker (for running Redis via Aspire)
- Redis CLI tools (optional, for shell scripts)

### Running the Aspire AppHost

The Aspire AppHost automatically provisions a Redis container with Redis Commander for easy inspection:

```bash
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

This will:
- Start a Redis container on port 6379
- Start Redis Commander web UI (accessible via Aspire dashboard)
- Display the Aspire dashboard URL

## 🛠️ Tooling Options

### 1. RedisFlow Diagnostics Tool (Recommended)

An interactive console application with a rich terminal UI for inspecting streams.

#### Usage

```bash
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run [connection-string] [stream-name]

# Examples:
dotnet run                              # Uses localhost:6379 and "mystream"
dotnet run localhost:6379 orders       # Custom stream name
dotnet run redis-server:6379 events    # Custom host
```

#### Features

- **📊 Stream Information** - View stream metadata and statistics
- **📝 View Stream Entries** - Browse recent messages with formatted output
- **👥 Consumer Groups** - List all consumer groups and their status
- **📋 Pending Messages** - Inspect pending messages per consumer group
- **🔄 Real-time Monitor** - Live dashboard showing stream activity
- **🔍 List All Streams** - Discover all streams in Redis

### 2. Shell Scripts

Quick inspection scripts located in `scripts/` directory.

#### inspect-stream.sh

Inspect stream information, entries, and consumer groups:

```bash
./scripts/inspect-stream.sh [stream-name] [redis-host] [redis-port]

# Examples:
./scripts/inspect-stream.sh mystream
./scripts/inspect-stream.sh orders localhost 6379
```

Output includes:
- Stream information (length, first/last IDs)
- Last 10 entries
- Consumer groups summary

#### inspect-pending.sh

Inspect pending messages for a consumer group:

```bash
./scripts/inspect-pending.sh [stream-name] [group-name] [redis-host] [redis-port]

# Examples:
./scripts/inspect-pending.sh mystream mygroup
./scripts/inspect-pending.sh orders order-processors localhost 6379
```

Output includes:
- Pending entries summary
- Detailed pending entries list
- Consumer information

#### monitor-stream.sh

Real-time monitoring of stream activity:

```bash
./scripts/monitor-stream.sh [stream-name] [redis-host] [redis-port]

# Examples:
./scripts/monitor-stream.sh mystream
./scripts/monitor-stream.sh events localhost 6379
```

Press `Ctrl+C` to stop monitoring.

### 3. Redis Commander (Web UI)

Redis Commander is automatically included when running the Aspire AppHost:

1. Start the AppHost: `cd src/RedisFlow/RedisFlow.AppHost && dotnet run`
2. Open the Aspire dashboard (URL shown in console)
3. Click on the Redis Commander endpoint
4. Browse streams, keys, and data through the web interface

### 4. Direct Redis CLI Commands

For advanced scenarios, use Redis CLI directly:

#### View Stream Info

```bash
redis-cli XINFO STREAM mystream
```

#### View Entries

```bash
# Get last 10 entries
redis-cli XREVRANGE mystream + - COUNT 10

# Get all entries
redis-cli XRANGE mystream - +

# Get entries from specific ID
redis-cli XRANGE mystream 1234567890-0 +
```

#### View Consumer Groups

```bash
# List groups
redis-cli XINFO GROUPS mystream

# View consumers in a group
redis-cli XINFO CONSUMERS mystream mygroup

# View pending messages
redis-cli XPENDING mystream mygroup

# Detailed pending messages
redis-cli XPENDING mystream mygroup - + 10
```

#### Monitor Live Activity

```bash
# Monitor all Redis commands
redis-cli MONITOR

# Monitor specific stream
redis-cli --scan --pattern "mystream*"
```

## 📊 Common Inspection Scenarios

### Scenario 1: Check if Messages are Being Produced

```bash
# Quick check with script
./scripts/inspect-stream.sh mystream

# Or with diagnostics tool
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "📊 Stream Information"
```

Look for:
- Increasing stream length
- Recent timestamps in entries

### Scenario 2: Debug Consumer Issues

```bash
# Check pending messages
./scripts/inspect-pending.sh mystream mygroup

# Or use diagnostics tool
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "📋 Pending Messages"
```

Look for:
- High pending count (consumers may be stuck)
- Old pending entries (messages not being processed)
- Consumer count (are consumers active?)

### Scenario 3: Monitor Real-time Activity

```bash
# Terminal-based monitoring
./scripts/monitor-stream.sh mystream

# Or interactive diagnostics tool
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "🔄 Real-time Monitor"
```

### Scenario 4: Investigate Message Content

```bash
# Use diagnostics tool for best formatting
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "📝 View Stream Entries"
```

### Scenario 5: Check Consumer Group Status

```bash
# Quick overview
./scripts/inspect-stream.sh mystream

# Detailed view with diagnostics tool
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "👥 Consumer Groups"
```

## 🔧 Troubleshooting

### Script Error: "redis-cli not found"

Install Redis CLI tools:

**Ubuntu/Debian:**
```bash
sudo apt-get install redis-tools
```

**macOS:**
```bash
brew install redis
```

**Windows:**
Download from https://github.com/microsoftarchive/redis/releases or use WSL.

### Connection Refused

Ensure Redis is running:
```bash
# If using Aspire
cd src/RedisFlow/RedisFlow.AppHost
dotnet run

# Or check Redis directly
redis-cli ping  # Should return "PONG"
```

### No Streams Found

Verify:
1. Producers are running and configured with correct stream names
2. Connection string is correct
3. Redis database number is correct (default is 0)

### Pending Messages Not Clearing

Check:
1. Consumers are running and properly configured
2. Consumer group exists: `redis-cli XINFO GROUPS mystream`
3. Messages are being acknowledged in consumer code
4. No exceptions in consumer logs

## 🔗 Integration with Aspire

The AppHost in `src/RedisFlow/RedisFlow.AppHost/AppHost.cs` configures:

```csharp
var redis = builder.AddRedis("redis")
    .WithRedisCommander(); // Adds Redis Commander web UI
```

This provides:
- Containerized Redis instance
- Redis Commander for visual inspection
- Automatic health checks
- Service discovery for producers/consumers

## 📝 Best Practices

1. **Use Diagnostics Tool for Development** - Best UX for interactive exploration
2. **Use Scripts for CI/CD** - Automated checks in pipelines
3. **Monitor Pending Messages** - High pending counts indicate issues
4. **Check Consumer Lag** - Difference between last entry and last delivered ID
5. **Verify Message Acknowledgment** - Ensure consumers call XACK
6. **Use Consumer Groups** - For parallel processing and fault tolerance
7. **Set Stream Retention** - Prevent unbounded growth with MAXLEN or MINID
8. **Log Stream IDs** - Essential for debugging and replay scenarios

## 📚 Additional Resources

- [Redis Streams Documentation](https://redis.io/docs/data-types/streams/)
- [Redis Streams Tutorial](https://redis.io/docs/data-types/streams-tutorial/)
- [StackExchange.Redis Streams](https://stackexchange.github.io/StackExchange.Redis/Streams.html)
- [Aspire Redis Component](https://learn.microsoft.com/en-us/dotnet/aspire/caching/stackexchange-redis-component)

## 🚨 Integration Testing

When writing integration tests, use the diagnostics tools to verify:
- Messages are produced with correct structure
- Consumer groups are created
- Pending messages are processed
- Stream IDs are tracked correctly

Example test helper:

```csharp
public async Task<long> GetStreamLengthAsync(string streamName)
{
    var db = _redis.GetDatabase();
    return await db.StreamLengthAsync(streamName);
}

public async Task<bool> HasPendingMessagesAsync(string streamName, string groupName)
{
    var db = _redis.GetDatabase();
    var pending = await db.ExecuteAsync("XPENDING", streamName, groupName);
    var pendingInfo = (RedisResult[])pending!;
    return long.Parse(pendingInfo[0].ToString()!) > 0;
}
```

---

**Last Updated:** 2025-11-03
**Version:** 1.0.0
