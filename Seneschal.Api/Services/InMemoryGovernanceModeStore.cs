using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Services;

public sealed class InMemoryGovernanceModeStore : IGovernanceModeStore
{
    private readonly RuntimeSettings _runtimeSettings;
    private readonly object _sync = new();
    private readonly IAuditEventStore? _auditEventStore;
    private RuntimeGovernanceState _state;

    public InMemoryGovernanceModeStore(
        RuntimeSettings runtimeSettings,
        IAuditEventStore? auditEventStore = null)
    {
        _runtimeSettings = runtimeSettings;
        _auditEventStore = auditEventStore;
        _state = new RuntimeGovernanceState { Mode = runtimeSettings.Mode };
    }

    public EnforcementMode GetMode()
    {
        lock (_sync)
        {
            return _state.Mode;
        }
    }

    public RuntimeGovernanceState GetState()
    {
        lock (_sync)
        {
            return _state;
        }
    }

    public async Task<RuntimeGovernanceState> SetModeAsync(
        EnforcementMode mode, long expectedVersion, string? actor = null,
        string? reason = null, string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        RuntimeGovernanceState previous;
        RuntimeGovernanceState updated;
        lock (_sync)
        {
            previous = _state;
            if (previous.Mode == mode)
            {
                return previous;
            }
            if (previous.Version != expectedVersion)
                throw new OperationalControlConcurrencyException(
                    "runtime governance mode", expectedVersion, previous.Version);
            updated = previous with
            {
                Mode = mode,
                Version = previous.Version + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = NormalizeActor(actor),
                Reason = NormalizeReason(reason, "Runtime governance mode changed.")
            };
            _state = updated;
            _runtimeSettings.Mode = mode;
        }
        try
        {
            if (_auditEventStore is not null)
            {
                await _auditEventStore.WriteAsync(
                    CreateEvidence(previous, updated, operationId),
                    cancellationToken);
            }
            return updated;
        }
        catch
        {
            lock (_sync)
            {
                if (_state == updated)
                {
                    _state = previous;
                    _runtimeSettings.Mode = previous.Mode;
                }
            }
            throw;
        }
    }

    public void SetMode(EnforcementMode mode) =>
        SetModeAsync(mode, GetState().Version).GetAwaiter().GetResult();

    internal static string NormalizeActor(string? actor) =>
        string.IsNullOrWhiteSpace(actor)
            ? "operator identity unavailable"
            : actor.Trim();
    internal static string NormalizeReason(string? reason, string fallback) =>
        string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
    internal static AuditEvent CreateEvidence(RuntimeGovernanceState previous,
        RuntimeGovernanceState updated, string? operationId) => new()
    {
        Id = string.IsNullOrWhiteSpace(operationId)
            ? Guid.NewGuid().ToString("N")
            : $"runtime-mode-{operationId.Trim()}",
        RequestId = operationId?.Trim() ?? string.Empty,
        TimestampUtc = updated.UpdatedAt!.Value,
        IdentityId = updated.UpdatedBy!,
        CapabilityId = "seneschal.runtime-governance.manage",
        Decision = DecisionType.Allow,
        PolicyDecision = DecisionType.Allow,
        EnforcementMode = updated.Mode,
        EffectiveAction = "runtime_mode_changed",
        Reason = updated.Reason!,
        RequestContext = new()
        {
            ["previousMode"] = previous.Mode.ToString(),
            ["resultingMode"] = updated.Mode.ToString(),
            ["previousVersion"] = previous.Version.ToString(),
            ["resultingVersion"] = updated.Version.ToString()
        }
    };
}
