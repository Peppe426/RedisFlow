# RedisFlow Integration Tests

This directory contains integration tests that verify Redis stream operations using the Aspire host.

## Prerequisites

These tests require:
- **.NET 9 SDK**
- **Docker Desktop** (or Docker Engine with Docker Compose)
- **.NET Aspire workload** installed

### Installing Aspire Workload

```bash
dotnet workload install aspire
```

## Running Tests Locally

```bash
# From the repository root
dotnet test tests/RedisFlow.Integration/
```

The tests will:
1. Automatically start the Aspire host
2. Provision a Redis container via Docker
3. Execute stream operations (produce, consume, acknowledge)
4. Verify pending message replay scenarios
5. Clean up resources after completion

## CI/CD Environments

These tests **will not run** in CI/CD environments without Docker and Aspire DCP (Distributed Application Coordinator) available. This is expected behavior.

For CI/CD pipelines, consider:
- Using a Redis service container instead of Aspire orchestration
- Implementing alternative integration tests that don't rely on Aspire.Hosting.Testing
- Running these tests only on self-hosted runners with full Docker support

## Test Structure

All tests follow the project's test structure conventions (see `docs/Test structure.md`):
- **Given / When / Then** pattern
- **NUnit** test framework
- **FluentAssertions** for readable assertions
- One logical assertion per test

## What's Being Tested

### Connection Tests
- Verify Redis is accessible from Aspire host
- Validate connection string discovery

### Stream Operations
- Create streams with `XADD`
- Read messages with consumer groups (`XREADGROUP`)
- Acknowledge messages (`XACK`)

### Resilience Scenarios
- Pending message replay (`XPENDING` + `XCLAIM`)
- Consumer restart behavior
- Consumer group mechanics

## Troubleshooting

### Tests Timeout or Fail to Start

**Symptom:** Tests fail with `TimeoutRejectedException` during setup

**Cause:** Aspire DCP is not available or Docker is not running

**Solution:**
1. Ensure Docker Desktop is running
2. Install Aspire workload: `dotnet workload install aspire`
3. Verify Docker is accessible: `docker ps`

### Container Startup Issues

**Symptom:** Tests fail with container startup errors

**Solution:**
1. Pull Redis image manually: `docker pull redis:latest`
2. Check Docker logs for errors
3. Ensure no port conflicts (Redis typically uses 6379)

### Connection String Errors

**Symptom:** Tests fail with "connection string not found"

**Solution:**
1. Ensure AppHost defines the "redis" resource correctly
2. Check AppHost.cs configuration
3. Verify ServiceDefaults are properly referenced
