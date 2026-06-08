namespace PlatformControlPlane.Core;

public static class SeedData
{
    public static void Load(
        GatewayRouteRegistry routes,
        FeatureFlagService flags,
        CostAttributionService costs,
        QueueHealthService queues)
    {
        const string actor = "seed";
        const string correlationId = "seed-data";

        routes.Upsert(new GatewayRoute(
            "orders-api",
            "/orders",
            "https://orders.internal.local",
            "trading-platform",
            1800,
            true,
            ["orders.read", "orders.write"]), actor, correlationId);

        routes.Upsert(new GatewayRoute(
            "cost-attribution-api",
            "/costs",
            "https://finops.internal.local",
            "finops",
            600,
            true,
            ["costs.read"]), actor, correlationId);

        flags.Upsert(new UpsertFeatureFlagRequest(
            "gradual-rollout-checkout-v2",
            "Controls staged rollout for the redesigned checkout workflow.",
            "commerce-platform",
            true,
            35,
            "https://runbooks.internal.local/rollback/checkout-v2"), actor, correlationId);

        flags.Upsert(new UpsertFeatureFlagRequest(
            "reconciliation-fast-path",
            "Enables optimized settlement reconciliation path.",
            "payments",
            false,
            0,
            "https://runbooks.internal.local/rollback/reconciliation-fast-path"), actor, correlationId);

        costs.Upsert(new CloudSpendRecord("vm-api-gateway-prod-01", "api-gateway", "platform", "prod", 12840.55m), actor, correlationId);
        costs.Upsert(new CloudSpendRecord("redis-feature-flags-prod", "feature-flags", "platform", "prod", 3840.15m), actor, correlationId);
        costs.Upsert(new CloudSpendRecord("orphaned-aks-nodepool-legacy", "legacy-worker", null, "prod", 17520.40m), actor, correlationId);

        queues.RegisterDeadLetter(new DeadLetterEvent(
            "service-bus-payments-high",
            "msg-100421",
            "downstream settlement timeout",
            DateTimeOffset.UtcNow.AddMinutes(-27),
            "high"), actor, correlationId);

        queues.RegisterDeadLetter(new DeadLetterEvent(
            "service-bus-payments-normal",
            "msg-100422",
            "schema version mismatch",
            DateTimeOffset.UtcNow.AddMinutes(-11),
            "normal"), actor, correlationId);
    }
}
