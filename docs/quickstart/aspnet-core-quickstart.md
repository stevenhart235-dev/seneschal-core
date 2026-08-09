# ASP.NET Core Quickstart

Protect one action in an existing ASP.NET Core service in under ten minutes.
You need a reachable Seneschal URL, a scoped integration API key, and the
identity and capability names your Seneschal operator configured.

## 1. Install

```powershell
dotnet add package Seneschal.AspNetCore --version 0.1.0-alpha.1
```

`Seneschal.Client` is included transitively.

## 2. Configure two values

Add the endpoint and key to `appsettings.json` (use your normal secret store for
the key outside local development):

```json
{
  "Seneschal": {
    "BaseUrl": "http://localhost:5077",
    "ApiKey": "dev-sample-key"
  }
}
```

The checked-in `dev-sample-key` is local-development only. It is scoped to the
sample `Developer` identity and `DeployApplication` capability.
See [Integration API Keys](../security/integration-api-keys.md) for key scope
and direct HTTP authentication details.

## 3. Register and protect one action

Add this to `Program.cs`:

```csharp
using Seneschal.AspNetCore;
using Seneschal.Client;
using Seneschal.Client.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSeneschal(
    builder.Configuration.GetSection("Seneschal"));

var app = builder.Build();

app.MapPost("/deploy/{operationId}", async (
    string operationId,
    ISeneschalClient client,
    CancellationToken cancellationToken) =>
{
    var result = await client.EvaluateAsync(new DecisionRequest
    {
        Identity = "Developer",
        Capability = "DeployApplication",
        OperationId = operationId,
        Context = new()
        {
            ["environment"] = "dev",
            ["resource"] = "sample-api"
        }
    }, cancellationToken);

    if (!result.ShouldProceed)
    {
        return result.Guidance == ExecutionGuidanceKind.Pause
            ? Results.Accepted($"/operations/{operationId}", new
            {
                result.ApprovalId,
                result.Message
            })
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Replace this response with the governed work.
    return Results.Ok(new { executed = true, operationId });
});

app.Run();
```

`ShouldProceed` is the execution instruction. Do not combine Decision, runtime
mode, EffectiveAction, or approval status to make a separate execution choice.

## 4. Run

Start Seneschal from this repository, then start your service:

```powershell
dotnet run --project Seneschal.Api
dotnet run --project path\to\your-service --urls http://localhost:5010
```

Call the protected action:

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://localhost:5010/deploy/release-001
```

The local sample policy returns an executable result for `Developer` deploying
to `dev`. A block returns HTTP 403. Connection, authentication, and invalid
response failures are fail-closed by default.

## Approval behavior

When typed guidance is `Pause`, stop without executing and return or persist an
application-owned pending response. Keep the request payload in your service,
have an operator resolve the approval in Seneschal, then call `EvaluateAsync`
again with the same `OperationId`. Only execute when the retry returns
`ShouldProceed == true`.

Seneschal stores approval evidence, not your work payload, and does not pause,
poll, retry, or resume the operation for you. Rejected approvals and unknown
guidance fail closed.

## Optional automatic endpoint protection

If an endpoint does not need the decision object, the ASP.NET package can apply
the same canonical contract automatically:

```csharp
app.UseSeneschal();

app.MapPost("/deploy", () => Results.Ok(new { executed = true }))
    .RequireCapability("DeployApplication");
```

Automatic protection returns HTTP 403 for a block, HTTP 409 when approval is
required, and fail-closed error responses for unavailable or invalid runtime
responses. See the package README for identity mapping, environment metadata,
and explicit failure-policy overrides.

## Common failures

| Symptom | Check |
|---|---|
| Startup says `BaseUrl` is required or invalid | Configure an absolute Seneschal URL, including `http://` or `https://`. |
| Startup says `ApiKey` is required | Provide a non-empty scoped key through configuration or secrets. |
| HTTP 401 | The key is missing or unknown. |
| HTTP 403 before evaluation | The key is not scoped to the requested identity, capability, or environment. |
| HTTP 403 after evaluation | Guidance blocked the action. Inspect Decision and Reason for evidence. |
| HTTP 502/503 | The response was invalid, timed out, or Seneschal was unavailable; default behavior is fail closed. |
