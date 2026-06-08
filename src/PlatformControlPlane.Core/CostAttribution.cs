using System.Collections.Concurrent;

namespace PlatformControlPlane.Core;

public sealed record CloudSpendRecord(
    string ResourceId,
    string ServiceName,
    string? OwnerTeam,
    string Environment,
    decimal MonthlyCost);

public sealed record OwnerCost(string OwnerTeam, decimal MonthlyCost, int ResourceCount);

public sealed record CostAttributionSummary(
    decimal TotalMonthlyCost,
    decimal AttributedMonthlyCost,
    decimal UnattributedMonthlyCost,
    IReadOnlyList<OwnerCost> CostsByOwner,
    IReadOnlyList<CloudSpendRecord> UnattributedResources);

public sealed class CostAttributionService
{
    private readonly ConcurrentDictionary<string, CloudSpendRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly AuditLog _auditLog;

    public CostAttributionService(AuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    public CloudSpendRecord Upsert(CloudSpendRecord record, string actor, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(record.ResourceId))
        {
            throw new ArgumentException("ResourceId is required.", nameof(record));
        }

        if (record.MonthlyCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(record), "MonthlyCost cannot be negative.");
        }

        var normalized = record with
        {
            OwnerTeam = string.IsNullOrWhiteSpace(record.OwnerTeam) ? null : record.OwnerTeam.Trim(),
            Environment = string.IsNullOrWhiteSpace(record.Environment) ? "unknown" : record.Environment.Trim()
        };

        _records[normalized.ResourceId] = normalized;
        _auditLog.Record(
            actor,
            "cost_record_upserted",
            "cloud_spend_record",
            normalized.ResourceId,
            $"{normalized.ServiceName} ${normalized.MonthlyCost:n2}/month",
            correlationId);

        return normalized;
    }

    public CostAttributionSummary Summarize()
    {
        var records = _records.Values.ToArray();
        var attributed = records.Where(r => !string.IsNullOrWhiteSpace(r.OwnerTeam)).ToArray();
        var unattributed = records.Where(r => string.IsNullOrWhiteSpace(r.OwnerTeam)).OrderByDescending(r => r.MonthlyCost).ToArray();

        var costsByOwner = attributed
            .GroupBy(r => r.OwnerTeam!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new OwnerCost(g.Key, g.Sum(r => r.MonthlyCost), g.Count()))
            .OrderByDescending(g => g.MonthlyCost)
            .ToArray();

        return new CostAttributionSummary(
            records.Sum(r => r.MonthlyCost),
            attributed.Sum(r => r.MonthlyCost),
            unattributed.Sum(r => r.MonthlyCost),
            costsByOwner,
            unattributed);
    }
}
