using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GitHubActionsGateScriptTests
{
    private static readonly string ScriptPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "integrations",
        "github-actions",
        "invoke-seneschal-gate.ps1"));

    [Theory]
    [InlineData("allow", "LogOnly", "allow", 0)]
    [InlineData("deny", "LogOnly", "logged_only", 0)]
    [InlineData("deny", "Enforce", "deny", 1)]
    [InlineData("requires_approval", "Enforce", "requires_approval", 1)]
    public async Task DecisionResponse_ProducesExpectedExitCode(
        string decision,
        string mode,
        string effectiveAction,
        int expectedExitCode)
    {
        await using var server = await GateStubServer.StartAsync(
            HttpStatusCode.OK,
            $$"""
            {"decision":"{{decision}}","mode":"{{mode}}","effectiveAction":"{{effectiveAction}}","policyMatched":"test-policy","reason":"test reason"}
            """);

        var result = await RunGateAsync(server.BaseUrl, "test-secret-key");

        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.Contains($"Decision: {decision}", result.StandardOutput);
        Assert.Contains($"Enforcement mode: {mode}", result.StandardOutput);
        Assert.Contains($"Effective action: {effectiveAction}", result.StandardOutput);
        Assert.Contains("Matched policy: test-policy", result.StandardOutput);
        Assert.Contains("Reason: test reason", result.StandardOutput);
    }

    [Fact]
    public async Task InvalidKey_FailsClosed()
    {
        await using var server = await GateStubServer.StartAsync(
            HttpStatusCode.Unauthorized,
            "{\"reason\":\"A valid Seneschal API key is required.\"}");

        var result = await RunGateAsync(server.BaseUrl, "invalid-key");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("fail-closed", result.StandardError);
    }

    [Fact]
    public async Task RuntimeUnavailable_FailsClosed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var result = await RunGateAsync($"http://127.0.0.1:{port}", "unavailable-key");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("fail-closed", result.StandardError);
    }

    [Fact]
    public async Task ApiKey_IsNeverWrittenToOutput()
    {
        const string secret = "never-print-this-api-key";
        await using var server = await GateStubServer.StartAsync(
            HttpStatusCode.OK,
            "{\"decision\":\"allow\",\"mode\":\"LogOnly\",\"effectiveAction\":\"allow\",\"policyMatched\":\"safe\",\"reason\":\"allowed\"}");

        var result = await RunGateAsync(server.BaseUrl, secret);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(secret, result.StandardOutput);
        Assert.DoesNotContain(secret, result.StandardError);
    }

    private static async Task<GateResult> RunGateAsync(
        string baseUrl,
        string apiKey)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", ScriptPath,
            "-BaseUrl", baseUrl,
            "-ApiKey", apiKey,
            "-Identity", "github-actions-production",
            "-Capability", "production.deployment.execute",
            "-Environment", "production",
            "-Resource", "checkout-api"
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Could not start PowerShell.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return new GateResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private sealed record GateResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed class GateStubServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _responseTask;

        private GateStubServer(
            TcpListener listener,
            Task responseTask,
            string baseUrl)
        {
            _listener = listener;
            _responseTask = responseTask;
            BaseUrl = baseUrl;
        }

        public string BaseUrl { get; }

        public static Task<GateStubServer> StartAsync(
            HttpStatusCode statusCode,
            string responseBody)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var responseTask = RespondOnceAsync(listener, statusCode, responseBody);
            return Task.FromResult(new GateStubServer(
                listener,
                responseTask,
                $"http://127.0.0.1:{port}"));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _responseTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or TimeoutException or SocketException)
            {
            }
        }

        private static async Task RespondOnceAsync(
            TcpListener listener,
            HttpStatusCode statusCode,
            string responseBody)
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            string? line;
            var contentLength = 0;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = int.Parse(line["Content-Length:".Length..].Trim());
                }
            }
            if (contentLength > 0)
            {
                var requestBody = new char[contentLength];
                await reader.ReadBlockAsync(requestBody, 0, requestBody.Length);
            }

            var body = Encoding.UTF8.GetBytes(responseBody);
            var reason = statusCode == HttpStatusCode.OK ? "OK" : "Unauthorized";
            var headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)statusCode} {reason}\r\n" +
                "Content-Type: application/json\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(headers);
            await stream.WriteAsync(body);
        }
    }
}
