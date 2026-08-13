using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class GovernanceConfigurationFingerprintServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(),
        $"seneschal-fingerprint-{Guid.NewGuid():N}");

    public GovernanceConfigurationFingerprintServiceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Fingerprint_IsDeterministicAcrossEquivalentYamlFormatting()
    {
        var first = Create("""
            policies:
            - name: deploy
              identity: operator
              capability: app.deploy
              environment: prod
              decision: allow
              reason: Approved deployment.
            """);
        var second = Create("""
            policies:
              - name: deploy
                identity: operator
                capability: app.deploy
                environment: prod
                decision: allow
                reason: Approved deployment.
            """);

        Assert.Equal(first.Service.GetCurrentFingerprint(),
            second.Service.GetCurrentFingerprint());
        Assert.StartsWith("sha256:", first.Service.GetCurrentFingerprint());
    }

    [Fact]
    public void Fingerprint_ChangesForPolicyModeAndWindowSemantics()
    {
        var fixture = Create(BasePolicy);
        var original = fixture.Service.GetCurrentFingerprint();
        File.WriteAllText(fixture.Path, BasePolicy.Replace("decision: allow", "decision: deny"));
        var policyChanged = new GovernanceConfigurationFingerprintService(
            new PolicyLoader(fixture.Path), fixture.Mode, fixture.Window)
            .GetCurrentFingerprint();
        fixture.Mode.SetMode(EnforcementMode.Enforce);
        var modeChanged = fixture.Service.GetCurrentFingerprint();
        fixture.Window.SetState(true, GovernanceWindowMode.Enforce);
        var windowChanged = fixture.Service.GetCurrentFingerprint();

        Assert.NotEqual(original, policyChanged);
        Assert.NotEqual(original, modeChanged);
        Assert.NotEqual(modeChanged, windowChanged);
    }

    [Fact]
    public void Fingerprint_DoesNotContainSecretsOrRawConfiguration()
    {
        var fixture = Create(BasePolicy);
        var fingerprint = fixture.Service.GetCurrentFingerprint();
        Assert.DoesNotContain("Approved deployment", fingerprint);
        Assert.DoesNotContain("operator", fingerprint);
        Assert.Equal(71, fingerprint.Length);
    }

    private Fixture Create(string yaml)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        var mode = new InMemoryGovernanceModeStore(new RuntimeSettings());
        var window = new InMemoryGovernanceWindowStore();
        return new(new GovernanceConfigurationFingerprintService(
            new PolicyLoader(path), mode, window), path, mode, window);
    }

    private const string BasePolicy = """
        policies:
        - name: deploy
          identity: operator
          capability: app.deploy
          environment: prod
          decision: allow
          reason: Approved deployment.
        """;

    public void Dispose() => Directory.Delete(_directory, recursive: true);
    private sealed record Fixture(GovernanceConfigurationFingerprintService Service,
        string Path, InMemoryGovernanceModeStore Mode,
        InMemoryGovernanceWindowStore Window);
}