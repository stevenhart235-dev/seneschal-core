using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Api.Mappers;
using CorePolicy = Seneschal.Core.Models.Policy;

namespace Seneschal.Api.Services;

public sealed class NorthwindHistorySeedOptions
{
    public bool Enabled { get; init; }
    public string SeedVersion { get; init; } = "s14-c6-v1";
}

public interface INorthwindHistoryClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemNorthwindHistoryClock : INorthwindHistoryClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class NorthwindHistorySeeder
{
    public const int PlannedRecordCount = 400;

    private readonly IAuditEventStore _auditStore;
    private readonly IActivityStore _activityStore;
    private readonly IdentityLoader _identityLoader;
    private readonly CapabilityLoader _capabilityLoader;
    private readonly PolicyLoader _policyLoader;
    private readonly NorthwindHistorySeedOptions _options;
    private readonly INorthwindHistoryClock _clock;
    private readonly ILogger<NorthwindHistorySeeder> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _seeded;

    public NorthwindHistorySeeder(
        IAuditEventStore auditStore,
        IActivityStore activityStore,
        IdentityLoader identityLoader,
        CapabilityLoader capabilityLoader,
        PolicyLoader policyLoader,
        NorthwindHistorySeedOptions options,
        INorthwindHistoryClock clock,
        ILogger<NorthwindHistorySeeder> logger)
    {
        _auditStore = auditStore;
        _activityStore = activityStore;
        _identityLoader = identityLoader;
        _capabilityLoader = capabilityLoader;
        _policyLoader = policyLoader;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_seeded)
            {
                return 0;
            }

            var anchor = _clock.UtcNow.ToUniversalTime();
            var events = Generate(anchor);
            Validate(events, anchor);

            var added = 0;
            foreach (var auditEvent in events.OrderBy(item => item.TimestampUtc))
            {
                if (await _auditStore.GetByIdAsync(auditEvent.Id, cancellationToken)
                    is not null)
                {
                    continue;
                }

                await _auditStore.WriteAsync(auditEvent, cancellationToken);
                await _activityStore.RecordAsync(auditEvent, cancellationToken);
                added++;
            }

