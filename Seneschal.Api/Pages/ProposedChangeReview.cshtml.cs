using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;

namespace Seneschal.Api.Pages;

public sealed class ProposedChangeReviewModel : PageModel
{
    private readonly ProposedChangeReviewService _reviews;
    public ProposedChangeReviewModel(ProposedChangeReviewService reviews)=>_reviews=reviews;
    public ProposedChangeReview? Review { get; private set; }
    public async Task OnGetAsync(string identityId,string capabilityId,int? days,
        CancellationToken cancellationToken) => Review=await _reviews.BuildAsync(
            identityId,capabilityId,days??IdentityExposureAnalysisService.DefaultObservationDays,cancellationToken);
}
