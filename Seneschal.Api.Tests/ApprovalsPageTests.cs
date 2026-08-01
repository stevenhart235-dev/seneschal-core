using Seneschal.Api.Pages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ApprovalsPageTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    public ApprovalsPageTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PageRendersSharedShellAndInMemoryWarning()
    {
        var html = await (await _factory.CreateClient().GetAsync("/approvals"))
            .Content.ReadAsStringAsync();
        Assert.Contains("<h1>Approvals</h1>", html);
        Assert.Contains("Temporary runtime state", html);
        Assert.Contains("aria-current=\"page\"><span>Approvals", html);
        Assert.Contains("Pending approvals", html);
        Assert.Contains("Approval history", html);
        Assert.Contains("Operation ID", html);
        Assert.DoesNotContain("table-scroll", html);
    }

    [Theory]
    [InlineData("Approved", ApprovalStatus.Approved)]
    [InlineData("Rejected", ApprovalStatus.Rejected)]
    public async Task ResolveActionUpdatesRecordAndWritesAudit(
        string resolution, ApprovalStatus expected)
    {
        var store = new InMemoryApprovalStore();
        var audit = new InMemoryAuditEventStore();
        var record = store.GetOrCreate("<identity>", "capability", "prod", "resource", "reason", DateTimeOffset.UtcNow).Record;
        var mode = new InMemoryGovernanceModeStore(new RuntimeSettings());
        var page = new ApprovalsModel(store,
            new ApprovalResolutionService(store,
                new InMemoryEvaluationCommitCoordinator(audit, store), mode));

        await page.OnPostResolveAsync(record.Id, resolution, "<reviewer>");

        Assert.Equal(expected, store.GetAll().Single().Status);
        var evidence = Assert.Single(await audit.GetRecentAsync());
        Assert.Equal(resolution, evidence.ApprovalAction);
        Assert.Equal("<reviewer>", evidence.ApprovalResolvedBy);
    }

    [Fact]
    public void PageOrdersPendingApprovedRejectedThenConsumed()
    {
        var store = new InMemoryApprovalStore();
        var now = DateTimeOffset.UtcNow;
        var pending = store.GetOrCreate("pending", "cap", "prod", "one", "reason", now).Record;
        var approved = store.GetOrCreate("approved", "cap", "prod", "two", "reason", now).Record;
        var rejected = store.GetOrCreate("rejected", "cap", "prod", "three", "reason", now).Record;
        var consumed = store.GetOrCreate("consumed", "cap", "prod", "four", "reason", now).Record;
        store.Resolve(approved.Id, ApprovalStatus.Approved, "operator", now);
        store.Resolve(rejected.Id, ApprovalStatus.Rejected, "operator", now);
        store.Resolve(consumed.Id, ApprovalStatus.Approved, "operator", now);
        store.Consume(consumed.Id, "decision", now);
        var audit = new InMemoryAuditEventStore();
        var page = new ApprovalsModel(store,
            new ApprovalResolutionService(store,
                new InMemoryEvaluationCommitCoordinator(audit, store),
                new InMemoryGovernanceModeStore(new RuntimeSettings())));

        page.OnGet();

        Assert.Equal(
            [ApprovalStatus.Pending, ApprovalStatus.Approved,
             ApprovalStatus.Rejected, ApprovalStatus.Consumed],
            page.Approvals.Select(item => item.Status));
        Assert.Equal(pending.Id, page.Approvals.First().Id);
    }

    [Fact]
    public void StateHandlerReturnsReadOnlyOperationScopedApprovalState()
    {
        var store = new InMemoryApprovalStore();
        var requestedAt = DateTimeOffset.UtcNow;
        var record = store.GetOrCreate(
            "worker", "production.release.approve", "production",
            "checkout-api", "reason", requestedAt, "release-demo-0042").Record;
        var audit = new InMemoryAuditEventStore();
        var page = new ApprovalsModel(store,
            new ApprovalResolutionService(store,
                new InMemoryEvaluationCommitCoordinator(audit, store),
                new InMemoryGovernanceModeStore(new RuntimeSettings())));

        var json = JsonSerializer.Serialize(page.OnGetState().Value);

        Assert.Contains(record.Id, json);
        Assert.Contains("release-demo-0042", json);
        Assert.Contains("Operation", json);
        Assert.Contains("Pending", json);
        Assert.Contains("production.release.approve", json);
    }
}
