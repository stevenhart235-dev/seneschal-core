using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryGovernanceWindowStore : IGovernanceWindowStore
{
    public const string ProductionFreezeName = "Production Freeze";
    public const string ProductionFreezeReason = "Weekend production freeze.";

    private readonly object _gate = new();
    private bool _enabled;
    private GovernanceWindowMode _mode = GovernanceWindowMode.Observe;
    private long _version;
    private DateTimeOffset? _updatedAt;
    private string? _updatedBy;
    private readonly IAuditEventStore? _auditEventStore;

    public InMemoryGovernanceWindowStore(IAuditEventStore? auditEventStore = null) =>
        _auditEventStore = auditEventStore;

    public GovernanceWindow GetWindow()
    {
        lock (_gate)
        {
            return new GovernanceWindow
            {
                Name = ProductionFreezeName,
                Description = "Manually control high-risk production changes during a production freeze.",
                Enabled = _enabled,
                Mode = _mode,
                AffectedCapabilities =
                [
                    "production.deployment.execute",
                    "infrastructure.production.apply",
                    "infrastructure.production.destroy"
                ],
                Reason = ProductionFreezeReason,
                Version = _version,
                UpdatedAt = _updatedAt,
                UpdatedBy = _updatedBy
            };
        }
    }

    public async Task<GovernanceWindow> SetStateAsync(
        bool enabled, GovernanceWindowMode mode, long expectedVersion,
        string? actor = null, string? reason = null, string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        GovernanceWindow previous;
        GovernanceWindow updated;
        lock (_gate)
        {
            previous = GetWindowUnsafe();
            if (previous.Enabled == enabled && previous.Mode == mode)
            {
                return previous;
            }
            if (_version != expectedVersion)
                throw new OperationalControlConcurrencyException(
                    "Governance Window", expectedVersion, _version);
            _enabled = enabled;
            _mode = mode;
            _version++;
            _updatedAt = DateTimeOffset.UtcNow;
            _updatedBy = string.IsNullOrWhiteSpace(actor)
                ? "operator identity unavailable" : actor.Trim();
            updated = GetWindowUnsafe();
        }
        try
        {
            if (_auditEventStore is not null)
                await _auditEventStore.WriteAsync(CreateEvidence(previous, updated,
                    string.IsNullOrWhiteSpace(reason)
                        ? "Governance Window state changed."
                        : reason.Trim(),
                    operationId), cancellationToken);
            return updated;
        }
        catch
        {
            lock (_gate)
            {
                if (_version == updated.Version)
                {
                    _enabled = previous.Enabled;
                    _mode = previous.Mode;
                    _version = previous.Version;
                    _updatedAt = previous.UpdatedAt;
                    _updatedBy = previous.UpdatedBy;
                }
            }
            throw;
        }
    }

    public void SetState(bool enabled, GovernanceWindowMode mode) =>
        SetStateAsync(enabled, mode, GetWindow().Version).GetAwaiter().GetResult();

    private GovernanceWindow GetWindowUnsafe() => new()
    {
        Name = ProductionFreezeName,
        Description = "Manually control high-risk production changes during a production freeze.",
        Enabled = _enabled,
        Mode = _mode,
        AffectedCapabilities =
        [
            "production.deployment.execute",
            "infrastructure.production.apply",
            "infrastructure.production.destroy"
        ],
        Reason = ProductionFreezeReason,
        Version = _version,
        UpdatedAt = _updatedAt,
        UpdatedBy = _updatedBy
    };

    public static AuditEvent CreateEvidence(GovernanceWindow previous,
        GovernanceWindow updated, string reason, string? operationId) => new()
    {
        Id = string.IsNullOrWhiteSpace(operationId)
            ? Guid.NewGuid().ToString("N")
            : $"governance-window-{operationId.Trim()}",
        RequestId = operationId?.Trim() ?? string.Empty,
        TimestampUtc = updated.UpdatedAt!.Value,
        IdentityId = updated.UpdatedBy!,
        CapabilityId = "seneschal.governance-window.manage",
        Decision = DecisionType.Allow,
        PolicyDecision = DecisionType.Allow,
        EnforcementMode = EnforcementMode.LogOnly,
        EffectiveAction = updated.Enabled
            ? "governance_window_enabled"
            : "governance_window_disabled",
        Reason = reason,
        GovernanceWindowName = updated.Name,
        GovernanceWindowMode = updated.Mode.ToString(),
        GovernanceWindowReason = updated.Reason,
        RequestContext = new()
        {
            ["previousEnabled"] = previous.Enabled.ToString(),
            ["resultingEnabled"] = updated.Enabled.ToString(),
            ["previousMode"] = previous.Mode.ToString(),
            ["resultingMode"] = updated.Mode.ToString(),
            ["previousVersion"] = previous.Version.ToString(),
            ["resultingVersion"] = updated.Version.ToString()
        }
    };
}
