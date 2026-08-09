using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Seneschal.Client.Models;
using Xunit;

namespace Seneschal.Client.Tests;

public sealed class ExecutionGuidanceConformanceTests
{
    private static readonly string ContractDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "execution-guidance-contracts");
    private static readonly string FixturePath = Path.Combine(
        ContractDirectory,
        "execution-guidance-conformance.v1.json");

    [Fact]
    public async Task ClientConformsToLanguageNeutralFixture()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath));
        var root = fixture.RootElement;

        Assert.Equal("seneschal.execution-guidance", root.GetProperty("contract").GetString());
        Assert.Equal("v1", root.GetProperty("contractVersion").GetString());
        Assert.Equal(1, root.GetProperty("revision").GetInt32());

        foreach (var testCase in root.GetProperty("cases").EnumerateArray())
        {
            var id = testCase.GetProperty("id").GetString();
            var input = testCase.GetProperty("input");
            var expected = testCase.GetProperty("expected");
            var responseJson = BuildResponseJson(input);
            var result = await EvaluateResponseAsync(responseJson);
            var expectedSemantic = Enum.Parse<ExecutionGuidanceKind>(
                expected.GetProperty("semantic").GetString()!,
                ignoreCase: false);

            Assert.True(
                expectedSemantic == result.Guidance,
                $"Fixture case '{id}' expected semantic {expectedSemantic} but received {result.Guidance}.");
            Assert.True(
                expected.GetProperty("shouldProceed").GetBoolean() == result.ShouldProceed,
                $"Fixture case '{id}' produced an unexpected ShouldProceed value.");
            AssertRawValuePreserved(id!, input, result.RawExecutionGuidance);
        }
    }

    [Fact]
    public void V1MetadataIsConsistentAndSemanticEditsRequireManifestUpdate()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(FixturePath));
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            ContractDirectory,
            "execution-guidance-conformance.schema.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            ContractDirectory,
            "execution-guidance-contract.json")));

        var fixtureRoot = fixture.RootElement;
        var schemaRoot = schema.RootElement;
        var manifestRoot = manifest.RootElement;
        var version = fixtureRoot.GetProperty("contractVersion").GetString();
        var revision = fixtureRoot.GetProperty("revision").GetInt32();

        Assert.Equal("v1", version);
        Assert.Equal(version, schemaRoot
            .GetProperty("properties")
            .GetProperty("contractVersion")
            .GetProperty("const")
            .GetString());
        Assert.Contains("/v1/", schemaRoot.GetProperty("$id").GetString());
        Assert.Equal(version, manifestRoot.GetProperty("contractVersion").GetString());
        Assert.Equal(revision, manifestRoot.GetProperty("fixtureRevision").GetInt32());
        Assert.Equal(Path.GetFileName(FixturePath), manifestRoot.GetProperty("fixture").GetString());
        Assert.Equal(
            ExecutionGuidanceContract.ConformanceVersion,
            version);
        Assert.Equal(
            ExecutionGuidanceContract.ConformanceRevision,
            revision);

        Assert.Equal(
            manifestRoot.GetProperty("semanticSha256").GetString(),
            SemanticChecksum(fixtureRoot));

        var changelog = File.ReadAllText(Path.Combine(ContractDirectory, "CHANGELOG.md"));
        Assert.Contains($"## {version} revision {revision}", changelog);
    }

    [Fact]
    public void FixtureOnlyAuthorizesCanonicalImmediateExecutionSemantics()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var authorizedSemantics = fixture.RootElement
            .GetProperty("cases")
            .EnumerateArray()
            .Where(testCase => testCase
                .GetProperty("expected")
                .GetProperty("shouldProceed")
                .GetBoolean())
                .Select(testCase => testCase
                    .GetProperty("expected")
                    .GetProperty("semantic")
                    .GetString()!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ContinueLogOnly", "Proceed"], authorizedSemantics);
    }

    private static string BuildResponseJson(JsonElement input)
    {
        var guidanceProperty = input.TryGetProperty(
            "executionGuidance",
            out var guidance)
            ? $",\"executionGuidance\":{guidance.GetRawText()}"
            : string.Empty;

        return $"{{\"decision\":\"allow\",\"mode\":\"LogOnly\"{guidanceProperty}}}";
    }

    private static string SemanticChecksum(JsonElement fixture)
    {
        var canonicalJson = JsonSerializer.Serialize(fixture);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<DecisionResult> EvaluateResponseAsync(string responseJson)
    {
        var handler = new FixtureResponseHandler(responseJson);
        using var httpClient = new HttpClient(handler);
        var client = SeneschalClient.Create(
            httpClient,
            new Uri("https://seneschal.example"));

        return await client.EvaluateAsync(new DecisionRequest
        {
            Identity = "conformance-test",
            Capability = "conformance.evaluate"
        });
    }

    private static void AssertRawValuePreserved(
        string id,
        JsonElement input,
        string? actual)
    {
        if (!input.TryGetProperty("executionGuidance", out var guidance))
        {
            Assert.True(actual == string.Empty, $"Fixture case '{id}' did not preserve the missing-property SDK default.");
            return;
        }

        var expected = guidance.ValueKind == JsonValueKind.Null
            ? null
            : guidance.GetString();
        Assert.True(expected == actual, $"Fixture case '{id}' did not preserve the raw guidance value.");
    }

    private sealed class FixtureResponseHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
    }
}
