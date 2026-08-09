# Approval Execution Guidance

Execution guidance is Seneschal's canonical machine-readable execution
contract. It does not cause Seneschal to pause, queue, resume, or retry an
external operation.

| Decision | Runtime mode | Guidance | Existing effective action |
|---|---|---|---|
| Allow | LogOnly or Enforce | `Proceed` | Allow |
| Deny | LogOnly | `ContinueLogOnly` | Logged only |
| Deny | Enforce | `Block` | Deny |
| Pending Approval | LogOnly | `ContinueLogOnly` | Logged only |
| Pending Approval | Enforce | `Pause` | Pending Approval |

Use `ExecutionGuidance` / `ShouldProceed` to determine whether to execute.
`Decision` describes the governance result; it is not by itself an execution
instruction. `EffectiveAction` remains useful diagnostic and compatibility
evidence. Callers do not need to interpret runtime mode.

| Guidance | `ShouldProceed` |
|---|---:|
| `Proceed` | `true` |
| `ContinueLogOnly` | `true` |
| `Block` | `false` |
| `Pause` | `false` |
| `Queue` | `false` |
| `Retry` | `false` |
| Missing, unknown, or unsupported | `false` |

## Typed .NET guidance

`Seneschal.Client` exposes the known values through `result.Guidance`, an
`ExecutionGuidanceKind`. Use the typed state for guidance-specific handling and
`ShouldProceed` for the execution decision:

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

The existing `ExecutionGuidance` property retains the raw server string for
source compatibility. `RawExecutionGuidance` is an explicit nullable alias for
diagnostics. A future value unknown to the installed SDK deserializes normally,
is preserved raw, maps to `ExecutionGuidanceKind.Unknown`, and fails closed.
Missing, null, and blank values behave the same way.

## Caller responsibilities

- **Synchronous API:** return `202 Accepted`, stop the current execution, and
  retry evaluation after approval. The status URL is owned by the application.
- **Pipeline:** fail or pause using facilities provided by the pipeline
  platform, then retry after approval. GitHub-hosted runners are not kept alive.
- **Worker or agent:** queue or checkpoint work locally and resume only after a
  new evaluation returns Allow.

Approval records contain governance scope and resolution evidence. They do not
preserve the application's original request payload.

## Approval correlation

Production approval requests should always supply a caller-owned `operationId`
and reuse it across evaluations of the same business operation. An approval is
scoped to identity, capability, environment, resource, and operation ID.

Requests that omit `operationId` temporarily use `LegacyContext` matching over
identity, capability, environment, and resource. Legacy requests never match
an operation-scoped approval.

An approved record is consumed when it resolves one matching evaluation to
Allow. Governance Windows evaluate afterward; if a window overrides that Allow,
the approval remains consumed because it was used during decision resolution.

The `Retry` and `Queue` values are reserved for explicit future caller guidance;
the current default mapping does not emit them.
