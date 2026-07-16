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
    private readonly IAuditSink _audit;
    private readonly IGovernanceModeStore _mode;

    public ApprovalsModel(IApprovalStore store, IAuditSink audit,
        IGovernanceModeStore mode)
    {
        _store = store;
        _audit = audit;
        _mode = mode;
    }

    public IReadOnlyCollection<ApprovalRecord> Approvals { get; private set; } = [];

    public void OnGet() => Load();

    public async Task<IActionResult> OnPostResolveAsync(
        string approvalId, string resolution, string resolvedBy)
    {
        if (!Enum.TryParse<ApprovalStatus>(resolution, true, out var status) ||
            status is not (ApprovalStatus.Approved or ApprovalStatus.Rejected) ||
            string.IsNullOrWhiteSpace(resolvedBy))
            return BadRequest();

        var record = _store.Resolve(
            approvalId, status, resolvedBy, DateTimeOffset.UtcNow);
        if (record is null) return NotFound();

        await _audit.WriteAsync(new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            RequestId = record.Id,
            TimestampUtc = record.ResolvedAt!.Value,
            IdentityId = record.IdentityId,
            CapabilityId = record.CapabilityId,
            ResourceId = record.ResourceId,
            Environment = record.Environment,
            Decision = status == ApprovalStatus.Approved
                ? DecisionType.Allow : DecisionType.Deny,
            PolicyDecision = DecisionType.RequireApproval,
            EnforcementMode = _mode.GetMode(),
            Reason = $"Approval {status.ToString().ToLowerInvariant()} by {record.ResolvedBy}.",
            PolicyReason = record.RequestReason,
            ApprovalId = record.Id,
            ApprovalStatus = record.Status.ToString(),
            ApprovalAction = record.Status.ToString(),
            ApprovalRequestReason = record.RequestReason,
            ApprovalResolvedAt = record.ResolvedAt,
            ApprovalResolvedBy = record.ResolvedBy
            ,ApprovalOperationId = record.OperationId
            ,ApprovalCorrelationMode = record.CorrelationMode.ToString()
        });
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
