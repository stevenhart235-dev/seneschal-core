using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class IdentityActivityModel : PageModel
{
    private readonly IInvestigationActivityReader _investigationActivity;
    private readonly IdentityLoader _identityLoader;

    public IdentityActivityModel(
        IInvestigationActivityReader investigationActivity,
        IdentityLoader identityLoader)
    {
        _investigationActivity = investigationActivity;
        _identityLoader = identityLoader;
    }

    public string? IdentityId { get; private set; }
    public IReadOnlyCollection<IdentityActivity> Identities { get; private set; }
        = [];
    public IdentityActivity? SelectedIdentity { get; private set; }
    public IdentityDefinition? SelectedIdentityDefinition { get; private set; }
    public IReadOnlyCollection<Seneschal.Core.Models.AuditEvent> RecentEvidence
        { get; private set; } = [];
    public IReadOnlyCollection<string> Environments { get; private set; } = [];
    public bool IdentityWasRequested => !string.IsNullOrWhiteSpace(IdentityId);
    public bool HasActivity => Identities.Count > 0;

    public async Task OnGetAsync(
        string? identityId,
        CancellationToken cancellationToken)
    {
        IdentityId = identityId;
        var snapshot = await _investigationActivity.GetSnapshotAsync(
            cancellationToken);

        Identities = snapshot.Identities
            .OrderByDescending(identity => identity.TotalRequests)
            .ThenByDescending(identity => identity.DeniedCount)
            .ThenByDescending(identity => identity.PendingApprovalCount)
            .ThenBy(identity => identity.IdentityId)
            .ToList();

        if (!string.IsNullOrWhiteSpace(identityId))
        {
            var investigation = await _investigationActivity.GetIdentityAsync(
                identityId, 100, cancellationToken);
            SelectedIdentity = investigation?.Activity;
            RecentEvidence = investigation?.RecentEvidence ?? [];
            Environments = investigation?.Environments ?? [];
            SelectedIdentityDefinition = _identityLoader.GetIdentities()
                .FirstOrDefault(identity => string.Equals(
                    identity.Name,
                    identityId,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
