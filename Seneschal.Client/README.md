# Seneschal.Client

`Seneschal.Client` is the .NET client for requesting capability decisions from
a running Seneschal API.

## Install

```powershell
dotnet add package Seneschal.Client --version 0.1.0-alpha.1
```

You need two connection values: the Seneschal base URL and a scoped integration
API key. Keep the key in environment variables or your normal secret store.

## Configure

```csharp
using Seneschal.Client;

builder.Services.Configure<SeneschalClientOptions>(options =>
{
    options.BaseUrl = new Uri("http://localhost:5077");
    options.ApiKey = builder.Configuration["Seneschal:ApiKey"];
});
builder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();
```

This package requires .NET 8 or later and a reachable Seneschal runtime.

## Evaluate one action

```csharp
var result = await client.EvaluateAsync(new DecisionRequest
{
    Identity = "my-service",
    Capability = "orders.submit",
    OperationId = order.Id,
    Context = new() { ["resource"] = order.Id }
}, cancellationToken);

if (!result.ShouldProceed)
{
    return;
}

await SubmitOrderAsync(order, cancellationToken);
```

The API key must be scoped to the identity and capability sent in the request.
`OperationId` is required only when the action may enter an approval flow; keep
it stable when re-evaluating the same business operation.

## Execution guidance

Use `ExecutionGuidance` / `ShouldProceed` to determine whether to execute.
`Decision` describes the governance result; it is not by itself an execution
instruction. `EffectiveAction` remains available as diagnostic and compatibility
evidence. The SDK does not automatically execute, retry, poll, queue, or resume
work.

```csharp
var result = await client.EvaluateAsync(request, cancellationToken);

if (result.Guidance == ExecutionGuidanceKind.Pause)
{
    await SaveApprovalCheckpointAsync(result.ApprovalId, cancellationToken);
    return;
}

if (!result.ShouldProceed)
{
    return;
}

await DoWorkAsync(cancellationToken);
```

`Guidance` is the typed `ExecutionGuidanceKind` recognized by this SDK version.
`ExecutionGuidance` remains the source-compatible raw wire property, and
`RawExecutionGuidance` makes raw-value access explicit. This preserves an
unknown future server value for logging and diagnostics without failing JSON
deserialization.

Unknown, missing, null, and blank values map to
`ExecutionGuidanceKind.Unknown`. `ShouldProceed` is derived exclusively from
the typed guidance contract: `Proceed` and `ContinueLogOnly` return `true`;
every other typed state returns `false`. Callers do not need to inspect Decision
or runtime mode.

This SDK declares conformance with Execution Guidance contract `v1`, fixture
revision 1, through `ExecutionGuidanceContract.ConformanceVersion` and
`ConformanceRevision`. The version is a build-time/documentation declaration;
it does not add runtime negotiation or API headers. The shared fixture and
versioning policy live under `integrations/contracts/execution-guidance`.

Pending Approval responses include `ApprovalId`, `ApprovalStatus`, `Message`,
and `RetryGuidance` when available. Seneschal does not retain the original
application request payload.

Set `DecisionRequest.OperationId` to a caller-owned business-operation ID and
reuse it across retries. Production approval flows should not rely on legacy
context-only matching.
