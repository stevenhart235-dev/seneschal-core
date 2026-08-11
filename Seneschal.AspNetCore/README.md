# Seneschal.AspNetCore

Capability governance middleware for ASP.NET Core. The package installs
`Seneschal.Client` transitively.

## Install

This package is not yet published to NuGet.org. After publication, install it
with:

```powershell
dotnet add package Seneschal.AspNetCore --version 0.1.0-alpha.1
```

For development-only local package validation, use an isolated generated feed:

```powershell
dotnet add package Seneschal.AspNetCore --source C:\path\to\isolated-feed --version 0.1.0-alpha.1
```

## Configure

Only `BaseUrl` and `ApiKey` are required:

`appsettings.json`:

```json
{
  "Seneschal": {
    "BaseUrl": "http://localhost:5077",
    "ApiKey": "<scoped-api-key>"
  }
}
```

Register once in `Program.cs`:

Before editing code, you can validate these values with:

```powershell
dotnet tool install --global Seneschal.Cli # available after publication
seneschal preflight --url http://localhost:5077 --api-key <scoped-api-key> --identity <identity> --capability <capability> --environment <environment>
```

```csharp
using Seneschal.AspNetCore;
using Seneschal.Client;
using Seneschal.Client.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSeneschal(
    builder.Configuration.GetSection("Seneschal"));

var app = builder.Build();
```

## Evaluate and execute

This is the canonical first integration because it makes the execution contract
visible:

```csharp
app.MapPost("/orders/{operationId}", async (
    string operationId,
    ISeneschalClient client,
    CancellationToken cancellationToken) =>
{
    var result = await client.EvaluateAsync(new DecisionRequest
    {
        Identity = "orders-api",
        Capability = "orders.submit",
        OperationId = operationId,
        Context = new() { ["resource"] = operationId }
    }, cancellationToken);

    if (!result.ShouldProceed)
    {
        return result.Guidance == ExecutionGuidanceKind.Pause
            ? Results.Accepted($"/operations/{operationId}")
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Execute the governed action only after ShouldProceed is true.
    return Results.Ok(new { executed = true, operationId });
});

app.Run();
```

Run the service normally with `dotnet run`. The required path is now install →
configure two values → register → evaluate → check `ShouldProceed` → execute.

For approval, stop the action when guidance is `Pause`, retain the application
work locally, and evaluate again with the same `OperationId` after approval.
Seneschal does not store or resume the work payload.

## Automatic endpoint protection

After the direct path works, endpoints that do not need the decision object can
use automatic protection:

```csharp
app.UseSeneschal();

app.MapPost("/deploy", () => Results.Ok(new { executed = true }))
    .RequireCapability("production.deployment.execute");
```

Advanced lambda configuration remains available:

```csharp
builder.Services.AddSeneschal(options =>
{
    options.BaseUrl = new Uri("http://localhost:5077");
    options.ApiKey = configuration["Seneschal:ApiKey"];
    options.DefaultEnvironment = "dev";
    options.FailureBehavior = SeneschalFailureBehavior.FailClosed;
    options.Timeout = TimeSpan.FromSeconds(10);
});
```

Startup fails immediately when `BaseUrl`, `ApiKey`, environment, or timeout is
invalid. API key values are not included in validation errors.

## Additional endpoint metadata

Fluent metadata:

```csharp
app.MapPost("/deploy", () => Results.Ok())
    .RequireCapability("production.deployment.execute");
```

Attribute metadata:

```csharp
[RequiresCapability("production.deployment.execute")]
static IResult Deploy() => Results.Ok();
```

An attribute can override the default environment and resource:

```csharp
[RequiresCapability(
    "production.deployment.execute",
    Environment = "production",
    ResourceId = "checkout-api")]
```

Identity defaults to the authenticated principal name, then `anonymous`.
Override it when application identity mapping differs:

```csharp
options.IdentityResolver = context =>
    context.User.FindFirst("subject_id")?.Value ?? "anonymous";
```

## Failure behavior

`FailClosed` is the default. It blocks when Seneschal is unavailable, times
out, rejects the integration, or returns an invalid response.

```csharp
options.FailureBehavior = SeneschalFailureBehavior.FailClosed;
```

`FailOpen` permits the endpoint when evaluation fails. Use it only when the
business operation is explicitly allowed to continue without governance.

```csharp
options.FailureBehavior = SeneschalFailureBehavior.FailOpen;
```

Failure responses contain only `decision`, `reason`, and `policyMatched`:

| Condition | Status |
|---|---:|
| Policy deny | 403 |
| Pending approval | 409 |
| Invalid or missing integration key | 401 |
| Integration key outside scope | 403 |
| Invalid Seneschal response | 502 |
| Timeout or unavailable API | 503 |

## Direct client decisions

`AddSeneschal` registers `ISeneschalClient`. Direct integrations can use the
existing fields or the convenience property:

```csharp
var decision = await client.EvaluateAsync(request, cancellationToken);

if (decision.ShouldProceed)
{
    await ExecuteAsync(cancellationToken);
}
```

Use `ExecutionGuidance` / `ShouldProceed` to determine whether to execute.
`Decision` describes the governance result; it is not by itself an execution
instruction. `ShouldProceed` is derived exclusively from guidance, so direct
callers do not need to inspect runtime mode. `decision.Guidance` exposes the
typed `ExecutionGuidanceKind`; `decision.RawExecutionGuidance` preserves the
server value when typed guidance is `Unknown`.

## Advanced compatibility

Existing lower-level APIs remain supported:

- Manual `SeneschalClientOptions` and `AddHttpClient` registration
- `UseSeneschalCapabilityAttributes(...)`
- `UseSeneschalCapability(...)`
- `SeneschalEnforcementBehavior`

The SDK does not discover capabilities or create policies automatically.
