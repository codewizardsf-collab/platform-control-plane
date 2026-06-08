# Platform Control Plane

An ASP.NET Core internal platform API for gateway route governance, feature-flag rollouts, rollback auditing, cloud cost attribution, queue health, and SLI snapshots.

## Stack

.NET 8, ASP.NET Core, internal platform services

## Problem

Platform teams need reliable self-service controls for routing, rollout safety, operational visibility, and cost ownership.

## Architecture

- PlatformControlPlane.Api exposes minimal API endpoints.
- PlatformControlPlane.Core contains domain services for flags, routes, cost, queues, and audit.
- A no-dependency test runner verifies critical domain behavior.

## Implemented Production Readiness

- CI builds API and test projects.
- Dockerfile and Compose file are included.
- Correlation IDs are propagated through middleware.
- Feature rollback creates audit events.

## Run And Test

```powershell
dotnet build src\PlatformControlPlane.Api\PlatformControlPlane.Api.csproj
dotnet run --project tests\PlatformControlPlane.Tests
```

## Quality Gates

- Project-specific GitHub Actions workflow included under .github/workflows/ci.yml.
- Generated build outputs and dependency folders are excluded through .gitignore.
- Tests and validation commands are intentionally small enough to run during code review.

## Production Extension Points

- Persist configuration in SQL Server or PostgreSQL.
- Add policy-based authorization.
- Export OpenTelemetry traces to Application Insights or Jaeger.

## Repository Hygiene

This repository contains original portfolio code only. It does not include employer source code, private resumes, generated binaries, local credentials, or large media files.

