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
    private readonly OperatorGovernanceContextService _governanceContext;
    private readonly IdentityExposureAnalysisService _exposureAnalysis;
    private readonly IdentityExposureFindingService _findingService;
    private readonly IdentityExposureRecommendationService _recommendationService;
    private readonly ProposedGovernanceChangeCandidateService _candidateService;

    public IdentityActivityModel(
        IInvestigationActivityReader investigationActivity,
        IdentityLoader identityLoader,
        OperatorGovernanceContextService governanceContext,
        IdentityExposureAnalysisService exposureAnalysis,
        IdentityExposureFindingService findingService,
        IdentityExposureRecommendationService recommendationService,
        ProposedGovernanceChangeCandidateService candidateService)
    {
        _investigationActivity = investigationActivity;
        _identityLoader = identityLoader;
        _governanceContext = governanceContext;
        _exposureAnalysis = exposureAnalysis;
        _findingService = findingService;
        _recommendationService = recommendationService;
        _candidateService = candidateService;
    }

    public string? IdentityId { get; private set; }
    public IReadOnlyCollection<IdentityActivity> Identities { get; private set; }
        = [];
    public IdentityActivity? SelectedIdentity { get; private set; }
    public IdentityDefinition? SelectedIdentityDefinition { get; private set; }
    public IReadOnlyCollection<Seneschal.Core.Models.AuditEvent> RecentEvidence
        { get; private set; } = [];
    public IReadOnlyCollection<string> Environments { get; private set; } = [];
    public IReadOnlyCollection<ConfiguredCapabilityContext> ConfiguredCapabilities
        { get; private set; } = [];
    public IdentityExposureAnalysis? Exposure { get; private set; }
    public IReadOnlyCollection<IdentityExposureFinding> Findings { get; private set; } = [];
    public IReadOnlyCollection<IdentityExposureRecommendation> Recommendations { get; private set; } = [];
    public IReadOnlyDictionary<string, ProposedGovernanceChangeCandidateResult> ProposalCandidates { get; private set; } = new Dictionary<string, ProposedGovernanceChangeCandidateResult>();
    public int ObservationDays { get; private set; } = IdentityExposureAnalysisService.DefaultObservationDays;
    public string? ExposureStateFilter { get; private set; }
    public string? ExposureRiskFilter { get; private set; }
    public string? ExposureTechnologyFilter { get; private set; }
    public static string CandidateKey(IdentityExposureRecommendation item) => $"{item.RecommendationType}|{item.CapabilityId}";
    public bool IdentityWasRequested => !string.IsNullOrWhiteSpace(IdentityId);
    public bool HasActivity => Identities.Count > 0;

    public async Task OnGetAsync(
        string? identityId,
        int? days,
        string? exposureState,
        string? exposureRisk,
        string? exposureTechnology,
        CancellationToken cancellationToken)
    {
        IdentityId = identityId;
        ObservationDays = Math.Clamp(days ?? IdentityExposureAnalysisService.DefaultObservationDays, 1, 365);
        ExposureStateFilter = exposureState;
        ExposureRiskFilter = exposureRisk;
        ExposureTechnologyFilter = exposureTechnology;
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
            ConfiguredCapabilities = await _governanceContext
                .GetIdentityCapabilitiesAsync(identityId, cancellationToken);
            var windowEnd = DateTimeOffset.UtcNow;
            Exposure = await _exposureAnalysis.AnalyzeAsync(new IdentityExposureQuery(
                identityId, windowEnd.AddDays(-ObservationDays), windowEnd,
                exposureState, exposureRisk, exposureTechnology), cancellationToken);
            Findings = _findingService.Generate(Exposure);
            Recommendations = _recommendationService.Generate(Findings);
            ProposalCandidates = Recommendations.ToDictionary(CandidateKey, item => _candidateService.Offer(item), StringComparer.OrdinalIgnoreCase);
        }
    }
}
