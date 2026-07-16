# Seneschal Multi-Application Adoption Lab

This lab runs three independent .NET console applications against one local
Seneschal instance. Each worker uses the packaged `Seneschal.Client`, owns a
separately scoped API key, and requests a decision before every simulated
operation.

## Expected policy behavior

| Worker | Interval | Decision |
|---|---:|---|
| DeploymentWorker | 5 seconds | Allow |
| DatabaseMigrationWorker | 7 seconds | Deny |
| RefundWorker | 10 seconds | Allow |
| ApprovalWorker | 8 seconds | PendingApproval |

ApprovalWorker keeps one caller-owned `operationId` stable while approval is
pending. It creates the next operation ID only after the current operation is
allowed and completed.

In `LogOnly`, all four operations execute while migration displays its deny
and approval displays PendingApproval. In `Enforce`, deployment and refund
continue while migration and approval are blocked.

> **Enforce does not deny everything.** Global `Enforce` applies the decisions
> produced by existing policies. It blocks deny and pending-approval decisions
> while allowing operations whose policy result is allow.

## Prepare the local package

Run from the repository root:

```powershell
dotnet pack Seneschal.Client/Seneschal.Client.csproj -c Release
```

The lab's `NuGet.Config` restores `Seneschal.Client` from
`artifacts/packages`. The worker projects have no source project references.

## Start the lab

Run each command from the repository root.

Start Seneschal:

```powershell
Start-Process powershell -ArgumentList '-NoExit','-Command','dotnet run --project Seneschal.Api --urls http://localhost:5000'
```

Start DeploymentWorker in a separate terminal:

```powershell
Start-Process powershell -ArgumentList '-NoExit','-Command','dotnet run --project labs/multi-application-adoption/DeploymentWorker/DeploymentWorker.csproj'
```

Start DatabaseMigrationWorker in a separate terminal:

```powershell
Start-Process powershell -ArgumentList '-NoExit','-Command','dotnet run --project labs/multi-application-adoption/DatabaseMigrationWorker/DatabaseMigrationWorker.csproj'
```

Start RefundWorker in a separate terminal:

```powershell
Start-Process powershell -ArgumentList '-NoExit','-Command','dotnet run --project labs/multi-application-adoption/RefundWorker/RefundWorker.csproj'
```

Start ApprovalWorker in a separate terminal:

```powershell
dotnet run --project labs/multi-application-adoption/ApprovalWorker/ApprovalWorker.csproj
```

Open the portal:

```powershell
Start-Process http://localhost:5000/dashboard
```

## Change runtime governance

Set `LogOnly`:

```powershell
Invoke-WebRequest -Method Post -Uri 'http://localhost:5000/governance?handler=SetMode' -ContentType 'application/x-www-form-urlencoded' -Body 'mode=LogOnly' | Out-Null
```

Set `Enforce`:

```powershell
Invoke-WebRequest -Method Post -Uri 'http://localhost:5000/governance?handler=SetMode' -ContentType 'application/x-www-form-urlencoded' -Body 'mode=Enforce' | Out-Null
```

Or use the portal control:

```powershell
Start-Process http://localhost:5000/governance
```

## Observe activity

Open audit:

```powershell
Start-Process http://localhost:5000/audit
```

Open capability activity:

```powershell
Start-Process http://localhost:5000/capability-activity
```

Open individual capability profiles:

```powershell
Start-Process 'http://localhost:5000/capability-explorer?capabilityId=production.deployment.execute'
```

```powershell
Start-Process 'http://localhost:5000/capability-explorer?capabilityId=database.migration.execute'
```

```powershell
Start-Process 'http://localhost:5000/capability-explorer?capabilityId=payments.refund.create'
```

## Console output

Each attempt prints:

- Timestamp
- Identity and capability
- Decision and enforcement mode
- Effective action
- Matched policy and reason
- `operation=EXECUTED` or `operation=BLOCKED`

The workers fail closed when the Seneschal client cannot obtain a decision.

## Smoke-test controls

Workers run indefinitely by default. Automated local checks can shorten the
interval and bound the loop without changing normal behavior:

```powershell
$env:LAB_INTERVAL_SECONDS='1'; $env:LAB_MAX_ITERATIONS='2'; dotnet run --project labs/multi-application-adoption/DeploymentWorker/DeploymentWorker.csproj
```

`SENESCHAL_URL` overrides the default `http://localhost:5000` runtime URL.

## Global lockdown status

Seneschal currently has no emergency lockdown or deny-all override. The global
mode only selects whether normal policy decisions are observed or enforced.

The smallest future lockdown design needs:

1. A runtime control state separate from `LogOnly` and `Enforce`, containing
   `Active`, an explicit reason, activation time, and activating operator.
2. A check before normal policy evaluation that returns an immediate deny for
   every authenticated capability request while lockdown is active.
3. An administrative operation to activate and clear lockdown atomically.
4. Administrative audit events for activation and restoration.
5. A visible portal banner and diagnostics field showing the active reason.
6. Clearing lockdown restores the previous governance mode and normal policy
   evaluation without rewriting policies.

That control is intentionally not implemented by this lab.
