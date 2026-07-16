using Xunit;

namespace Seneschal.Api.Tests;

public sealed class DemoLauncherTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void LauncherRestoresAndTracksApprovalWorkerWithFreshPackageCache()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot, "demo.ps1"));

        Assert.Contains("'labs/multi-application-adoption/ApprovalWorker/ApprovalWorker.csproj'", script);
        Assert.Contains("Start-DemoProcess -Name 'approval-worker'", script);
        Assert.Contains("$packageCacheDirectory = Join-Path $demoDirectory 'packages'", script);
        Assert.Contains("Remove-Item -LiteralPath $packageCacheDirectory -Recurse -Force", script);
        Assert.Contains("$env:NUGET_PACKAGES = $packageCacheDirectory", script);
        Assert.Contains("dotnet restore $workerProject --force --no-cache", script);
        Assert.Contains("'run', '--no-restore', '--project'", script);
    }

    [Fact]
    public void StopLauncherStopsEveryTrackedProcessId()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot, "stop-demo.ps1"));

        Assert.Contains("foreach ($processId in @($state.ProcessIds))", script);
        Assert.Contains("taskkill.exe /PID ([int]$processId) /T /F", script);
    }

    [Fact]
    public void ApprovalRunnerAttachesToDemoAndPollsExistingApprovalEvidence()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot, "demo-approval.ps1"));

        Assert.Contains("artifacts/demo/state.json", script);
        Assert.Contains("$baseUrl/ready", script);
        Assert.Contains("$baseUrl/audit", script);
        Assert.Contains("release-approval-worker", script);
        Assert.Contains("production.release.approve", script);
        Assert.Contains("approvalOperationId", script);
        Assert.Contains("approvalAction -eq 'Consumed'", script);
        Assert.Contains("approvalAction -eq 'Used'", script);
        Assert.Contains("Start it with .\\demo.ps1", script);
        Assert.DoesNotContain("& .\\demo.ps1", script);
        Assert.DoesNotContain("Start-Process dotnet", script);
    }
}
