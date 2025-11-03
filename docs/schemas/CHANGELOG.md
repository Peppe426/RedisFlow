# Schema Changelog

## v1.0.0 - Initial Release

### Added
- `message.proto`: Initial schema for Message with producer, content, and created_at fields
  - Field 1: `producer` (string) - Identifier of the message producer
  - Field 2: `content` (string) - Message content
  - Field 3: `created_at` (google.protobuf.Timestamp) - Creation timestamp
