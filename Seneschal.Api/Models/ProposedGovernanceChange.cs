using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Schema;

namespace Seneschal.Api.Models;

public sealed record ProposedGovernanceChange
{
    public string ContractVersion { get; init; } = "";
    public int Revision { get; init; }
    public string ProposalId { get; init; } = "";
    public string BaseGovernanceConfigurationFingerprint { get; init; } = "";
    public ProposedGovernanceChangeSource Source { get; init; } = new();
    public ProposedGovernanceChangeOperation Change { get; init; } = new();
}

public sealed record ProposedGovernanceChangeSource
{
    public string RecommendationType { get; init; } = "";
    public string FindingType { get; init; } = "";
    public string Identity { get; init; } = "";
    public string Capability { get; init; } = "";
    public ProposedObservationWindow ObservationWindow { get; init; } = new();
    public string EvidenceCoverage { get; init; } = "";
}

public sealed record ProposedObservationWindow
{
    public DateTimeOffset StartUtc { get; init; }
    public DateTimeOffset EndUtc { get; init; }
}

public sealed record ProposedGovernanceChangeOperation
{
    public string Operation { get; init; } = "";
    public string Policy { get; init; } = "";
    public string Capability { get; init; } = "";
}

public sealed record ProposedGovernanceChangeSimulationRequest
{
    public ProposedGovernanceChange Proposal { get; init; } = new();
    public string Identity { get; init; } = "";
    public string Capability { get; init; } = "";
    public string? OperationId { get; init; }
    public Dictionary<string, string> Context { get; init; } = [];
}
