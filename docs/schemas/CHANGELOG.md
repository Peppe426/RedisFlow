# Schema Changelog

## 2025-11-03 - Initial Schema

### message.proto v1.0
- Added `StreamMessage` with fields:
  - `producer` (string, tag 1): Unique identifier for the producer
  - `content` (string, tag 2): Message content/payload
  - `created_at` (google.protobuf.Timestamp, tag 3): Message creation timestamp
