# Schema Changelog

## [1.0.0] - 2025-11-03

### Added
- Initial schema for `MessageProto` with fields:
  - `producer` (string, tag 1): Identifies the message producer
  - `content` (string, tag 2): Message payload content
  - `created_at` (google.protobuf.Timestamp, tag 3): Message creation timestamp
This document tracks all changes to Protocol Buffer schemas used in RedisFlow.

---

## Guidelines

1. **Never reuse tag numbers** - Reserve numbers for deleted fields
2. **Avoid `required` fields** - Not supported in proto3
3. **Avoid changing field types** - Add new fields and deprecate old ones
4. **Use `snake_case`** for field names
5. **Document all changes** with date, version, and rationale

---

## Schema Versions

### [Unreleased]

_No schema changes yet. Schemas will be added as producer and consumer implementations are developed._

---

## Example Entry Format

```
## [v1.1.0] - 2024-01-15

### Changed
- Added `correlation_id` field to `EventData` message (tag 4)
- Rationale: Enable distributed tracing across services

### Deprecated
- `timestamp` field (tag 3) - Use `event_timestamp` instead for clarity

### Breaking Changes
- None
```

---

## Notes

- This file should be updated **before** merging any PR that modifies `.proto` files
- Breaking changes require major version bump and migration guide
- Use semantic versioning for schema versions
## 2025-11-03 - Initial Schema

### message.proto v1
- Added `MessagePayload` message with fields:
  - `producer` (tag 1): Producer identifier string
  - `content` (tag 2): Message content string
  - `created_at` (tag 3): Creation timestamp using `google.protobuf.Timestamp`
- Used for Redis Stream message serialization
