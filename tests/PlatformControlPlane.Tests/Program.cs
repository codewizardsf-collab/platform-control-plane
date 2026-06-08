using PlatformControlPlane.Core;

var tests = new (string Name, Action Test)[]
{
    ("feature flag decisions are deterministic", FeatureFlagDecisionsAreDeterministic),
    ("feature flag rollback disables rollout and audits it", FeatureFlagRollbackDisablesRolloutAndAuditsIt),
    ("gateway route registry resolves longest enabled prefix", GatewayRouteRegistryResolvesLongestEnabledPrefix),
    ("cost attribution separates tagged and untagged spend", CostAttributionSeparatesTaggedAndUntaggedSpend),
    ("queue health escalates dead-letter volume", QueueHealthEscalatesDeadLetterVolume)
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        test.Test();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Failures:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Environment.Exit(1);
}

static void FeatureFlagDecisionsAreDeterministic()
{
    var audit = new AuditLog();
    var flags = new FeatureFlagService(audit);

    flags.Upsert(new UpsertFeatureFlagRequest(
        "checkout-v2",
        "Staged checkout release",
        "commerce",
        true,
        50,
        null), "test", "test-correlation");

    var first = flags.Evaluate("checkout-v2", "customer-1024");
    var second = flags.Evaluate("checkout-v2", "customer-1024");

    AssertEqual(first.Bucket, second.Bucket, "same subject should stay in the same rollout bucket");
    AssertEqual(first.Enabled, second.Enabled, "same subject should receive stable flag decision");
}

static void FeatureFlagRollbackDisablesRolloutAndAuditsIt()
{
    var audit = new AuditLog();
    var flags = new FeatureFlagService(audit);

    flags.Upsert(new UpsertFeatureFlagRequest(
        "fast-reconciliation",
        "Fast settlement path",
        "payments",
        true,
        100,
        "https://runbooks.local/rollback"), "release-manager", "corr-1");

    var rolledBack = flags.Rollback("fast-reconciliation", "release-manager", "corr-2");
    var decision = flags.Evaluate("fast-reconciliation", "merchant-44");

    AssertFalse(rolledBack.Enabled, "rollback should disable the flag");
    AssertEqual(0, rolledBack.RolloutPercentage, "rollback should set rollout to zero");
    AssertFalse(decision.Enabled, "flag decision should be disabled after rollback");
    AssertTrue(audit.Latest().Any(e => e.Action == "feature_flag_rolled_back"), "rollback should create audit event");
}

static void GatewayRouteRegistryResolvesLongestEnabledPrefix()
{
    var audit = new AuditLog();
    var registry = new GatewayRouteRegistry(audit);

    registry.Upsert(new GatewayRoute("orders", "/orders", "https://orders.local", "platform", 600, true, ["orders.read"]), "test", "corr");
    registry.Upsert(new GatewayRoute("orders-admin", "/orders/admin", "https://orders-admin.local", "platform", 60, true, ["orders.admin"]), "test", "corr");

    var resolved = registry.TryResolve("/orders/admin/replay", out var route);

    AssertTrue(resolved, "route should resolve");
    AssertEqual("orders-admin", route.RouteId, "longest matching prefix should win");
}

static void CostAttributionSeparatesTaggedAndUntaggedSpend()
{
    var audit = new AuditLog();
    var costs = new CostAttributionService(audit);

    costs.Upsert(new CloudSpendRecord("redis-01", "feature-flags", "platform", "prod", 1200m), "test", "corr");
    costs.Upsert(new CloudSpendRecord("vm-orphan", "legacy-worker", null, "prod", 450m), "test", "corr");

    var summary = costs.Summarize();

    AssertEqual(1650m, summary.TotalMonthlyCost, "total monthly cost should include all records");
    AssertEqual(450m, summary.UnattributedMonthlyCost, "untagged spend should be isolated");
    AssertEqual("vm-orphan", summary.UnattributedResources.Single().ResourceId, "orphan resource should be surfaced");
}

static void QueueHealthEscalatesDeadLetterVolume()
{
    var audit = new AuditLog();
    var queues = new QueueHealthService(audit);

    for (var index = 0; index < 10; index++)
    {
        queues.RegisterDeadLetter(new DeadLetterEvent(
            "service-bus-payments-high",
            $"msg-{index}",
            "downstream timeout",
            DateTimeOffset.UtcNow.AddMinutes(-index),
            "high"), "test", "corr");
    }

    var report = queues.Reports().Single();
    AssertEqual("critical", report.Status, "ten dead letters should trigger critical status");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    AssertTrue(!condition, message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}. Expected '{expected}', got '{actual}'.");
    }
}