            _seeded = true;
            _logger.LogInformation(
                "Northwind demo history seed {SeedVersion} loaded {Added} records at anchor {Anchor:u}.",
                _options.SeedVersion,
                added,
                anchor);
            return added;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Northwind demo history seed {SeedVersion} failed.",
                _options.SeedVersion);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<AuditEvent> Generate(DateTimeOffset anchor)
    {
        anchor = anchor.ToUniversalTime();
        var events = new List<AuditEvent>(PlannedRecordCount);

        for (var sequence = 0; sequence < PlannedRecordCount; sequence++)
        {
            var decision = DecisionFor(sequence);
            var timestamp = TimestampFor(anchor, sequence);
            var workload = SelectWorkload(decision, timestamp, sequence);
            var mode = decision is DecisionType.Deny or DecisionType.RequireApproval
                ? EnforcementMode.Enforce
                : sequence % 4 == 0
                    ? EnforcementMode.LogOnly
                    : EnforcementMode.Enforce;
            if (decision == DecisionType.LogOnly)
            {
                mode = EnforcementMode.LogOnly;
            }

            var policy = new CorePolicy
            {
                Id = workload.PolicyId,
                Name = workload.PolicyId,
                Effect = decision,
                Reason = workload.Reason,
                Conditions = new Dictionary<string, string>
                {
                    ["identity.id"] = workload.Identity,
                    ["capability.id"] = workload.Capability,
                    ["resource.environment"] = workload.Environment
                }
            };
            var conditions = policy.Conditions
                .Select(condition => new EvaluationStep
                {
                    Property = condition.Key,
                    Expected = condition.Value,
                    Actual = condition.Value,
                    Matched = true
                })
                .ToList();

            events.Add(new AuditEvent
            {
                Id = $"northwind-{_options.SeedVersion}-{workload.Key}-{sequence:D4}",
                RequestId = $"northwind-request-{_options.SeedVersion}-{sequence:D4}",
                TimestampUtc = timestamp,
                IdentityId = workload.Identity,
                CapabilityId = workload.Capability,
                ResourceId = workload.Resource,
                Environment = workload.Environment,
                Decision = decision,
                EnforcementMode = mode,
                MatchedPolicies = [workload.PolicyId],
                Obligations = workload.Obligations.ToList(),
                Reason = workload.Reason,
                EvaluationDurationMs = 7 + ((sequence * 17 + workload.Key.Length) % 89),
                PolicyDecision = decision,
                PolicyReason = workload.Reason,
                PolicyEvaluations =
                [
                    new PolicyEvaluation
                    {
                        Policy = policy,
                        Matched = true,
                        Reasons = [workload.Reason],
                        Obligations = workload.Obligations.ToList(),
                        Conditions = conditions
                    }
                ],
                ExecutionGuidance = ExecutionGuidanceFor(decision, mode),
                CallerMessage = CallerMessageFor(decision, mode),
                RetryGuidance = decision == DecisionType.RequireApproval
                    ? "Pause and retry the governed operation after an approval is available."
                    : null
            });
        }

        return events;
    }

    private void Validate(
        IReadOnlyCollection<AuditEvent> events,
        DateTimeOffset anchor)
    {
        var identities = _identityLoader.GetIdentities()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var capabilities = _capabilityLoader.GetCapabilities()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var policies = _policyLoader.GetPolicies()
            .ToDictionary(
                item => item.Name,
                item => DecisionTypeMapper.ToCore(item.Decision),
                StringComparer.OrdinalIgnoreCase);
        policies["default-deny"] = DecisionType.Deny;

        if (events.Count is < 300 or > 500)
        {
            throw new InvalidOperationException(
                $"Northwind history produced {events.Count} records; expected 300–500.");
        }

        if (events.Select(item => item.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != events.Count)
        {
            throw new InvalidOperationException(
                "Northwind history contains duplicate audit identifiers.");
        }

        var oldest = events.Min(item => item.TimestampUtc);
        var newest = events.Max(item => item.TimestampUtc);
        if (newest > anchor || newest - oldest < TimeSpan.FromDays(14))
        {
            throw new InvalidOperationException(
                "Northwind history timestamps do not form a valid 14-day UTC range.");
        }

        var unknownIdentity = events.FirstOrDefault(item =>
            !identities.Contains(item.IdentityId));
        if (unknownIdentity is not null)
        {
            throw new InvalidOperationException(
                $"Northwind history references unknown identity '{unknownIdentity.IdentityId}'.");
        }

        var unknownCapability = events.FirstOrDefault(item =>
            !capabilities.Contains(item.CapabilityId));
        if (unknownCapability is not null)
        {
            throw new InvalidOperationException(
                $"Northwind history references unknown capability '{unknownCapability.CapabilityId}'.");
        }

        var unknownPolicy = events.FirstOrDefault(item =>
            item.MatchedPolicies.Any(policy => !policies.ContainsKey(policy)));
        if (unknownPolicy is not null)
        {
            throw new InvalidOperationException(
                $"Northwind history references an unknown matched policy on '{unknownPolicy.Id}'.");
        }

        var contradictoryPolicy = events.FirstOrDefault(item =>
            item.MatchedPolicies.Any(policy => policies[policy] != item.Decision));
        if (contradictoryPolicy is not null)
        {
            throw new InvalidOperationException(
                $"Northwind history decision contradicts matched policy on '{contradictoryPolicy.Id}'.");
        }
    }

    private static DecisionType DecisionFor(int sequence)
    {
        return (sequence % 100) switch
        {
            < 80 => DecisionType.Allow,
            < 88 => DecisionType.Deny,
            < 95 => DecisionType.RequireApproval,
            _ => DecisionType.LogOnly
        };
    }

    private static DateTimeOffset TimestampFor(
        DateTimeOffset anchor,
        int sequence)
    {
        if (sequence == 0)
        {
            return anchor.AddSeconds(-5);
        }

        if (sequence == PlannedRecordCount - 1)
        {
            return anchor.AddDays(-14).AddMinutes(-6);
        }

        var totalMinutes = TimeSpan.FromDays(14).TotalMinutes;
        var minutesAgo = 3 + (sequence * totalMinutes /
            (PlannedRecordCount - 1));
        var timestamp = anchor.AddMinutes(-minutesAgo);

        // Concentrate alternate weekend records into the preceding Friday
        // while retaining some overnight and weekend automation.
        if (timestamp.DayOfWeek == DayOfWeek.Saturday && sequence % 2 == 0)
        {
            timestamp = timestamp.AddDays(-1);
        }
        else if (timestamp.DayOfWeek == DayOfWeek.Sunday && sequence % 2 == 0)
        {
            timestamp = timestamp.AddDays(-2);
        }

        return timestamp;
    }

    private static SeedWorkload SelectWorkload(
        DecisionType decision,
        DateTimeOffset timestamp,
        int sequence)
    {
        var workloads = decision switch
        {
            DecisionType.Allow when timestamp.DayOfWeek is
                DayOfWeek.Saturday or DayOfWeek.Sunday => WeekendAllowWorkloads,
            DecisionType.Allow => AllowWorkloads,
            DecisionType.Deny => DenyWorkloads,
            DecisionType.RequireApproval => ApprovalWorkloads,
            _ => ObserveWorkloads
        };

        return workloads[sequence % workloads.Count];
    }

    private static string ExecutionGuidanceFor(
        DecisionType decision,
        EnforcementMode mode) => (decision, mode) switch
    {
        (DecisionType.Allow, _) => "Proceed",
        (DecisionType.Deny, EnforcementMode.LogOnly) => "ContinueLogOnly",
        (DecisionType.Deny, _) => "Block",
        (DecisionType.RequireApproval, EnforcementMode.LogOnly) => "ContinueLogOnly",
        (DecisionType.RequireApproval, _) => "Pause",
        (DecisionType.LogOnly, _) => "ContinueLogOnly",
        _ => "Block"
    };

    private static string? CallerMessageFor(
        DecisionType decision,
        EnforcementMode mode) => (decision, mode) switch
    {
        (DecisionType.Deny, EnforcementMode.Enforce) =>
            "Policy blocked this historical operation.",
        (DecisionType.RequireApproval, EnforcementMode.Enforce) =>
            "Approval was required before this historical operation could continue.",
        (DecisionType.LogOnly, _) =>
            "The operation was observed and recorded without enforcement.",
        _ => null
    };

    private static readonly IReadOnlyList<SeedWorkload> AllowWorkloads =
    [
        Allow("payments", "payments-api", "payments.refund.create", "payment-ledger",
            "Refund worker can create production refunds", "Approved payment service operation completed."),
        Allow("checkout-write", "checkout-api", "postgres.production.write", "checkout-orders",
            "Deployment worker can deploy to production", "Checkout service completed an expected production write."),
        Allow("fraud-model", "fraud-detection-worker", "openai.model.invoke", "fraud-risk-model",
            "Deployment worker can deploy to production", "Fraud scoring model invocation completed under monitoring."),
        Allow("notification-mail", "notification-service", "m365.mail.send", "customer-notifications",
            "Deployment worker can deploy to production", "Notification service delivered an approved operational message."),
        Allow("gateway-config", "api-gateway", "azure.appconfig.write", "gateway-production",
            "Platform agent can apply production infrastructure", "API gateway applied an approved configuration update."),
        Allow("github-dispatch", "github-release-worker", "github.workflow.dispatch", "release-pipeline",
            "GitHub Actions can deploy checkout API to production", "Release Engineering dispatched an approved workflow."),
        Allow("terraform-plan", "terraform-cloud", "terraform.plan", "northwind-platform",
            "Terraform can apply production infrastructure", "Terraform generated a reviewed infrastructure plan."),
        Allow("argocd-scale", "argocd-production", "kubernetes.deployment.scale", "payments-namespace",
            "Deployment worker can deploy to production", "GitOps automation applied an expected workload scale adjustment."),
        Allow("platform-apply", "platform-agent", "terraform.apply.development", "platform-development",
            "Platform agent can apply production infrastructure", "Platform automation applied a development infrastructure change.",
            "retain-change-evidence"),
        Allow("support-model", "support-ai", "openai.model.invoke", "support-assistant",
            "Deployment worker can deploy to production", "Support assistant completed an approved model invocation."),
        Allow("customer-read", "customer-portal", "postgres.production.read", "customer-profile",
            "Refund worker can create production refunds", "Customer portal completed an expected scoped data read."),
        Allow("security-alert", "security-automation", "slack.security-alert.send", "security-operations",
            "Platform agent can apply production infrastructure", "Security automation delivered an operational alert."),
        Allow("incident-channel", "incident-response-bot", "slack.incident-channel.create", "security-operations",
            "Platform agent can apply production infrastructure", "Incident automation prepared an operational collaboration channel."),
        Allow("sharepoint-read", "support-ai", "m365.sharepoint.document.read", "support-knowledge",
            "Deployment worker can deploy to production", "Support assistant read approved operational knowledge."),
        Allow("backup-create-weekday", "backup-automation", "postgres.backup.create", "northwind-production",
            "Terraform can apply production infrastructure", "Scheduled production backup completed.")
    ];

    private static readonly IReadOnlyList<SeedWorkload> WeekendAllowWorkloads =
    [
        Allow("backup-create", "backup-automation", "postgres.backup.create", "northwind-production",
            "Terraform can apply production infrastructure", "Scheduled production backup completed."),
        Allow("argocd-reconcile", "argocd-production", "kubernetes.deployment.restart", "checkout-namespace",
            "Deployment worker can deploy to production", "GitOps automation completed a controlled workload restart."),
        Allow("overnight-alert", "security-automation", "slack.security-alert.send", "security-operations",
            "Platform agent can apply production infrastructure", "Overnight security monitoring delivered an operational alert."),
        Allow("fraud-overnight", "fraud-detection-worker", "openai.model.invoke", "fraud-risk-model",
            "Deployment worker can deploy to production", "Overnight fraud scoring completed under monitoring.")
    ];

    private static readonly IReadOnlyList<SeedWorkload> DenyWorkloads =
    [
        Deny("ai-secret", "support-ai", "openai.production-secret.read", "production-ai-secrets",
            "Northwind privileged AI operations", "AI identities cannot read production infrastructure secrets."),
        Deny("terraform-destroy", "terraform-cloud", "infrastructure.production.destroy", "northwind-platform",
            "Platform agent cannot destroy production infrastructure", "Production infrastructure destruction was blocked."),
        Deny("cluster-exec", "Developer", "kubernetes.pod.exec", "payments-namespace",
            "Developers cannot delete production databases", "Direct developer access to a production workload was blocked."),
        Deny("fraud-secret", "fraud-analysis-ai", "azure.keyvault.secret.read", "fraud-production-vault",
            "Northwind privileged AI operations", "AI identities cannot read production infrastructure secrets.")
    ];

    private static readonly IReadOnlyList<SeedWorkload> ApprovalWorkloads =
    [
        Approval("release-approval", "github-release-worker", "production.deployment.execute", "release-pipeline",
            "Northwind production freeze", "Production deployment requires approval while release controls are active."),
        Approval("argocd-approval", "argocd-production", "production.deployment.execute", "production-cluster",
            "Northwind production freeze", "Production reconciliation requires approval during controlled release periods."),
        Approval("support-secret", "SupportAgent", "azure.keyvault.secret.read", "support-production-vault",
            "Support secret reads require approval", "Reading production secrets requires approval.")
    ];

    private static readonly IReadOnlyList<SeedWorkload> ObserveWorkloads =
    [
        Observe("support-observe", "support-ai", "openai.customer-data.process", "support-cases",
            "Northwind customer data observation", "Approved AI customer-data processing is recorded for governance review."),
        Observe("fraud-observe", "fraud-analysis-ai", "openai.model.invoke", "fraud-analysis",
            "Northwind customer data observation", "Approved AI model activity is recorded for governance review.")
    ];

    private static SeedWorkload Allow(
        string key,
        string identity,
        string capability,
        string resource,
        string policy,
        string reason,
        params string[] obligations) =>
        new(key, identity, capability, resource, "production", policy, reason, obligations);

    private static SeedWorkload Deny(
        string key,
        string identity,
        string capability,
        string resource,
        string policy,
        string reason) =>
        new(key, identity, capability, resource, "production", policy, reason, []);

    private static SeedWorkload Approval(
        string key,
        string identity,
        string capability,
        string resource,
        string policy,
        string reason) =>
        new(key, identity, capability, resource, "production", policy, reason,
            ["human-review-required"]);

    private static SeedWorkload Observe(
        string key,
        string identity,
        string capability,
        string resource,
        string policy,
        string reason) =>
        new(key, identity, capability, resource, "production", policy, reason,
            ["retain-governance-evidence"]);

    private sealed record SeedWorkload(
        string Key,
        string Identity,
        string Capability,
        string Resource,
        string Environment,
        string PolicyId,
        string Reason,
        IReadOnlyList<string> Obligations);
}
