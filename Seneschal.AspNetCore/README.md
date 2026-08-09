# Seneschal.AspNetCore

Capability governance middleware for ASP.NET Core. The package installs
`Seneschal.Client` transitively.

## Install

```powershell
dotnet add package Seneschal.AspNetCore --version 0.1.0-alpha.1
```

For local packages, add the generated feed first:

```powershell
dotnet nuget add source C:\path\to\seneschal-core\artifacts\packages --name SeneschalLocal
```

## Configure

`appsettings.json`:

```json
{
  "Seneschal": {
    "BaseUrl": "http://localhost:5000",
    "ApiKey": "dev-sample-key",
    "DefaultEnvironment": "dev",
    "FailureBehavior": "FailClosed",
    "Timeout": "00:00:10"
  }
}
```

`Program.cs`:

```csharp
using Seneschal.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSeneschal(
    builder.Configuration.GetSection("Seneschal"));

var app = builder.Build();

app.UseSeneschal();

app.MapPost("/deploy", () => Results.Ok(new { executed = true }))
    .RequireCapability("production.deployment.execute");

app.Run();
```

Lambda configuration remains available:

```csharp
builder.Services.AddSeneschal(options =>
{
    options.BaseUrl = new Uri("http://localhost:5000");
    options.ApiKey = configuration["Seneschal:ApiKey"];
    options.DefaultEnvironment = "dev";
    options.FailureBehavior = SeneschalFailureBehavior.FailClosed;
    options.Timeout = TimeSpan.FromSeconds(10);
});
```

Startup fails immediately when `BaseUrl`, `ApiKey`, environment, or timeout is
invalid. API key values are not included in validation errors.

## Protect endpoints

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
callers do not need to inspect runtime mode.

## Advanced compatibility

Existing lower-level APIs remain supported:

- Manual `SeneschalClientOptions` and `AddHttpClient` registration
- `UseSeneschalCapabilityAttributes(...)`
- `UseSeneschalCapability(...)`
- `SeneschalEnforcementBehavior`

The SDK does not discover capabilities or create policies automatically.
