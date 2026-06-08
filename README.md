# Platform Control Plane

An ASP.NET Core internal platform API that demonstrates senior .NET engineering patterns from the resume set: gateway route ownership, feature-flag rollouts, rollback auditability, cloud spend attribution, SLI snapshots, and queue dead-letter visibility.

The project intentionally uses only framework-provided packages so it can build offline and be reviewed quickly.

## Enterprise Behaviors Demonstrated

- Gateway route registry with owner teams, required scopes, enabled state, upstream URLs, and rate limits.
- Longest-prefix route resolution for API gateway style routing.
- Gradual feature flag rollout using stable SHA-256 subject bucketing.
- Rollback endpoint that disables a flag, drops rollout to zero, and writes an audit event.
- Cost attribution summary that separates tagged and untagged infrastructure spend.
- Dead-letter queue health reports with degraded and critical thresholds.
- Correlation middleware that propagates `traceparent` or `x-correlation-id`.
- No-dependency test runner covering the most important domain behaviors.

## Run

```powershell
dotnet build src\PlatformControlPlane.Api\PlatformControlPlane.Api.csproj
dotnet run --project src\PlatformControlPlane.Api
```

If the SDK assigns a port automatically, use the URL printed by `dotnet run`.

## Test

```powershell
dotnet run --project tests\PlatformControlPlane.Tests
```

## Useful API Calls

```powershell
Invoke-RestMethod http://localhost:5000/feature-flags
Invoke-RestMethod http://localhost:5000/costs/attribution
Invoke-RestMethod http://localhost:5000/sli/snapshots
Invoke-RestMethod http://localhost:5000/audit
```

## Resume Mapping

This project supports bullets around:

- ASP.NET Core internal platform APIs.
- Feature flag systems with gradual rollout and rollback hooks.
- API gateway ownership and rate limiting.
- Azure-style cost attribution APIs.
- Distributed tracing and incident isolation workflows.
- Queue health and dead-letter monitoring.

## Production Next Steps

- Persist configuration in PostgreSQL or SQL Server.
- Add OpenTelemetry packages and export traces to Application Insights or Jaeger.
- Add authentication and policy-based authorization.
- Add real rate-limiting middleware in front of upstream proxy handlers.
- Replace in-memory queue health with Azure Service Bus or Kafka consumer telemetry.
