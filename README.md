
---

## Overview

This repository is a proof-of-concept (POC) for using Redis in an on-premises environment.
The intent is to target Redis Enterprise (ENT) — Aspire deployments for production/on-prem usage.

This POC enforces a single, explicit serialization choice (see below). The README intentionally
does not include code examples at this stage.

---

## ⚙️ Components
- **Redis Server:** On-premises Redis Enterprise (ENT / Aspire) is the target platform. The server
    provides Streams and Consumer Group capabilities used by this POC.
- **Producers:** .NET console apps (producers push binary-encoded messages to the Redis stream).
- **Consumer:** .NET console app (reads and acknowledges messages using a consumer group).

---

## 🧩 Technical Objectives
1. Provision an on-prem Redis Enterprise (ENT) instance suitable for Streams and consumer groups.
2. Publish messages to a Redis Stream using Protocol Buffers (binary protobuf encoding).
3. Consume and acknowledge messages using consumer groups and verify pending message handling.
4. Demonstrate message persistence and replay behavior on consumer restart and failures.
5. Observe system behavior when producers go offline, and validate pending message recovery.

---

## 🔄 Serialization Strategy (PROTOCOL BUFFERS — REQUIRED)

This project requires Protocol Buffers (protobuf) for message serialization. JSON and other
schema-less binary formats (e.g., MessagePack) are not acceptable for this POC — a .proto schema
must be used and versioned along with the code that produces and consumes messages.

Why protobuf?
- Compact binary encoding with a defined schema (.proto files).
- Strong versioning and compatibility guarantees suited for cross-service contracts.
- Native, well-supported tooling for .NET and many other platforms.

Requirements:
- Define message formats using `.proto` schema files in the repository (or in a shared schema
    package) so producers and consumers use the same contract.
- Use the protobuf binary wire format when writing to Redis Streams (no JSON payloads).
- Include schema evolution notes in `docs/` as message types change.

---

## Notes / Next steps
- Add `.proto` schema files under `docs/schemas/` or a shared package and document versioning.
- Provide deployment and run instructions for on-prem Redis Enterprise (ENT) in `docs/`.
- Add minimal runnable examples later once schemas and deployment choices are finalized.
