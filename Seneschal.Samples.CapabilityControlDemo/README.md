# CapabilityControlDemo

This sample proves the scoped API key and capability evaluation loop with a
small autonomous platform-agent scenario.

The simulated agent wants to execute:

- identity: `platform-agent`
- capability: `infrastructure.production.apply`
- environment: `production`
- resource: `prod-subscription`

No real Terraform or infrastructure work is performed. The console app only
prints the decision Seneschal returns and the action it would have taken.

## Run the demo

From the repository root, start Seneschal.Api in one PowerShell terminal:

```powershell
Push-Location .\Seneschal.Api; dotnet run --urls http://localhost:5000; Pop-Location
```

In a second PowerShell terminal, run the demo:

```powershell
dotnet run --project .\Seneschal.Samples.CapabilityControlDemo\Seneschal.Samples.CapabilityControlDemo.csproj -- http://localhost:5000
```

## What the demo proves

The demo runs four integration scenarios:

1. Valid API key + matching allow policy returns `Decision: Allow`.
2. Valid API key + matching deny policy returns `Decision: Deny`; because
   Seneschal currently runs in monitor/log-only mode, the effective action is
   `logged_only` and the simulated integration would proceed while recording
   the decision.
3. Missing/invalid API key is rejected with `HTTP 401`.
4. Valid API key outside capability scope is rejected with `HTTP 403`.

## Expected output shape

```text
1. Allowed request with valid scoped API key and matching policy
  Result: Decision: Allow
  Enforcement Mode: LogOnly
  Effective Action: allow
  Application Behavior: Allowed; would apply infrastructure changes.

2. Denied request when policy does not allow it
  Result: Decision: Deny
  Enforcement Mode: LogOnly
  Effective Action: logged_only
  Application Behavior: Monitor mode records the denial, but the simulated integration would proceed.

3. Rejected request when integration API key is missing
  Result: HTTP 401 Unauthorized
  Application Behavior: Request rejected before policy evaluation; no action executed.

3b. Rejected request when integration API key is invalid
  Result: HTTP 401 Unauthorized
  Application Behavior: Request rejected before policy evaluation; no action executed.

4. Rejected request when API key is valid but not scoped for capability
  Result: HTTP 403 Forbidden
  Application Behavior: Request rejected before policy evaluation; no action executed.
```

## Demo keys

The sample keys live in
`Seneschal.Api/Policies/integration-keys.yaml` and are development-only.

- `dev-capability-control-key` can request the demo production
  infrastructure capabilities for `platform-agent`.
- `dev-capability-control-limited-key` is intentionally not scoped for
  `infrastructure.production.apply`, so the request is rejected before policy
  evaluation.
