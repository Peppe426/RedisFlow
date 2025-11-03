#!/bin/bash
# inspect-pending.sh - Inspect pending messages in Redis Stream consumer groups
# Usage: ./inspect-pending.sh [stream-name] [group-name] [redis-host] [redis-port]

STREAM_NAME=${1:-"mystream"}
GROUP_NAME=${2:-"mygroup"}
REDIS_HOST=${3:-"localhost"}
REDIS_PORT=${4:-6379}

echo "======================================"
echo "Redis Pending Messages Inspector"
echo "======================================"
echo "Stream: $STREAM_NAME"
echo "Group: $GROUP_NAME"
echo "Host: $REDIS_HOST:$REDIS_PORT"
echo "======================================"
echo ""

# Check if redis-cli is available
if ! command -v redis-cli &> /dev/null; then
    echo "ERROR: redis-cli not found. Please install Redis CLI tools."
    exit 1
fi

echo "📋 Pending Entries Summary:"
echo "--------------------------------------"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XPENDING "$STREAM_NAME" "$GROUP_NAME"
echo ""

echo "📝 Detailed Pending Entries (Last 10):"
echo "--------------------------------------"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XPENDING "$STREAM_NAME" "$GROUP_NAME" - + 10
echo ""

echo "👤 Consumers in Group:"
echo "--------------------------------------"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XINFO CONSUMERS "$STREAM_NAME" "$GROUP_NAME" 2>/dev/null || echo "No consumer information available"
echo ""

echo "✅ Inspection complete!"
