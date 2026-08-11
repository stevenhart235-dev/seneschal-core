using Seneschal.Client;
using Seneschal.Client.Models;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PreflightCommandTests
{
    private static readonly PreflightOptions Options = new(
        new Uri("https://seneschal.example"),
        "secret-never-print",
        "refund-worker",
        "payments.refund.create",
        "development",
        "preflight");

    [Fact]
    public async Task Success_IsConciseReadyAndNeverPrintsApiKey()
    {
        var report = await PreflightRunner.RunAsync(
            Options,
            FakeTransport.Result("allow", "Proceed"));

        Assert.Equal(0, report.ExitCode);
        Assert.Equal(PreflightFailureCategory.None, report.Category);
        Assert.Contains("Endpoint:       OK", report.Lines);
        Assert.Contains("Health:         Healthy", report.Lines);
        Assert.Contains("Readiness:      Ready", report.Lines);
        Assert.Contains("Integration:    Ready", report.Lines);
        Assert.DoesNotContain(report.Lines, line => line.Contains(Options.ApiKey));
    }

    [Theory]
    [InlineData(PreflightFailureCategory.AuthenticationFailure)]
    [InlineData(PreflightFailureCategory.InvalidIdentity)]
    [InlineData(PreflightFailureCategory.InvalidCapability)]
    [InlineData(PreflightFailureCategory.ScopeMismatch)]
    public async Task EvaluationContractFailures_AreDistinct(
        PreflightFailureCategory category)
    {
        var report = await PreflightRunner.RunAsync(
            Options,
            FakeTransport.Failure(category));

        Assert.Equal(2, report.ExitCode);
        Assert.Equal(category, report.Category);
        Assert.Contains("Integration:    Not ready", report.Lines);
    }

    [Fact]
    public async Task EndpointUnavailable_IsDistinct()
    {
        var report = await PreflightRunner.RunAsync(
            Options,
            FakeTransport.EndpointUnavailable());

        Assert.Equal(PreflightFailureCategory.EndpointUnavailable, report.Category);
        Assert.Contains("Endpoint:       Unavailable", report.Lines);
    }

    [Fact]
    public async Task ServiceNotReady_IsDistinct()
    {
        var report = await PreflightRunner.RunAsync(
            Options,
            FakeTransport.NotReady());

        Assert.Equal(PreflightFailureCategory.ServiceNotReady, report.Category);
        Assert.Contains("Readiness:      Not ready", report.Lines);
    }

    [Theory]
    [InlineData("deny", "Block", PreflightFailureCategory.EvaluationDenied)]
    [InlineData("requires_approval", "Pause", PreflightFailureCategory.ApprovalRequired)]
    public async Task GovernanceStops_AreAccurateButIntegrationIsReady(
        string decision,
        string guidance,
        PreflightFailureCategory expectedCategory)
    {
        var report = await PreflightRunner.RunAsync(
            Options,
            FakeTransport.Result(decision, guidance));

        Assert.Equal(0, report.ExitCode);
        Assert.Equal(expectedCategory, report.Category);
        Assert.Contains(
            expectedCategory == PreflightFailureCategory.ApprovalRequired
                ? "Governance:     Approval required"
                : "Governance:     Denied",
            report.Lines);
        Assert.Contains("Execution:      Stop", report.Lines);
        Assert.Contains("Integration:    Ready", report.Lines);
    }

    [Fact]
    public async Task UnknownGuidance_FailsClosedAndInvalidatesContract()
    {
        var report = await PreflightRunner.RunAsync(
            Options,
            FakeTransport.Result("allow", "ExecuteImmediately"));

        Assert.Equal(2, report.ExitCode);
        Assert.Equal(PreflightFailureCategory.MalformedGuidance, report.Category);
        Assert.Contains("Integration:    Not ready", report.Lines);
    }

    [Theory]
    [InlineData("allow", "Proceed", 0, PreflightFailureCategory.None, "Yes")]
    [InlineData("deny", "Block", 0, PreflightFailureCategory.EvaluationDenied, "No")]
    [InlineData("requires_approval", "Pause", 0, PreflightFailureCategory.ApprovalRequired, "No")]
    [InlineData("deny", "ContinueLogOnly", 0, PreflightFailureCategory.EvaluationDenied, "Yes")]
    public async Task PolicySimulation_PresentsGovernanceOutcomeAsValid(
        string decision,
        string guidance,
        int expectedExitCode,
        PreflightFailureCategory expectedCategory,
        string wouldExecute)
    {
        var report = await PolicySimulationRunner.RunAsync(
            Options,
            FakeTransport.Result(decision, guidance, governanceWindow: true));

        Assert.Equal(expectedExitCode, report.ExitCode);
        Assert.Equal(expectedCategory, report.Category);
        Assert.Contains($"ShouldProceed/Would execute: {wouldExecute}", report.Lines);
        Assert.Contains("Matched policies:", report.Lines);
        Assert.Contains("  - policy-a", report.Lines);
        Assert.Contains("  Name: Production Freeze", report.Lines);
        Assert.Contains("  Mode: Enforce", report.Lines);
        Assert.Contains("  Influenced result: Yes", report.Lines);
        Assert.Contains(report.Lines, line => line.StartsWith("Reason:"));
        Assert.Contains(report.Lines, line => line.StartsWith("Approval status:"));
        Assert.DoesNotContain(report.Lines, line => line.Contains(Options.ApiKey));
    }

    [Fact]
    public async Task PolicySimulation_UnknownGuidanceFailsClosed()
    {
        var report = await PolicySimulationRunner.RunAsync(
            Options,
            FakeTransport.Result("allow", "ExecuteImmediately"));

        Assert.Equal(2, report.ExitCode);
        Assert.Equal(PreflightFailureCategory.MalformedGuidance, report.Category);
        Assert.Contains(report.Lines, line => line.Contains("fail closed"));
    }

    [Theory]
    [InlineData(PreflightFailureCategory.AuthenticationFailure)]
    [InlineData(PreflightFailureCategory.ScopeMismatch)]
    public async Task PolicySimulation_AuthAndScopeFailuresAreNonZero(
        PreflightFailureCategory category)
    {
        var report = await PolicySimulationRunner.RunAsync(
            Options,
            FakeTransport.Failure(category));

        Assert.Equal(2, report.ExitCode);
        Assert.Equal(category, report.Category);
    }

    private sealed class FakeTransport : IPreflightTransport
    {
        private readonly ServiceProbe _health;
        private readonly ServiceProbe _ready;
        private readonly DecisionResult? _result;
        private readonly PreflightFailureCategory? _failure;

        private FakeTransport(
            ServiceProbe health,
            ServiceProbe ready,
            DecisionResult? result = null,
            PreflightFailureCategory? failure = null)
        {
            _health = health;
            _ready = ready;
            _result = result;
            _failure = failure;
        }

        public static FakeTransport Result(
            string decision,
            string guidance,
            bool governanceWindow = false) => new(
            new ServiceProbe(true, "Healthy"),
            new ServiceProbe(true, "Ready"),
            new DecisionResult
            {
                Decision = decision,
                EffectiveAction = decision,
                ExecutionGuidance = guidance,
                Reason = "Policy explanation",
                ApprovalStatus = decision == "requires_approval" ? "Pending" : null,
                MatchedPolicies = ["policy-a"],
                GovernanceWindowName = governanceWindow ? "Production Freeze" : null,
                GovernanceWindowMode = governanceWindow ? "Enforce" : null,
                GovernanceWindowReason = governanceWindow ? "Release freeze" : null,
                GovernanceWindowInfluencedResult = governanceWindow
            });

        public static FakeTransport Failure(PreflightFailureCategory category) => new(
            new ServiceProbe(true, "Healthy"),
            new ServiceProbe(true, "Ready"),
            failure: category);

        public static FakeTransport EndpointUnavailable() => new(
            new ServiceProbe(false, "Unavailable"),
            new ServiceProbe(false, "Unknown"),
            failure: PreflightFailureCategory.EndpointUnavailable);

        public static FakeTransport NotReady() => new(
            new ServiceProbe(true, "Healthy"),
            new ServiceProbe(false, "Not ready"));

        public Task<ServiceProbe> CheckHealthAsync(CancellationToken cancellationToken)
        {
            if (_failure == PreflightFailureCategory.EndpointUnavailable)
                throw new PreflightTransportException(_failure.Value, "endpoint unavailable");
            return Task.FromResult(_health);
        }

        public Task<ServiceProbe> CheckReadinessAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_ready);

        public Task<DecisionResult> EvaluateAsync(
            PreflightOptions options,
            CancellationToken cancellationToken)
        {
            if (_failure is not null)
                throw new PreflightTransportException(_failure.Value, "preflight failed");
            return Task.FromResult(_result!);
        }
    }
}
