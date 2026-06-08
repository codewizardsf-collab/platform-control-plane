using PlatformControlPlane.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AuditLog>();
builder.Services.AddSingleton<GatewayRouteRegistry>();
builder.Services.AddSingleton<FeatureFlagService>();
builder.Services.AddSingleton<CostAttributionService>();
builder.Services.AddSingleton<QueueHealthService>();
builder.Services.AddSingleton<SliSnapshotService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("traceparent", out var traceParent)
        ? traceParent.ToString()
        : context.Request.Headers.TryGetValue("x-correlation-id", out var existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("n");

    context.Items["correlation-id"] = correlationId;
    context.Response.Headers["x-correlation-id"] = correlationId;
    await next();
});

SeedData.Load(
    app.Services.GetRequiredService<GatewayRouteRegistry>(),
    app.Services.GetRequiredService<FeatureFlagService>(),
    app.Services.GetRequiredService<CostAttributionService>(),
    app.Services.GetRequiredService<QueueHealthService>());

app.MapGet("/", () => new
{
    service = "platform-control-plane",
    capabilities = new[]
    {
        "gateway route governance",
        "gradual feature rollout",
        "rollback audit trail",
        "cost attribution",
        "queue dead-letter monitoring",
        "service SLI snapshots"
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", checkedAt = DateTimeOffset.UtcNow }));

app.MapGet("/gateway/routes", (GatewayRouteRegistry registry) => registry.All());

app.MapPost("/gateway/routes", (GatewayRoute route, GatewayRouteRegistry registry, HttpContext context) =>
    Guard(() =>
    {
        var created = registry.Upsert(route, Actor(context), CorrelationId(context));
        return Results.Created($"/gateway/routes/{created.RouteId}", created);
    }));

app.MapPost("/gateway/resolve", (ResolveRouteRequest request, GatewayRouteRegistry registry) =>
{
    if (registry.TryResolve(request.Path, out var route))
    {
        return Results.Ok(route);
    }

    return Results.NotFound(new { error = "No enabled gateway route matched the requested path." });
});

app.MapGet("/feature-flags", (FeatureFlagService flags) => flags.All());

app.MapPost("/feature-flags", (UpsertFeatureFlagRequest request, FeatureFlagService flags, HttpContext context) =>
    Guard(() =>
    {
        var created = flags.Upsert(request, Actor(context), CorrelationId(context));
        return Results.Created($"/feature-flags/{created.Key}", created);
    }));

app.MapPost("/feature-flags/{key}/decisions", (string key, FeatureDecisionRequest request, FeatureFlagService flags) =>
    Results.Ok(flags.Evaluate(key, request.SubjectId)));

app.MapPost("/feature-flags/{key}/rollback", (string key, FeatureFlagService flags, HttpContext context) =>
    Guard(() => Results.Ok(flags.Rollback(key, Actor(context), CorrelationId(context)))));

app.MapGet("/costs/attribution", (CostAttributionService costs) => costs.Summarize());

app.MapPost("/costs/records", (CloudSpendRecord record, CostAttributionService costs, HttpContext context) =>
    Guard(() =>
    {
        var created = costs.Upsert(record, Actor(context), CorrelationId(context));
        return Results.Created($"/costs/records/{created.ResourceId}", created);
    }));

app.MapGet("/queues/health", (QueueHealthService queues) => queues.Reports());

app.MapPost("/queues/dead-letters", (DeadLetterEvent deadLetter, QueueHealthService queues, HttpContext context) =>
    Guard(() =>
    {
        var created = queues.RegisterDeadLetter(deadLetter, Actor(context), CorrelationId(context));
        return Results.Created($"/queues/dead-letters/{created.MessageId}", created);
    }));

app.MapGet("/sli/snapshots", (SliSnapshotService snapshots) => snapshots.Current());

app.MapGet("/audit", (AuditLog auditLog, int? count) => auditLog.Latest(count ?? 100));

app.Run();

static string Actor(HttpContext context)
{
    return context.Request.Headers.TryGetValue("x-actor", out var actor) && !string.IsNullOrWhiteSpace(actor.ToString())
        ? actor.ToString()
        : "local-demo";
}

static string CorrelationId(HttpContext context)
{
    return context.Items.TryGetValue("correlation-id", out var value) && value is string correlationId
        ? correlationId
        : Guid.NewGuid().ToString("n");
}

static IResult Guard(Func<IResult> action)
{
    try
    {
        return action();
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
}

public sealed record ResolveRouteRequest(string Path);

public sealed record FeatureDecisionRequest(string SubjectId);
