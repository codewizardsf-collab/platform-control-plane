using System.Collections.Concurrent;

namespace PlatformControlPlane.Core;

public sealed record AuditEvent(
    DateTimeOffset Timestamp,
    string Actor,
    string Action,
    string EntityType,
    string EntityId,
    string Details,
    string CorrelationId);

public sealed class AuditLog
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    public void Record(
        string actor,
        string action,
        string entityType,
        string entityId,
        string details,
        string correlationId)
    {
        _events.Enqueue(new AuditEvent(
            DateTimeOffset.UtcNow,
            actor,
            action,
            entityType,
            entityId,
            details,
            correlationId));
    }

    public IReadOnlyList<AuditEvent> Latest(int count = 100)
    {
        return _events
            .Reverse()
            .Take(Math.Clamp(count, 1, 500))
            .ToArray();
    }
}
