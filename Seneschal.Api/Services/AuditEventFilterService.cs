using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public static class AuditEventFilterService
{
    public static IReadOnlyCollection<AuditEvent> Apply(
        IEnumerable<AuditEvent> auditEvents,
        AuditEventFilter filter)
    {
        ArgumentNullException.ThrowIfNull(auditEvents);
        ArgumentNullException.ThrowIfNull(filter);

        var matches = auditEvents;

        if (!string.IsNullOrWhiteSpace(filter.IdentityId))
        {
            matches = matches.Where(auditEvent =>
                EqualsFilter(auditEvent.IdentityId, filter.IdentityId));
        }

        if (!string.IsNullOrWhiteSpace(filter.CapabilityId))
        {
            matches = matches.Where(auditEvent =>
                EqualsFilter(auditEvent.CapabilityId, filter.CapabilityId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Decision))
        {
            matches = matches.Where(auditEvent =>
                EqualsFilter(auditEvent.Decision, filter.Decision));
        }

        if (!string.IsNullOrWhiteSpace(filter.EnforcementMode))
        {
            matches = matches.Where(auditEvent =>
                EqualsFilter(
                    auditEvent.EnforcementMode,
                    filter.EnforcementMode));
        }

        if (!string.IsNullOrWhiteSpace(filter.Environment))
        {
            matches = matches.Where(auditEvent =>
                EqualsFilter(auditEvent.Environment, filter.Environment));
        }

        if (!string.IsNullOrWhiteSpace(filter.MatchedPolicy))
        {
            matches = matches.Where(auditEvent =>
                auditEvent.MatchedPolicies.Any(policy =>
                    EqualsFilter(policy, filter.MatchedPolicy)));
        }

        return matches.ToList();
    }

    private static bool EqualsFilter(
        string value,
        string filter)
    {
        return string.Equals(
            value,
            filter,
            StringComparison.OrdinalIgnoreCase);
    }
}
