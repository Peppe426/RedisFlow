---
name:
description:
---

# Mando
---

## Target Platform & Language
- **Framework:** .NET 9 (unless a folder explicitly documents a different target).  
- **Language:** C# (set `LangVersion` to `latest` or use project default).  
- **Serialization:** Use **Protocol Buffers (protobuf)** for all messages written to Redis Streams.  
  - No JSON or schema-less formats (e.g., MessagePack) for stream payloads.

---

## Repository Layout
| Folder | Purpose |
|---------|----------|
| `src/` | Source projects (producers, consumers, shared contracts). |
| `tests/` | Unit and integration tests using NUnit + FluentAssertions. |
| `docs/schemas/` | `.proto` schema files (versioned and tracked). |
| `docs/` | Additional design and operational docs (runbooks, schema evolution notes). |

---

## Protobuf (.proto) Rules
- Store `.proto` files under `docs/schemas/` or a shared package.
- Follow protobuf best practices:
  - **Never reuse tag numbers.** Reserve numbers for deleted fields.
  - Avoid `required` fields (not supported in proto3).
  - Avoid changing existing field types. Add new fields and deprecate the old ones.
  - Use `snake_case` for field names.
  - Document all schema changes in `docs/schemas/CHANGELOG.md`.
  - Use well-known types (e.g., `google.protobuf.Timestamp`) and import explicitly.
- **Tooling:** Generate C# types during build (`Grpc.Tools` or a build-time `protoc` step).  
  - Do **not** check in generated types unless absolutely necessary.

---

## C# / .NET Best Practices
- Follow standard Microsoft conventions:
  - PascalCase for public members.
  - camelCase or `_camelCase` for private fields.
- Use expression-bodied members when concise and clear.
- Prefer immutable DTOs (readonly or init-only properties).
- Use `System.Text.Json` **only** for admin tools, never for stream payloads.
- Use dependency injection via `Microsoft.Extensions.DependencyInjection`.
- Keep application composition in a single `HostBuilder` entry point.

---

## Redis / Streams Guidelines
- Target **Redis Enterprise (ENT / Aspire)** for on-prem deployments.
- Use **consumer groups** for parallel processing.
- Manage **pending entries lists (PEL)** properly and implement **claim/retry** logic (`XCLAIM`, `XPENDING`) for consumer restarts.
- Keep protobuf payloads small; store large blobs separately by ID reference.
- Document retention and eviction policies for streams under `docs/`.

---

## Testing Rules
- Frameworks: **NUnit** + **FluentAssertions**.
- Pattern: **Given / When / Then** inside each test.
- Naming:  

- **All tests must follow the structure and conventions in**  
[`docs/Test structure.md`](/docs/Test%20structure.md)
- One logical assertion per test.
- Shared fixtures and helpers live in `tests/Shared/`.
- Async tests use `async Task` and assertions like:
```csharp
await act.Should().ThrowAsync<T>();
