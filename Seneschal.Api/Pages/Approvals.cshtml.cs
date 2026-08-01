using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Api.Services;

namespace Seneschal.Api.Pages;

[IgnoreAntiforgeryToken]
public sealed class ApprovalsModel : PageModel
{
    private readonly IApprovalStore _store;
    private readonly ApprovalResolutionService _resolutionService;

    public ApprovalsModel(IApprovalStore store,
        ApprovalResolutionService resolutionService)
    {
        _store = store;
        _resolutionService = resolutionService;
    }

    public IReadOnlyCollection<ApprovalRecord> Approvals { get; private set; } = [];

    public void OnGet() => Load();

    public JsonResult OnGetState() => new(_store.GetAll().Select(record => new
    {
        approvalId = record.Id,
        operationId = record.OperationId,
        correlationMode = record.CorrelationMode.ToString(),
        identity = record.IdentityId,
        capability = record.CapabilityId,
        environment = record.Environment,
        resource = record.ResourceId,
        requestedAt = record.RequestedAt,
        status = record.Status.ToString(),
        resolvedAt = record.ResolvedAt,
        resolvedBy = record.ResolvedBy,
        consumedAt = record.ConsumedAt,
        consumedByDecisionId = record.ConsumedByDecisionId
    }));

    public async Task<IActionResult> OnPostResolveAsync(
        string approvalId, string resolution, string resolvedBy)
    {
        if (!Enum.TryParse<ApprovalStatus>(resolution, true, out var status) ||
            status is not (ApprovalStatus.Approved or ApprovalStatus.Rejected) ||
            string.IsNullOrWhiteSpace(resolvedBy))
            return BadRequest();

        var record = await _resolutionService.ResolveAsync(
            approvalId, status, resolvedBy, DateTimeOffset.UtcNow,
            HttpContext?.RequestAborted ?? default);
        if (record is null) return NotFound();
        return RedirectToPage();
    }

    private void Load() => Approvals = _store.GetAll()
        .OrderBy(record => record.Status switch
        {
            ApprovalStatus.Pending => 0,
            ApprovalStatus.Approved => 1,
            ApprovalStatus.Rejected => 2,
            ApprovalStatus.Consumed => 3,
            _ => 4
        })
        .ThenByDescending(record => record.RequestedAt)
        .ToList();
}
