using Seneschal.Api.Pages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    [Fact]
    public async Task ApprovingRejectedApproval_ReturnsConflictWithoutMutationOrEvidence()
    {
        var (page, store, audit, record) = CreatePage();
        store.Resolve(record.Id, ApprovalStatus.Rejected, "first", DateTimeOffset.UtcNow);

        var result = await page.OnPostResolveAsync(
            record.Id, "Approved", "second");

        Assert.IsType<PageResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, page.Response.StatusCode);
        Assert.Contains("current status does not allow", page.StatusMessage);
        Assert.Equal(ApprovalStatus.Rejected, store.GetById(record.Id)!.Status);
        Assert.Empty(await audit.GetRecentAsync());
    }

    [Fact]
    public async Task RejectingConsumedApproval_ReturnsConflictWithoutDuplicateEvidence()
    {
        var (page, store, audit, record) = CreatePage();
        await page.OnPostResolveAsync(record.Id, "Approved", "first");
        store.Consume(record.Id, "decision", DateTimeOffset.UtcNow);
        var evidenceBefore = await audit.GetRecentAsync();

        var result = await page.OnPostResolveAsync(
            record.Id, "Rejected", "second");

        Assert.IsType<PageResult>(result);
        Assert.Equal(ApprovalStatus.Consumed, store.GetById(record.Id)!.Status);
        Assert.Equal(evidenceBefore.Count, (await audit.GetRecentAsync()).Count);
    }

    [Fact]
    public async Task ResolvingAlreadyResolvedApproval_DoesNotDuplicateEvidence()
    {
        var (page, store, audit, record) = CreatePage();
        await page.OnPostResolveAsync(record.Id, "Approved", "first");

        var result = await page.OnPostResolveAsync(
            record.Id, "Approved", "second");

        Assert.IsType<PageResult>(result);
        Assert.Equal(ApprovalStatus.Approved, store.GetById(record.Id)!.Status);
        Assert.Single(await audit.GetRecentAsync());
    }

    [Fact]
    public async Task ConcurrentMutation_ReturnsDistinctConflictMessage()
    {
        var store = new InMemoryApprovalStore();
        var record = store.GetOrCreate("identity", "capability", "prod",
            "resource", "reason", DateTimeOffset.UtcNow).Record;
        var page = CreatePage(store, new ThrowingCoordinator(
            new OperationalControlConcurrencyException("approval", 0, 1)));

        var result = await page.OnPostResolveAsync(
            record.Id, "Approved", "reviewer");

        Assert.IsType<PageResult>(result);
        Assert.Contains("changed by another operation", page.StatusMessage);
        Assert.Equal(ApprovalStatus.Pending, store.GetById(record.Id)!.Status);
    }

    [Fact]
    public async Task ProviderFailure_ReturnsSafeUnavailableResponse()
    {
        var store = new InMemoryApprovalStore();
        var record = store.GetOrCreate("identity", "capability", "prod",
            "resource", "reason", DateTimeOffset.UtcNow).Record;
        var page = CreatePage(store, new ThrowingCoordinator(
            new EvaluationCommitException("database table secret")));

        var result = Assert.IsType<ObjectResult>(await page.OnPostResolveAsync(
            record.Id, "Approved", "reviewer"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.DoesNotContain("database", result.Value?.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ApprovalStatus.Pending, store.GetById(record.Id)!.Status);
    }

    private static (ApprovalsModel Page, InMemoryApprovalStore Store,
        InMemoryAuditEventStore Audit, ApprovalRecord Record) CreatePage()
    {
        var store = new InMemoryApprovalStore();
        var audit = new InMemoryAuditEventStore();
        var record = store.GetOrCreate("identity", "capability", "prod",
            "resource", "reason", DateTimeOffset.UtcNow).Record;
        return (CreatePage(store,
            new InMemoryEvaluationCommitCoordinator(audit, store)),
            store, audit, record);
    }

    private static ApprovalsModel CreatePage(
        InMemoryApprovalStore store,
        IEvaluationCommitCoordinator coordinator)
    {
        var page = new ApprovalsModel(store, new ApprovalResolutionService(
            store, coordinator,
            new InMemoryGovernanceModeStore(new RuntimeSettings())));
        page.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return page;
    }

    private sealed class ThrowingCoordinator(Exception exception) :
        IEvaluationCommitCoordinator
    {
        public Task CommitAsync(EvaluationCommit evaluationCommit,
            CancellationToken cancellationToken = default) =>
            Task.FromException(exception);
    }
}
