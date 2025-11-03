# Schema Changelog

All notable changes to the protobuf schemas will be documented in this file.

## [1.0.0] - 2025-11-03

### Added
- Initial `message.proto` schema definition
- `Message` type with fields:
  - `producer` (string, tag 1): Identifier of the message producer
  - `content` (string, tag 2): Message payload/content
  - `created_at` (google.protobuf.Timestamp, tag 3): UTC timestamp of message creation

### Schema Evolution Guidelines
- **Never reuse tag numbers** - once assigned, a tag number is permanent
- **Never change field types** - add new fields instead and deprecate old ones
- **Use reserved for deleted fields** - prevents accidental reuse
- All changes must be backward-compatible
- Document all changes in this file with date and version

### Example of Adding a New Field
```protobuf
message Message {
  string producer = 1;
  string content = 2;
  google.protobuf.Timestamp created_at = 3;
  string new_field = 4;  // Added in version 1.1.0
}
```

### Example of Deprecating a Field
```protobuf
message Message {
  reserved 2;  // 'old_content' deprecated in version 2.0.0
  reserved "old_content";
  
  string producer = 1;
  string content_v2 = 4;  // Replacement for old 'content'
  google.protobuf.Timestamp created_at = 3;
}
```
