# Redis Stream Operations Guide

## Stream Retention & Eviction Policies

### Overview
Redis Streams can grow indefinitely unless configured with retention policies. This document outlines recommended practices for managing stream size and data retention.

## Retention Strategies

### 1. MAXLEN - Capped Streams
Limit the stream to a maximum number of entries:

```bash
# Add message with approximate trimming (more efficient)
XADD mystream MAXLEN ~ 1000 * field1 value1

# Trim existing stream
XTRIM mystream MAXLEN ~ 1000
```

**Recommended for:**
- High-throughput scenarios where recent data is most important
- Fixed memory budget requirements
- Streams that don't require long-term history

### 2. MINID - Time-Based Retention
Trim messages older than a specific message ID (timestamp-based):

```bash
# Trim messages older than a specific ID
XTRIM mystream MINID <message-id>
```

**Recommended for:**
- Time-based data retention policies
- Compliance requirements with specific retention periods
- Periodic cleanup jobs

### 3. Manual Cleanup
Implement custom cleanup logic based on business rules:

```csharp
// Example: Delete processed messages older than 7 days
var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds();
var minId = $"{sevenDaysAgo}-0";
await db.StreamTrimAsync("messages", minId);
```

## Consumer Group Management

### Monitoring Pending Entries
Check pending messages in consumer groups:

```bash
# Get pending info for a consumer group
XPENDING mystream mygroup

# Get detailed pending info for specific consumer
XPENDING mystream mygroup - + 10 consumer1
```

### Claiming Stale Messages
Reclaim messages from inactive consumers:

```bash
# Claim messages idle for more than 5 minutes (300000ms)
XAUTOCLAIM mystream mygroup consumer2 300000 0-0 COUNT 10
```

### Consumer Group Cleanup
Remove inactive consumers and consumer groups:

```bash
# Delete a consumer from a group
XGROUP DELCONSUMER mystream mygroup consumer1

# Delete entire consumer group
XGROUP DESTROY mystream mygroup
```

## Monitoring & Diagnostics

### Stream Information
```bash
# Get stream information
XINFO STREAM mystream

# Get consumer group information
XINFO GROUPS mystream

# Get consumer information
XINFO CONSUMERS mystream mygroup
```

### Key Metrics to Monitor
1. **Stream Length**: `XLEN mystream`
2. **Pending Count**: Check via `XPENDING`
3. **Consumer Lag**: Time between message creation and consumption
4. **Memory Usage**: `MEMORY USAGE mystream`

## Production Recommendations

### For Development/Testing
- Use MAXLEN with approximate trimming (~)
- Keep streams small (< 1000 entries)
- Example: `XADD mystream MAXLEN ~ 1000 * ...`

### For Production
1. **Enable Persistence**: Configure RDB or AOF
2. **Set Memory Limits**: Use `maxmemory` directive
3. **Configure Eviction**: Set `maxmemory-policy` (e.g., `noeviction` for streams)
4. **Monitor PEL**: Implement alerts for growing pending lists
5. **Implement Cleanup Jobs**: Scheduled XTRIM operations
6. **Use Consumer Groups**: For parallel processing and fault tolerance

### Sample Redis Configuration
```conf
# Memory management
maxmemory 2gb
maxmemory-policy noeviction

# Persistence
save 900 1
save 300 10
save 60 10000

appendonly yes
appendfsync everysec
```

## Troubleshooting

### Issue: Pending List Growing
**Symptoms**: XPENDING shows increasing count
**Causes**:
- Consumer crashes before acknowledgment
- Processing errors not handled properly
- Consumer not running

**Resolution**:
1. Check consumer logs for errors
2. Restart consumers to process pending messages
3. Use XAUTOCLAIM to reclaim stale messages
4. Implement proper error handling in consumers

### Issue: High Memory Usage
**Symptoms**: Redis memory usage growing
**Causes**:
- No retention policy configured
- High message throughput
- Large message payloads

**Resolution**:
1. Implement MAXLEN or MINID trimming
2. Store large payloads externally (e.g., blob storage)
3. Increase available memory or scale horizontally
4. Archive old streams to cold storage

### Issue: Messages Not Being Consumed
**Symptoms**: Stream length growing, no consumer activity
**Causes**:
- Consumer group not created
- Consumers not running
- Consumer reading wrong position

**Resolution**:
1. Verify consumer group exists: `XINFO GROUPS mystream`
2. Check consumer status: `XINFO CONSUMERS mystream mygroup`
3. Ensure consumers are reading with ">" for new messages
4. Check consumer logs for connection issues

## Integration with .NET

### Configuring Retention in Code
```csharp
// Add message with retention
await database.StreamAddAsync(
    "mystream",
    "field",
    "value",
    "*",
    maxLength: 1000,
    useApproximateMaxLength: true);

// Periodic cleanup job
public async Task TrimOldMessagesAsync()
{
    var retention = TimeSpan.FromDays(7);
    var cutoff = DateTimeOffset.UtcNow.Subtract(retention).ToUnixTimeMilliseconds();
    var minId = $"{cutoff}-0";
    
    await _database.StreamTrimAsync(_streamKey, minId);
}
```

### Monitoring in .NET
```csharp
// Get stream info
var info = await database.ExecuteAsync("XINFO", "STREAM", streamKey);

// Get pending info
var pending = await database.StreamPendingAsync(streamKey, groupName);
Console.WriteLine($"Pending messages: {pending.PendingMessageCount}");
```

## References
- [Redis Streams Documentation](https://redis.io/docs/data-types/streams/)
- [Redis Consumer Groups](https://redis.io/docs/data-types/streams-tutorial/)
- [Redis Persistence](https://redis.io/docs/management/persistence/)
