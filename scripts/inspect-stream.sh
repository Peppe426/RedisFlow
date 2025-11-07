#!/bin/bash
# inspect-stream.sh - Inspect Redis Stream entries and metadata
# Usage: ./inspect-stream.sh [stream-name] [redis-host] [redis-port]

STREAM_NAME=${1:-"mystream"}
REDIS_HOST=${2:-"localhost"}
REDIS_PORT=${3:-6379}

echo "======================================"
echo "Redis Stream Inspector"
echo "======================================"
echo "Stream: $STREAM_NAME"
echo "Host: $REDIS_HOST:$REDIS_PORT"
echo "======================================"
echo ""

# Check if redis-cli is available
if ! command -v redis-cli &> /dev/null; then
    echo "ERROR: redis-cli not found. Please install Redis CLI tools."
    exit 1
fi

echo "📊 Stream Information:"
echo "--------------------------------------"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XINFO STREAM "$STREAM_NAME" 2>/dev/null || echo "Stream not found or error occurred"
echo ""

echo "📝 Last 10 Stream Entries:"
echo "--------------------------------------"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XREVRANGE "$STREAM_NAME" + - COUNT 10
echo ""

echo "📈 Stream Length:"
echo "--------------------------------------"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XLEN "$STREAM_NAME"
echo ""

echo "👥 Consumer Groups:"
echo "--------------------------------------"
redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XINFO GROUPS "$STREAM_NAME" 2>/dev/null || echo "No consumer groups found"
echo ""

echo "✅ Inspection complete!"
