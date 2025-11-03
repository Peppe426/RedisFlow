# Schema Changelog

## 2025-11-03 - Initial Schema

### message.proto v1
- Added `MessagePayload` message with fields:
  - `producer` (tag 1): Producer identifier string
  - `content` (tag 2): Message content string
  - `created_at` (tag 3): Creation timestamp using `google.protobuf.Timestamp`
- Used for Redis Stream message serialization
