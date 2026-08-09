using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Seneschal.Client.Models;
using Xunit;

namespace Seneschal.Client.Tests;

public sealed class SeneschalClientTests
{
    [Fact]
    public void PublicConstructors_ExposeSingleTypedHttpClientConstructor()
    {
        var constructor = Assert.Single(typeof(SeneschalClient).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Collection(
            parameters,
            parameter => Assert.Equal(typeof(HttpClient), parameter.ParameterType),
            parameter => Assert.Equal(
                typeof(IOptions<SeneschalClientOptions>),
                parameter.ParameterType));
    }

    [Fact]
    public async Task EvaluateAsync_PostsRequestAndDeserializesDecision()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(
                    """
                    {
                      "decision": "allow",
                      "reason": "Allowed by policy",
                      "policyMatched": "policy-1",
                      "durationMs": 12,
                      "effectiveAction": "allow",
                      "mode": "LogOnly",
                      "executionGuidance": "Proceed",
                      "approvalId": "approval-1",
                      "approvalStatus": "Pending",
                      "operationId": "release-001",
                      "approvalCorrelationMode": "Operation",
                      "message": "caller message",
                      "retryGuidance": "retry later"
                    }
                    """)
            });
        using var httpClient = new HttpClient(handler);
        var client = SeneschalClient.Create(
            httpClient,
            new Uri("https://seneschal.example"));

        var result = await client.EvaluateAsync(new DecisionRequest
        {
            Identity = "payment-agent",
            Capability = "azure.keyvault.secret.read",
            OperationId = "release-001",
            Context = new Dictionary<string, string>
            {
                ["environment"] = "production"
            }
        });

        Assert.Equal("allow", result.Decision);
        Assert.Equal("Allowed by policy", result.Reason);
        Assert.Equal("policy-1", result.PolicyMatched);
        Assert.Equal(12, result.DurationMs);
        Assert.Equal("allow", result.EffectiveAction);
        Assert.Equal("LogOnly", result.Mode);
        Assert.Equal("Proceed", result.ExecutionGuidance);
        Assert.Equal("approval-1", result.ApprovalId);
        Assert.Equal("Pending", result.ApprovalStatus);
        Assert.Equal("release-001", result.OperationId);
        Assert.Equal("Operation", result.ApprovalCorrelationMode);
        Assert.Equal("caller message", result.Message);
        Assert.Equal("retry later", result.RetryGuidance);

        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.Equal(
            "https://seneschal.example/evaluate",
            handler.Requests.Single().RequestUri?.ToString());
        Assert.Contains(
            "Seneschal.Client/0.1.0-alpha.1",
            handler.Requests.Single().Headers["User-Agent"]);

        using var document = JsonDocument.Parse(handler.Requests.Single().Body);

        Assert.Equal(
            "payment-agent",
            document.RootElement.GetProperty("identity").GetString());
        Assert.Equal(
            "azure.keyvault.secret.read",
            document.RootElement.GetProperty("capability").GetString());
        Assert.Equal(
            "release-001",
            document.RootElement.GetProperty("operationId").GetString());
        Assert.Equal(
            "production",
            document.RootElement
                .GetProperty("context")
                .GetProperty("environment")
                .GetString());
    }

    [Fact]
    public async Task EvaluateAsync_SendsApiKeyHeaderWhenConfigured()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"decision":"allow"}""")
            });
        using var httpClient = new HttpClient(handler);
        var client = new SeneschalClient(
            httpClient,
            Options.Create(new SeneschalClientOptions
            {
                BaseUrl = new Uri("https://seneschal.example"),
                ApiKey = "secret-token"
            }));

        await client.EvaluateAsync(CreateRequest());

        Assert.True(
            handler.Requests.Single().Headers.TryGetValue(
                "X-Seneschal-Api-Key",
                out var values));
        Assert.Equal("secret-token", values.Single());
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsForUnsuccessfulResponse()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = JsonContent("boom")
            });
        using var httpClient = new HttpClient(handler);
        var client = SeneschalClient.Create(
            httpClient,
            new Uri("https://seneschal.example"));

        var exception = await Assert.ThrowsAsync<SeneschalClientException>(
            () => client.EvaluateAsync(CreateRequest()));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal("boom", exception.ResponseBody);
        Assert.Contains("HTTP 500", exception.Message);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsForInvalidJsonResponse()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("{not-json")
            });
        using var httpClient = new HttpClient(handler);
        var client = SeneschalClient.Create(
            httpClient,
            new Uri("https://seneschal.example"));

        var exception = await Assert.ThrowsAsync<SeneschalClientException>(
            () => client.EvaluateAsync(CreateRequest()));

        Assert.Contains("invalid decision response", exception.Message);
    }

    [Fact]
    public async Task EvaluateAsync_ThrowsForConnectivityFailure()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("network down"));
        using var httpClient = new HttpClient(handler);
        var client = SeneschalClient.Create(
            httpClient,
            new Uri("https://seneschal.example"));

        var exception = await Assert.ThrowsAsync<SeneschalClientException>(
            () => client.EvaluateAsync(CreateRequest()));

        Assert.Contains("Unable to reach", exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task EvaluateAsync_PropagatesCallerCancellation()
    {
        var handler = new CancellationAwareHandler();
        using var httpClient = new HttpClient(handler);
        var client = SeneschalClient.Create(
            httpClient,
            new Uri("https://seneschal.example"));
        using var cancellation = new CancellationTokenSource();

        var evaluation = client.EvaluateAsync(
            CreateRequest(),
            cancellation.Token);
        await handler.RequestStarted;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await evaluation);
        Assert.True(handler.ObservedCancellationToken.IsCancellationRequested);
    }

    [Theory]
    [InlineData("Proceed", true)]
    [InlineData("proceed", true)]
    [InlineData("ContinueLogOnly", true)]
    [InlineData("Block", false)]
    [InlineData("Pause", false)]
    [InlineData("Queue", false)]
    [InlineData("Retry", false)]
    [InlineData("ExecuteImmediately", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void DecisionResult_ShouldProceedDerivesOnlyFromExecutionGuidance(
        string? guidance,
        bool expected)
    {
        var result = new DecisionResult
        {
            Decision = "allow",
            Mode = "LogOnly",
            ExecutionGuidance = guidance!
        };

        Assert.Equal(expected, result.ShouldProceed);
    }

    private static DecisionRequest CreateRequest()
    {
        return new DecisionRequest
        {
            Identity = "payment-agent",
            Capability = "azure.keyvault.secret.read",
            Context = new Dictionary<string, string>
            {
                ["environment"] = "production"
            }
        };
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.ToDictionary(
                    header => header.Key,
                    header => header.Value.ToList()),
                body));

            return _handler(request);
        }
    }

    private sealed class CancellationAwareHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _requestStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => _requestStarted.Task;
        public CancellationToken ObservedCancellationToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ObservedCancellationToken = cancellationToken;
            _requestStarted.SetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        Dictionary<string, List<string>> Headers,
        string Body);
}
