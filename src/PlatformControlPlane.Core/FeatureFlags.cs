using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace PlatformControlPlane.Core;

public sealed record FeatureFlag(
    string Key,
    string Description,
    string OwnerTeam,
    bool Enabled,
    int RolloutPercentage,
    string? RollbackHook,
    DateTimeOffset UpdatedAt);

public sealed record FeatureFlagDecision(
    string Key,
    string SubjectId,
    bool Enabled,
    int Bucket,
    string Reason);

public sealed record UpsertFeatureFlagRequest(
    string Key,
    string Description,
    string OwnerTeam,
    bool Enabled,
    int RolloutPercentage,
    string? RollbackHook);

public sealed class FeatureFlagService
{
    private readonly ConcurrentDictionary<string, FeatureFlag> _flags = new(StringComparer.OrdinalIgnoreCase);
    private readonly AuditLog _auditLog;

    public FeatureFlagService(AuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    public IReadOnlyList<FeatureFlag> All()
    {
        return _flags.Values.OrderBy(flag => flag.Key).ToArray();
    }

    public FeatureFlag Upsert(UpsertFeatureFlagRequest request, string actor, string correlationId)
    {
        Validate(request);

        var flag = new FeatureFlag(
            request.Key.Trim(),
            request.Description.Trim(),
            request.OwnerTeam.Trim(),
            request.Enabled,
            request.RolloutPercentage,
            string.IsNullOrWhiteSpace(request.RollbackHook) ? null : request.RollbackHook.Trim(),
            DateTimeOffset.UtcNow);

        _flags[flag.Key] = flag;
        _auditLog.Record(
            actor,
            "feature_flag_upserted",
            "feature_flag",
            flag.Key,
            $"Enabled={flag.Enabled}; Rollout={flag.RolloutPercentage}%",
            correlationId);

        return flag;
    }

    public FeatureFlagDecision Evaluate(string key, string subjectId)
    {
        if (!_flags.TryGetValue(key, out var flag))
        {
            return new FeatureFlagDecision(key, subjectId, false, -1, "flag_not_found");
        }

        if (!flag.Enabled)
        {
            return new FeatureFlagDecision(key, subjectId, false, -1, "flag_disabled");
        }

        var bucket = StableBucket(key, subjectId);
        var enabled = bucket < flag.RolloutPercentage;
        return new FeatureFlagDecision(
            flag.Key,
            subjectId,
            enabled,
            bucket,
            enabled ? "inside_rollout_bucket" : "outside_rollout_bucket");
    }

    public FeatureFlag Rollback(string key, string actor, string correlationId)
    {
        if (!_flags.TryGetValue(key, out var existing))
        {
            throw new KeyNotFoundException($"Feature flag '{key}' was not found.");
        }

        var rolledBack = existing with
        {
            Enabled = false,
            RolloutPercentage = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _flags[key] = rolledBack;
        _auditLog.Record(
            actor,
            "feature_flag_rolled_back",
            "feature_flag",
            rolledBack.Key,
            $"Rollback hook: {rolledBack.RollbackHook ?? "not-configured"}",
            correlationId);

        return rolledBack;
    }

    private static void Validate(UpsertFeatureFlagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            throw new ArgumentException("Feature flag key is required.", nameof(request));
        }

        if (request.Key.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Feature flag key cannot contain whitespace.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Feature flag description is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OwnerTeam))
        {
            throw new ArgumentException("Feature flag owner team is required.", nameof(request));
        }

        if (request.RolloutPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "RolloutPercentage must be between 0 and 100.");
        }
    }

    private static int StableBucket(string key, string subjectId)
    {
        var input = Encoding.UTF8.GetBytes($"{key}:{subjectId}");
        var hash = SHA256.HashData(input);
        return BitConverter.ToUInt16(hash, 0) % 100;
    }
}
