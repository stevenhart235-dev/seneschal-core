# Seneschal.Client

`Seneschal.Client` is the .NET client for requesting capability decisions from
a running Seneschal API.

```csharp
using Seneschal.Client;

builder.Services.Configure<SeneschalClientOptions>(options =>
{
    options.BaseUrl = new Uri("http://localhost:5000");
    options.ApiKey = builder.Configuration["Seneschal:ApiKey"];
});
builder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();
```

This package requires .NET 8 or later and a reachable Seneschal runtime.

## Execution guidance

`Decision` is the governance outcome. `EffectiveAction` is the runtime-mode
projection retained for compatibility. `ExecutionGuidance` tells the caller
whether it should `Proceed`, `Block`, `Pause`, or `ContinueLogOnly`; the SDK
does not automatically execute, retry, poll, queue, or resume work.

Pending Approval responses include `ApprovalId`, `ApprovalStatus`, `Message`,
and `RetryGuidance` when available. Seneschal does not retain the original
application request payload.

Set `DecisionRequest.OperationId` to a caller-owned business-operation ID and
reuse it across retries. Production approval flows should not rely on legacy
context-only matching.
