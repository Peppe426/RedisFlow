# Quick Start: Redis Stream Developer Tooling

This guide provides a quick walkthrough of the Redis stream developer tooling to help you get started monitoring your streams in 5 minutes.

## Prerequisites

```bash
# Ensure you have .NET 9 SDK installed
dotnet --version  # Should show 9.x

# Optional: Install redis-cli for shell scripts
# Ubuntu/Debian: sudo apt-get install redis-tools
# macOS: brew install redis
# Windows: Use WSL or download from GitHub
```

## Quick Start Steps

### 1. Start Redis with Aspire

```bash
cd src/RedisFlow/RedisFlow.AppHost
dotnet run
```

This will:
- Start Redis container on port 6379
- Start Redis Commander web UI
- Display the Aspire dashboard URL

**Output will look like:**
```
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 9.5.2
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application starting.
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: https://localhost:17238
```

### 2. Produce Some Test Data

Open a new terminal and use Redis CLI to add some test messages:

```bash
# Connect to Redis
redis-cli

# Add some messages to a stream
XADD mystream * producer test-app message "Hello World" timestamp $(date +%s)
XADD mystream * producer test-app message "Message 2" timestamp $(date +%s)
XADD mystream * producer test-app message "Message 3" timestamp $(date +%s)

# Create a consumer group
XGROUP CREATE mystream mygroup 0 MKSTREAM

# Exit redis-cli
exit
```

### 3. Use the Diagnostics Tool

In another terminal:

```bash
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
```

You'll see an interactive menu:

```
 ____          _ _     _____ _               ____  _                             _   _          
|  _ \ ___  __| (_)___  |  ___| | _____      |  _ \(_) __ _  __ _ _ __   ___  ___| |_(_) ___ ___ 
| |_) / _ \/ _` | / __| | |_  | |/ _ \ \ /\ / / | | | |/ _` |/ _` | '_ \ / _ \/ __| __| |/ __/ __|
|  _ <  __/ (_| | \__ \ |  _| | | (_) \ V  V /  |_| | | (_| | (_| | | | | (_) \__ \ |_| | (__\__ \
|_| \_\___|\__,_|_|___/ |_|   |_|\___/ \_/\_/  |____/|_|\__,_|\__, |_| |_|\___/|___/\__|_|\___|___/
                                                               |___/                                

Connection: localhost:6379
Stream: mystream

? What would you like to inspect?
  📊 Stream Information
  📝 View Stream Entries
  👥 Consumer Groups
  📋 Pending Messages
  🔄 Real-time Monitor
  🔍 List All Streams
❯ ❌ Exit
```

**Try these options:**
1. **📊 Stream Information** - See stream metadata
2. **📝 View Stream Entries** - Browse messages with pretty formatting
3. **👥 Consumer Groups** - View all consumer groups
4. **🔄 Real-time Monitor** - Live dashboard updates every 2 seconds

### 4. Use Shell Scripts (Alternative)

```bash
# Quick inspection
./scripts/inspect-stream.sh mystream

# Sample output:
# ======================================
# Redis Stream Inspector
# ======================================
# Stream: mystream
# Host: localhost:6379
# ======================================
#
# 📊 Stream Information:
# --------------------------------------
# length
# 3
# ...

# Monitor in real-time
./scripts/monitor-stream.sh mystream

# Inspect pending messages (if consumer group exists)
./scripts/inspect-pending.sh mystream mygroup
```

### 5. Use Redis Commander Web UI

1. Go to the Aspire dashboard URL (from step 1)
2. Click on the Redis Commander resource
3. Browse streams visually through the web interface

## Common Scenarios

### Scenario 1: Check if Producer is Working

```bash
# Quick check with script
./scripts/inspect-stream.sh mystream

# Look for increasing "Stream Length" value
```

### Scenario 2: Debug Consumer Not Processing

```bash
# Check pending messages
./scripts/inspect-pending.sh mystream mygroup

# Or use diagnostics tool:
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "📋 Pending Messages"
```

**What to look for:**
- High pending count = Consumer might be stuck
- Old pending entries = Messages not being acknowledged
- Check consumer count = Are consumers running?

### Scenario 3: Monitor Production Rate

```bash
# Use real-time monitor
./scripts/monitor-stream.sh mystream

# Or use diagnostics tool:
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "🔄 Real-time Monitor"
```

### Scenario 4: Investigate Message Content

```bash
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run localhost:6379 mystream
# Select: "📝 View Stream Entries"
# Enter number of entries to view
```

## Tips and Tricks

### 1. Custom Redis Connection

```bash
# Different host
cd src/RedisFlow/RedisFlow.Diagnostics
dotnet run redis.example.com:6380 events

# With scripts
./scripts/inspect-stream.sh events redis.example.com 6380
```

### 2. Multiple Streams

Use the "🔍 List All Streams" option in the diagnostics tool to discover all streams, then restart the tool with the specific stream name.

### 3. Consumer Restart Testing

```bash
# Create pending message
redis-cli XGROUP CREATE mystream mygroup 0 MKSTREAM
redis-cli XREADGROUP GROUP mygroup consumer1 COUNT 1 STREAMS mystream >

# DON'T acknowledge (simulate crash)
# Then check pending:
./scripts/inspect-pending.sh mystream mygroup
```

### 4. Stream Cleanup

```bash
# Delete a stream
redis-cli DEL mystream

# Trim old entries (keep last 1000)
redis-cli XTRIM mystream MAXLEN 1000
```

## Troubleshooting

### "Connection refused"
- Ensure Aspire AppHost is running: `cd src/RedisFlow/RedisFlow.AppHost && dotnet run`
- Check Redis port: `netstat -an | grep 6379`

### "redis-cli not found"
- Scripts require Redis CLI tools
- Alternative: Use the diagnostics tool instead

### "Stream not found"
- Stream doesn't exist yet
- Produce some messages first or use the diagnostics tool's "🔍 List All Streams" option

## Next Steps

- Read the full [Developer Tooling Documentation](Developer-Tooling.md)
- Run the [Integration Tests](../src/RedisFlow/RedisFlow.Integration.Tests/README.md)
- Implement your own producers and consumers
- Set up monitoring in your CI/CD pipeline

## Need Help?

Refer to:
- [Developer Tooling Documentation](Developer-Tooling.md) - Comprehensive guide
- [Integration Tests README](../src/RedisFlow/RedisFlow.Integration.Tests/README.md) - Test setup
- [Redis Streams Documentation](https://redis.io/docs/data-types/streams/) - Official Redis docs

---

**Happy Debugging! 🔍🚀**
