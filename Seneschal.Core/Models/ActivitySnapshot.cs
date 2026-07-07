namespace Seneschal.Core.Models;

public sealed record ActivitySnapshot
{
    public IReadOnlyCollection<CapabilityActivity> Capabilities { get; init; }
        = [];
    public IReadOnlyCollection<IdentityActivity> Identities { get; init; }
        = [];
    public IReadOnlyCollection<PolicyActivity> Policies { get; init; }
        = [];
}
