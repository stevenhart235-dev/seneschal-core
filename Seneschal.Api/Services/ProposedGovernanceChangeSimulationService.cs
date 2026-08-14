using Seneschal.Api.Models;
using Seneschal.Core.Enums;
using ApiPolicy = Seneschal.Api.Models.Policy;

namespace Seneschal.Api.Services;

public sealed class ProposedGovernanceChangeSimulationService
{
    private readonly ProposedGovernanceChangeContractValidator _contract;
    private readonly ProposedPolicyConfigurationService _configuration;
    private readonly PolicyLoader _policies;
    private readonly CapabilityLoader _capabilities;
    private readonly CoreDecisionService _decisions;

    public ProposedGovernanceChangeSimulationService(
        ProposedGovernanceChangeContractValidator contract,
        ProposedPolicyConfigurationService configuration, PolicyLoader policies,
        CapabilityLoader capabilities, CoreDecisionService decisions)
    { _contract=contract; _configuration=configuration; _policies=policies;
      _capabilities=capabilities; _decisions=decisions; }

    public ProposedChangeSimulationOutcome Simulate(
        ProposedGovernanceChangeSimulationRequest request)
    {
        var contractErrors = _contract.Validate(request.Proposal);
        if (contractErrors.Count > 0) return Failed("invalid_proposal", contractErrors);
        var current = _decisions.CurrentConfiguration();
        if (!string.Equals(request.Proposal.BaseGovernanceConfigurationFingerprint,
            current.Fingerprint, StringComparison.Ordinal))
            return Failed("stale_proposal", ["Base configuration changed. Regenerate the proposal before simulation."]);
        var applied = _configuration.Apply(request.Proposal, _policies.GetPolicies());
        if (!applied.IsValid) return Failed("invalid_proposed_configuration", applied.Errors);
        var proposedCore = PolicyLoader.ProjectCorePolicies(applied.Policies);
        var proposedFingerprint = GovernanceConfigurationFingerprintService.Compute(
            proposedCore, current.Mode, current.GovernanceWindow);
        var proposed = new EvaluationConfiguration(proposedCore, current.Mode,
            current.GovernanceWindow, proposedFingerprint, EvaluationConfigurationKind.Proposed);
        var decisionRequest = new DecisionRequest { Identity=request.Identity,
            Capability=request.Capability, OperationId=request.OperationId,
            Context=new Dictionary<string,string>(request.Context) };
        var compared = _decisions.ComparePreview(decisionRequest, proposed);
        var staticComparison = StaticComparison(request.Proposal, _policies.GetPolicies(), applied.Policies);
        return new(true, null, [], current.Mode, current.Fingerprint, proposedFingerprint,
            compared.Timestamp, Project(compared.Current, current.Fingerprint),
            Project(compared.Proposed, proposedFingerprint),
            Differences(compared.Current, compared.Proposed), staticComparison);
    }

    private StaticGovernanceComparison StaticComparison(ProposedGovernanceChange proposal,
        IReadOnlyList<ApiPolicy> current, IReadOnlyList<ApiPolicy> proposed)
    {
        var identity = proposal.Source.Identity;
        var before = CapabilitiesFor(identity, current); var after = CapabilitiesFor(identity, proposed);
        var metadata = _capabilities.GetCapabilities().ToDictionary(item => item.Name,
            StringComparer.OrdinalIgnoreCase);
        int CountRisk(HashSet<string> ids, string risk) => ids.Count(id => metadata.TryGetValue(id,
            out var capability) && string.Equals(capability.Risk, risk, StringComparison.OrdinalIgnoreCase));
        var policy = current.Single(item => string.Equals(item.Name, proposal.Change.Policy,
            StringComparison.OrdinalIgnoreCase));
        metadata.TryGetValue(proposal.Change.Capability, out var affected);
        return new("Static governance context comparison", before.Count, after.Count,
            after.Count-before.Count, CountRisk(before,"Critical"), CountRisk(after,"Critical"),
            CountRisk(after,"Critical")-CountRisk(before,"Critical"), CountRisk(before,"High"),
            CountRisk(after,"High"), CountRisk(after,"High")-CountRisk(before,"High"),
            identity, proposal.Change.Capability, policy.Name, policy.EffectiveEnvironments,
            affected?.Technology ?? "", affected?.Risk ?? "Unknown",
            "Static relationships do not prove every runtime outcome or reduced real-world risk.");
    }
    private static HashSet<string> CapabilitiesFor(string identity, IEnumerable<ApiPolicy> policies) =>
        policies.Where(p => p.EffectiveIdentities.Contains(identity, StringComparer.OrdinalIgnoreCase))
            .SelectMany(p => p.EffectiveCapabilities).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static SimulatedGovernanceResult Project(DecisionResult result, string fingerprint) =>
        new(result.Decision, result.EffectiveAction, result.ExecutionGuidance,
            result.ExecutionGuidance is "Proceed" or "ContinueLogOnly", result.PolicyMatched,
            result.MatchedPolicies ?? [], result.Reason, result.ApprovalStatus,
            result.GovernanceWindowName, result.GovernanceWindowInfluencedResult ?? false, fingerprint);
    private static IReadOnlyCollection<SimulationDifference> Differences(DecisionResult current, DecisionResult proposed)
    {
        var values = new[] { ("Decision",current.Decision,proposed.Decision),
            ("EffectiveAction",current.EffectiveAction,proposed.EffectiveAction),
            ("ExecutionGuidance",current.ExecutionGuidance,proposed.ExecutionGuidance),
            ("WinningPolicy",current.PolicyMatched,proposed.PolicyMatched) };
        return values.Where(v => !string.Equals(v.Item2,v.Item3,StringComparison.Ordinal))
            .Select(v => new SimulationDifference(v.Item1,v.Item2,v.Item3)).ToList();
    }
    private static ProposedChangeSimulationOutcome Failed(string code, IReadOnlyCollection<string> errors) =>
        new(false, code, errors, null, null, null, null, null, null, [], null);
}

public sealed record ProposedChangeSimulationOutcome(bool IsValid, string? ErrorCode,
    IReadOnlyCollection<string> Errors, EnforcementMode? RuntimeMode,
    string? CurrentGovernanceConfigurationFingerprint,
    string? ProposedGovernanceConfigurationFingerprint, DateTimeOffset? EvaluationTimestamp,
    SimulatedGovernanceResult? Current, SimulatedGovernanceResult? Proposed,
    IReadOnlyCollection<SimulationDifference> Differences,
    StaticGovernanceComparison? StaticGovernanceContextComparison);
public sealed record SimulatedGovernanceResult(string Decision, string EffectiveAction,
    string ExecutionGuidance, bool ShouldProceed, string WinningPolicy,
    IReadOnlyCollection<string> MatchedPolicies, string Reason, string? ApprovalStatus,
    string? GovernanceWindowName, bool GovernanceWindowInfluencedResult,
    string ConfigurationFingerprint);
public sealed record SimulationDifference(string Field, string Current, string Proposed);
public sealed record StaticGovernanceComparison(string Label, int CurrentConfiguredCapabilities,
    int ProposedConfiguredCapabilities, int ConfiguredCapabilityDifference,
    int CurrentCriticalCapabilities, int ProposedCriticalCapabilities, int CriticalDifference,
    int CurrentHighCapabilities, int ProposedHighCapabilities, int HighDifference,
    string Identity, string Capability, string Policy, IReadOnlyCollection<string> Environments,
    string Technology, string CuratedCapabilityRisk, string Limitation);
