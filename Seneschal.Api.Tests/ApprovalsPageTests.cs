using Seneschal.Api.Pages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
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
        var page = new ApprovalsModel(store, audit,
            new InMemoryGovernanceModeStore(new RuntimeSettings()));

        await page.OnPostResolveAsync(record.Id, resolution, "<reviewer>");

        Assert.Equal(expected, store.GetAll().Single().Status);
        var evidence = Assert.Single(await audit.GetRecentAsync());
        Assert.Equal(resolution, evidence.ApprovalAction);
        Assert.Equal("<reviewer>", evidence.ApprovalResolvedBy);
    }
}
