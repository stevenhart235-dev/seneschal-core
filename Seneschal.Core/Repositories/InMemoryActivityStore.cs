using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryActivityStore : IActivityStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CapabilityAccumulator> _capabilities =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IdentityAccumulator> _identities =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PolicyAccumulator> _policies =
        new(StringComparer.OrdinalIgnoreCase);

    public Task RecordAsync(
        AuditEvent decisionEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisionEvent);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            GetCapability(decisionEvent.CapabilityId)
                .Record(decisionEvent);
            GetIdentity(decisionEvent.IdentityId)
                .Record(decisionEvent);

            foreach (var policyId in decisionEvent.MatchedPolicies
                .Where(policy => !string.IsNullOrWhiteSpace(policy)))
            {
                GetPolicy(policyId).Record(decisionEvent);
            }
        }

        return Task.CompletedTask;
    }

    public Task<ActivitySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(new ActivitySnapshot
            {
                Capabilities = _capabilities.Values
                    .Select(activity => activity.ToActivity())
                    .OrderBy(activity => activity.CapabilityId)
                    .ToList(),
                Identities = _identities.Values
                    .Select(activity => activity.ToActivity())
                    .OrderBy(activity => activity.IdentityId)
                    .ToList(),
                Policies = _policies.Values
                    .Select(activity => activity.ToActivity())
                    .OrderBy(activity => activity.PolicyId)
                    .ToList()
            });
        }
    }

    private CapabilityAccumulator GetCapability(string capabilityId)
    {
        if (!_capabilities.TryGetValue(capabilityId, out var activity))
        {
            activity = new CapabilityAccumulator(capabilityId);
            _capabilities[capabilityId] = activity;
        }

        return activity;
    }

    private IdentityAccumulator GetIdentity(string identityId)
    {
        if (!_identities.TryGetValue(identityId, out var activity))
        {
            activity = new IdentityAccumulator(identityId);
            _identities[identityId] = activity;
        }

        return activity;
    }

    private PolicyAccumulator GetPolicy(string policyId)
    {
        if (!_policies.TryGetValue(policyId, out var activity))
        {
            activity = new PolicyAccumulator(policyId);
            _policies[policyId] = activity;
        }

        return activity;
    }

    private sealed class CapabilityAccumulator
    {
        private long _totalDurationMs;

        public CapabilityAccumulator(string capabilityId)
        {
            CapabilityId = capabilityId;
        }

        public string CapabilityId { get; }
        public long TotalRequests { get; private set; }
        public long AllowedCount { get; private set; }
        public long DeniedCount { get; private set; }
        public long PendingApprovalCount { get; private set; }
        public long GovernedEvaluationCount { get; private set; }
        public DateTimeOffset? LastUsedUtc { get; private set; }

        public void Record(AuditEvent decisionEvent)
        {
            TotalRequests++;
            _totalDurationMs += decisionEvent.EvaluationDurationMs;
            LastUsedUtc = Latest(LastUsedUtc, decisionEvent.TimestampUtc);

            if (!string.IsNullOrWhiteSpace(decisionEvent.GovernanceWindowName))
            {
                GovernedEvaluationCount++;
            }

            if (decisionEvent.Decision == DecisionType.Allow)
            {
                AllowedCount++;
            }
            else if (decisionEvent.Decision == DecisionType.Deny)
            {
                DeniedCount++;
            }
            else if (decisionEvent.Decision == DecisionType.RequireApproval)
            {
                PendingApprovalCount++;
            }
        }

        public CapabilityActivity ToActivity()
        {
            return new CapabilityActivity
            {
                CapabilityId = CapabilityId,
                TotalRequests = TotalRequests,
                AllowedCount = AllowedCount,
                DeniedCount = DeniedCount,
                PendingApprovalCount = PendingApprovalCount,
                GovernedEvaluationCount = GovernedEvaluationCount,
                LastUsedUtc = LastUsedUtc,
                AverageEvaluationDurationMs = TotalRequests == 0
                    ? 0
                    : (double)_totalDurationMs / TotalRequests
            };
        }
    }

    private sealed class IdentityAccumulator
    {
        private readonly HashSet<string> _distinctCapabilities =
            new(StringComparer.OrdinalIgnoreCase);

        public IdentityAccumulator(string identityId)
        {
            IdentityId = identityId;
        }

        public string IdentityId { get; }
        public long TotalRequests { get; private set; }
        public long DeniedCount { get; private set; }
        public long PendingApprovalCount { get; private set; }
        public DateTimeOffset? LastUsedUtc { get; private set; }

        public void Record(AuditEvent decisionEvent)
        {
            TotalRequests++;
            _distinctCapabilities.Add(decisionEvent.CapabilityId);
            LastUsedUtc = Latest(LastUsedUtc, decisionEvent.TimestampUtc);

            if (decisionEvent.Decision == DecisionType.Deny)
            {
                DeniedCount++;
            }
            else if (decisionEvent.Decision == DecisionType.RequireApproval)
            {
                PendingApprovalCount++;
            }
        }

        public IdentityActivity ToActivity()
        {
            return new IdentityActivity
            {
                IdentityId = IdentityId,
                TotalRequests = TotalRequests,
                DistinctCapabilitiesUsed = _distinctCapabilities
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DeniedCount = DeniedCount,
                PendingApprovalCount = PendingApprovalCount,
                LastUsedUtc = LastUsedUtc
            };
        }
    }

    private sealed class PolicyAccumulator
    {
        public PolicyAccumulator(string policyId)
        {
            PolicyId = policyId;
        }

        public string PolicyId { get; }
        public long MatchCount { get; private set; }
        public DateTimeOffset? LastMatchedUtc { get; private set; }

        public void Record(AuditEvent decisionEvent)
        {
            MatchCount++;
            LastMatchedUtc = Latest(LastMatchedUtc, decisionEvent.TimestampUtc);
        }

        public PolicyActivity ToActivity()
        {
            return new PolicyActivity
            {
                PolicyId = PolicyId,
                MatchCount = MatchCount,
                LastMatchedUtc = LastMatchedUtc
            };
        }
    }

    private static DateTimeOffset Latest(
        DateTimeOffset? current,
        DateTimeOffset candidate)
    {
        return current.HasValue && current.Value > candidate
            ? current.Value
            : candidate;
    }
}
