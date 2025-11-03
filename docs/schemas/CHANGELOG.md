# Schema Changelog

## 2025-11-03 - Initial Schema

### message.proto v1
- Added `EventMessage` with fields:
  - `producer` (string, tag 1): Producer identifier
  - `content` (string, tag 2): Message payload
  - `created_at` (google.protobuf.Timestamp, tag 3): Creation timestamp
- Uses proto3 syntax
- C# namespace: `RedisFlow.Contracts`
