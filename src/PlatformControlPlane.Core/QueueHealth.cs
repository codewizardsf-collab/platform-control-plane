using System.Collections.Concurrent;

namespace PlatformControlPlane.Core;

public sealed record DeadLetterEvent(
    string QueueName,
    string MessageId,
    string Reason,
    DateTimeOffset FailedAt,
    string Priority);

public sealed record QueueHealthReport(
    string QueueName,
    string Priority,
    int DeadLetterCount,
    DateTimeOffset? LastFailureAt,
    string Status);

public sealed class QueueHealthService
{
    private readonly ConcurrentBag<DeadLetterEvent> _events = new();
    private readonly AuditLog _auditLog;

    public QueueHealthService(AuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    public DeadLetterEvent RegisterDeadLetter(DeadLetterEvent deadLetter, string actor, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(deadLetter.QueueName))
        {
            throw new ArgumentException("QueueName is required.", nameof(deadLetter));
        }

        if (string.IsNullOrWhiteSpace(deadLetter.MessageId))
        {
            throw new ArgumentException("MessageId is required.", nameof(deadLetter));
        }

        var normalized = deadLetter with
        {
            QueueName = deadLetter.QueueName.Trim(),
            MessageId = deadLetter.MessageId.Trim(),
            Reason = string.IsNullOrWhiteSpace(deadLetter.Reason) ? "unspecified" : deadLetter.Reason.Trim(),
            Priority = string.IsNullOrWhiteSpace(deadLetter.Priority) ? "normal" : deadLetter.Priority.Trim().ToLowerInvariant(),
            FailedAt = deadLetter.FailedAt == default ? DateTimeOffset.UtcNow : deadLetter.FailedAt
        };

        _events.Add(normalized);
        _auditLog.Record(
            actor,
            "dead_letter_registered",
            "queue",
            normalized.QueueName,
            $"{normalized.MessageId}: {normalized.Reason}",
            correlationId);

        return normalized;
    }

    public IReadOnlyList<QueueHealthReport> Reports()
    {
        return _events
            .GroupBy(e => new { e.QueueName, e.Priority })
            .Select(g =>
            {
                var count = g.Count();
                return new QueueHealthReport(
                    g.Key.QueueName,
                    g.Key.Priority,
                    count,
                    g.Max(e => e.FailedAt),
                    count switch
                    {
                        >= 10 => "critical",
                        >= 3 => "degraded",
                        _ => "healthy"
                    });
            })
            .OrderByDescending(report => report.DeadLetterCount)
            .ToArray();
    }
}
