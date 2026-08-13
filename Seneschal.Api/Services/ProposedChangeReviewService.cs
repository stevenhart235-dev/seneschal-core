using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public sealed class ProposedChangeReviewService
{
    private readonly IdentityExposureAnalysisService _analysis;
    private readonly IdentityExposureFindingService _findings;
    private readonly IdentityExposureRecommendationService _recommendations;
    private readonly ProposedGovernanceChangeCandidateService _candidates;
    private readonly ProposedGovernanceChangeSimulationService _simulation;

    public ProposedChangeReviewService(IdentityExposureAnalysisService analysis,
        IdentityExposureFindingService findings,
        IdentityExposureRecommendationService recommendations,
        ProposedGovernanceChangeCandidateService candidates,
        ProposedGovernanceChangeSimulationService simulation)
    { _analysis=analysis; _findings=findings; _recommendations=recommendations;
      _candidates=candidates; _simulation=simulation; }

    public async Task<ProposedChangeReview> BuildAsync(string identityId,
        string capabilityId, int days, CancellationToken cancellationToken=default)
    {
        var end=DateTimeOffset.UtcNow; var exposure=await _analysis.AnalyzeAsync(
            new(identityId,end.AddDays(-Math.Clamp(days,1,365)),end),cancellationToken);
        var item=exposure.AllItems.FirstOrDefault(x=>string.Equals(x.CapabilityId,
            capabilityId,StringComparison.OrdinalIgnoreCase));
        var finding=_findings.Generate(exposure).FirstOrDefault(x=>
            x.FindingType==IdentityExposureFindingType.HighRiskConfiguredNotObserved &&
            string.Equals(x.CapabilityId,capabilityId,StringComparison.OrdinalIgnoreCase));
        var recommendation=finding is null ? null : _recommendations.Generate([finding]).SingleOrDefault();
        var candidate=recommendation is null
            ? new ProposedGovernanceChangeCandidateResult(false,null,
                "No qualifying finding and recommendation are available.")
            : _candidates.Offer(recommendation);
        ProposedChangeSimulationOutcome? simulation=null;
        if(candidate.IsAvailable)
        {
            var environment=item?.Environments.FirstOrDefault() ?? string.Empty;
            simulation=_simulation.Simulate(new ProposedGovernanceChangeSimulationRequest
            { Proposal=candidate.Proposal!,Identity=identityId,Capability=capabilityId,
              Context=new(){["environment"]=environment,["resource"]="proposed-change-review"} });
        }
        var stale=simulation?.ErrorCode=="stale_proposal";
        return new(identityId,capabilityId,exposure,item,finding,recommendation,
            candidate,simulation,stale,Limitations);
    }

    private static readonly IReadOnlyCollection<string> Limitations=[
        "This proposal has not been applied.",
        "Simulation does not modify active governance.",
        "Static changes do not prove complete runtime blast radius.",
        "A recommendation does not establish business necessity.",
        "A proposed outcome for one request context does not establish every possible runtime outcome.",
        "Curated capability risk is not a calculated risk score.",
        "A configuration fingerprint difference does not identify the specific policy change unless that change is otherwise known."
    ];
}

public sealed record ProposedChangeReview(string IdentityId,string CapabilityId,
    IdentityExposureAnalysis Exposure,IdentityExposureItem? ExposureItem,
    IdentityExposureFinding? Finding,IdentityExposureRecommendation? Recommendation,
    ProposedGovernanceChangeCandidateResult Candidate,
    ProposedChangeSimulationOutcome? Simulation,bool IsStale,
    IReadOnlyCollection<string> Limitations);
