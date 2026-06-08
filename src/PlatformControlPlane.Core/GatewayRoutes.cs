using System.Collections.Concurrent;

namespace PlatformControlPlane.Core;

public sealed record GatewayRoute(
    string RouteId,
    string PathPrefix,
    string UpstreamBaseUrl,
    string OwnerTeam,
    int RateLimitPerMinute,
    bool Enabled,
    IReadOnlyList<string> RequiredScopes);

public sealed class GatewayRouteRegistry
{
    private readonly ConcurrentDictionary<string, GatewayRoute> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly AuditLog _auditLog;

    public GatewayRouteRegistry(AuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    public IReadOnlyList<GatewayRoute> All()
    {
        return _routes.Values.OrderBy(route => route.PathPrefix).ToArray();
    }

    public GatewayRoute Upsert(GatewayRoute route, string actor, string correlationId)
    {
        Validate(route);

        var normalized = route with
        {
            RouteId = route.RouteId.Trim(),
            PathPrefix = NormalizePath(route.PathPrefix),
            UpstreamBaseUrl = route.UpstreamBaseUrl.Trim().TrimEnd('/'),
            OwnerTeam = route.OwnerTeam.Trim(),
            RequiredScopes = route.RequiredScopes.Select(scope => scope.Trim()).Where(scope => scope.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };

        _routes[normalized.RouteId] = normalized;
        _auditLog.Record(
            actor,
            "gateway_route_upserted",
            "gateway_route",
            normalized.RouteId,
            $"{normalized.PathPrefix} -> {normalized.UpstreamBaseUrl}; limit={normalized.RateLimitPerMinute}/min",
            correlationId);

        return normalized;
    }

    public bool TryResolve(string requestPath, out GatewayRoute route)
    {
        route = _routes.Values
            .Where(candidate => candidate.Enabled)
            .OrderByDescending(candidate => candidate.PathPrefix.Length)
            .FirstOrDefault(candidate => requestPath.StartsWith(candidate.PathPrefix, StringComparison.OrdinalIgnoreCase))!;

        return route is not null;
    }

    private static void Validate(GatewayRoute route)
    {
        if (string.IsNullOrWhiteSpace(route.RouteId))
        {
            throw new ArgumentException("RouteId is required.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(route.PathPrefix) || !route.PathPrefix.StartsWith('/'))
        {
            throw new ArgumentException("PathPrefix must start with '/'.", nameof(route));
        }

        if (!Uri.TryCreate(route.UpstreamBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("UpstreamBaseUrl must be an absolute URL.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(route.OwnerTeam))
        {
            throw new ArgumentException("OwnerTeam is required.", nameof(route));
        }

        if (route.RateLimitPerMinute < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(route), "RateLimitPerMinute must be greater than zero.");
        }
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        return trimmed.Length > 1 ? trimmed.TrimEnd('/') : trimmed;
    }
}
