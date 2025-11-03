# Schema Changelog

## [1.0.0] - 2025-11-03

### Added
- Initial schema for `MessageProto` with fields:
  - `producer` (string, tag 1): Identifies the message producer
  - `content` (string, tag 2): Message payload content
  - `created_at` (google.protobuf.Timestamp, tag 3): Message creation timestamp
