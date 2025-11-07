#!/bin/bash
# monitor-stream.sh - Real-time monitoring of Redis Stream activity
# Usage: ./monitor-stream.sh [stream-name] [redis-host] [redis-port]

STREAM_NAME=${1:-"mystream"}
REDIS_HOST=${2:-"localhost"}
REDIS_PORT=${3:-6379}
REFRESH_INTERVAL=2

echo "======================================"
echo "Redis Stream Monitor"
echo "======================================"
echo "Stream: $STREAM_NAME"
echo "Host: $REDIS_HOST:$REDIS_PORT"
echo "Refresh: ${REFRESH_INTERVAL}s"
echo "Press Ctrl+C to stop"
echo "======================================"
echo ""

# Check if redis-cli is available
if ! command -v redis-cli &> /dev/null; then
    echo "ERROR: redis-cli not found. Please install Redis CLI tools."
    exit 1
fi

while true; do
    clear
    echo "======================================"
    echo "Redis Stream Monitor - $(date '+%Y-%m-%d %H:%M:%S')"
    echo "======================================"
    echo ""
    
    echo "📊 Stream Stats:"
    echo "--------------------------------------"
    STREAM_LENGTH=$(redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XLEN "$STREAM_NAME" 2>/dev/null)
    echo "Total entries: ${STREAM_LENGTH:-0}"
    echo ""
    
    echo "👥 Consumer Groups:"
    echo "--------------------------------------"
    redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XINFO GROUPS "$STREAM_NAME" 2>/dev/null || echo "No groups"
    echo ""
    
    echo "📝 Latest Entry:"
    echo "--------------------------------------"
    redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" XREVRANGE "$STREAM_NAME" + - COUNT 1
    echo ""
    
    sleep "$REFRESH_INTERVAL"
done
