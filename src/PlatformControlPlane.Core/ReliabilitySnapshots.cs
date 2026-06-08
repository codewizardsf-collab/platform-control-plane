namespace PlatformControlPlane.Core;

public sealed record ServiceReliabilitySnapshot(
    string ServiceName,
    string OwnerTeam,
    int DeploymentsThisWeek,
    double P99LatencyMs,
    double ErrorRatePercent,
    DateTimeOffset? LastIncidentAt)
{
    public string HealthGrade
    {
        get
        {
            if (ErrorRatePercent >= 2.0 || P99LatencyMs >= 1200)
            {
                return "critical";
            }

            if (ErrorRatePercent >= 0.5 || P99LatencyMs >= 500)
            {
                return "watch";
            }

            return "healthy";
        }
    }
}

public sealed class SliSnapshotService
{
    private readonly List<ServiceReliabilitySnapshot> _snapshots =
    [
        new("api-gateway", "platform", 11, 84, 0.04, null),
        new("feature-flags", "platform", 6, 122, 0.02, null),
        new("deployment-dashboard", "developer-experience", 9, 241, 0.13, DateTimeOffset.UtcNow.AddDays(-12)),
        new("cost-attribution", "finops", 3, 418, 0.22, DateTimeOffset.UtcNow.AddDays(-24))
    ];

    public IReadOnlyList<ServiceReliabilitySnapshot> Current()
    {
        return _snapshots.OrderBy(snapshot => snapshot.ServiceName).ToArray();
    }
}
