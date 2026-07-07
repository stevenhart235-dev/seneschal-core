namespace Seneschal.Api.Models;

public sealed class AuditEventFilter
{
    public string? IdentityId { get; init; }
    public string? CapabilityId { get; init; }
    public string? Decision { get; init; }
    public string? EnforcementMode { get; init; }
    public string? Environment { get; init; }
    public string? MatchedPolicy { get; init; }
}
