# Production Freeze Demo

## Demo objective

This 5–10 minute demo shows Seneschal governing production capability requests through its current runtime model. It demonstrates that Seneschal can:

- Observe production capability requests before an operation runs.
- Move from `LogOnly` observation to `Enforce` behavior.
- Block high-risk operations when policy returns `Deny` or `PendingApproval`.
- Surface decision evidence in the Dashboard, activity views, and Audit Trail.
- Apply the same identity, capability, environment, and resource model to workers, CI/CD, and infrastructure automation.

> **Important:** `Enforce` does not create a freeze and does not turn existing Allow decisions into Deny decisions. For this demo, the production freeze is a presenter-controlled policy configuration loaded when Seneschal starts.

## Audience

This demo is intended for:

- Mature platform teams
- Site reliability engineering teams
- Cloud security teams
- Release engineering teams
- Platform leadership
- Teams responsible for high-risk production operations

## Current components used

The walkthrough uses only components currently present in the repository:

- Seneschal runtime
- Dashboard
- Runtime Governance
- Audit Trail
- Capability Activity
- Identity Activity
- GitHub Actions governance gate
- Terraform/OpenTofu governance gate
- Deployment, database migration, refund, and release approval workers
- `LogOnly` and `Enforce` runtime modes

## Scenario

A production freeze is simulated through the repository's demo-only policy profile.

- GitHub Actions requests `production.deployment.execute` for `checkout-api`.
- Terraform/OpenTofu requests `infrastructure.production.apply` for `prod-subscription`.
- The database migration worker continuously requests `database.migration.execute`.
- The release approval worker continuously produces `PendingApproval` for `production.release.approve`.

There is no scheduled activation. The presenter selects the alternate policy file before startup, then uses Runtime Governance to demonstrate the operational difference between observing its decisions and enforcing them.

## Presenter preparation: production-freeze profile

`Seneschal.Api/Policies/policies.production-freeze.yaml` contains two highest-priority Deny policies for the GitHub Actions deployment and Terraform production apply. It also retains the normal worker policies, so the four-worker demonstration behaves as it does under the default profile.

Select the alternate file through the existing ASP.NET Core configuration-path environment variable, then launch the normal demo:

```powershell
$env:Seneschal__Configuration__PoliciesPath='Policies/policies.production-freeze.yaml'
.\demo.ps1
```

The environment variable is inherited by the Seneschal API process. It does not modify `policies.yaml`.

## Step-by-step walkthrough

### 1. Start the local runtime and workers

From the repository root, with the freeze-profile environment variable set as shown above:

```powershell
.\demo.ps1
```

Open the Dashboard if it is not already visible:

```text
http://localhost:5000/dashboard
```

**Presenter:** “The four workers produce a continuous mix of Allow, Deny, and Pending Approval decisions. The same runtime will now evaluate CI/CD and infrastructure requests.”

### 2. Confirm LogOnly

Open Runtime Governance:

```text
http://localhost:5000/governance
```

Confirm the canonical mode is `LogOnly`.

**Presenter:** “LogOnly provides evidence before enforcement. A denied request is visible and audited, but an integrated caller is told to proceed.”

### 3. Run the GitHub Actions gate locally

```powershell
powershell -File integrations/github-actions/invoke-seneschal-gate.ps1 `
  -BaseUrl http://localhost:5000 `
  -ApiKey dev-github-actions-key `
  -Identity github-actions-production `
  -Capability production.deployment.execute `
  -Environment production `
  -Resource checkout-api
```

Expected result in the controlled freeze configuration: `Decision: deny`, `Enforcement mode: LogOnly`, and exit code `0`.

**Presenter:** “The deployment capability is denied by freeze policy, but LogOnly records the decision without stopping the simulated workflow.”

### 4. Create a local Terraform/OpenTofu plan

Terraform:

```powershell
terraform -chdir=integrations/terraform/examples/production-apply init
terraform -chdir=integrations/terraform/examples/production-apply plan -out=tfplan
```

OpenTofu uses the same configuration:

```powershell
tofu -chdir=integrations/terraform/examples/production-apply init
tofu -chdir=integrations/terraform/examples/production-apply plan -out=tfplan
```

The example contains only a local `terraform_data` resource and requires no cloud credentials.

### 5. Run the Terraform/OpenTofu gate locally

```powershell
powershell -File integrations/terraform/invoke-seneschal-gate.ps1 `
  -BaseUrl http://localhost:5000 `
  -ApiKey dev-terraform-production-key `
  -Identity terraform-production `
  -Capability infrastructure.production.apply `
  -Environment production `
  -Resource prod-subscription `
  -PlanFile integrations/terraform/examples/production-apply/tfplan
```

Expected result in the controlled freeze configuration: `Decision: deny`, `Enforcement mode: LogOnly`, and exit code `0`.

To demonstrate conditional apply in LogOnly:

```powershell
if ($LASTEXITCODE -eq 0) {
  terraform -chdir=integrations/terraform/examples/production-apply apply tfplan
}
```

Use `tofu` instead of `terraform` for the OpenTofu equivalent.

**Presenter:** “Seneschal receives only governance context and safe plan metadata remains local. It does not parse or upload the Terraform plan.”

### 6. Review the evidence

Open these portal views:

- Dashboard: `http://localhost:5000/dashboard`
- Capability Activity: `http://localhost:5000/capability-activity`
- GitHub identity activity: `http://localhost:5000/identity-activity?identityId=github-actions-production`
- Terraform identity activity: `http://localhost:5000/identity-activity?identityId=terraform-production`
- Audit Trail: `http://localhost:5000/audit`

Show the GitHub, Terraform, database migration, and release approval evaluations.

**Presenter:** “Identity authorization answers who the caller is. Capability governance also records what privileged operation was requested, where, against which resource, under which policy, and with what outcome.”

### 7. Switch to Enforce

At `http://localhost:5000/governance`, choose **Switch to Enforce** and confirm the dialog.

**Presenter:** “The policies have not changed. Enforce changes the consequence of their Deny and Pending Approval decisions for integrated callers.”

### 8. Rerun the same GitHub and Terraform gates

Run the commands from steps 3 and 5 again.

Expected results:

- GitHub deployment gate exits `1`; the deployment step does not run.
- Terraform/OpenTofu gate exits `1`; `apply` must not run.

Use the same conditional apply pattern to make the boundary explicit:

```powershell
if ($LASTEXITCODE -eq 0) {
  terraform -chdir=integrations/terraform/examples/production-apply apply tfplan
} else {
  Write-Host 'Apply blocked by Seneschal.'
}
```

**Presenter:** “The workflow integration treats the decision as a pre-operation gate. Under Enforce, the same denied request now stops before the privileged operation.”

### 9. Show Deny and Pending Approval evidence

Return to the Dashboard and Audit Trail. Show:

- Denied GitHub deployment and Terraform apply evaluations.
- The blocked database migration worker.
- The release approval worker blocked pending approval.
- Matched policy, runtime mode, effective action, and reason in the evidence.

**Presenter:** “Pending Approval is visible and blocks under Enforce, but Seneschal does not yet provide an approval-resolution workflow.”

### 10. Return to LogOnly and stop

At `http://localhost:5000/governance`, choose **Return to LogOnly** and confirm.

Then stop the local processes:

```powershell
.\stop-demo.ps1
```

Clear the profile selection before a later normal-profile launch:

```powershell
Remove-Item Env:Seneschal__Configuration__PoliciesPath -ErrorAction SilentlyContinue
```

**Presenter:** “Returning to LogOnly restores observation behavior without rewriting policy. The mode itself is currently in memory and resets when Seneschal restarts.”

## Expected outcomes

| Operation | LogOnly | Enforce |
|---|---|---|
| GitHub Actions production deployment | Freeze-policy Deny is recorded; gate exits `0`; simulated deployment may continue | Deny is enforced; gate exits `1`; deployment does not run |
| Terraform/OpenTofu production apply | Freeze-policy Deny is recorded; gate exits `0`; apply may run | Deny is enforced; gate exits `1`; apply does not run |
| Database migration worker | Deny is recorded; simulated migration executes | Deny is enforced; simulated migration is blocked |
| Release approval worker | Pending Approval is recorded; simulated operation executes | Pending Approval is enforced; operation is blocked pending approval |

## What is simulated versus implemented

### Implemented today

- Runtime decisions for Allow, Deny, and Pending Approval
- GitHub Actions pre-operation gate
- Terraform/OpenTofu pre-apply gate
- `LogOnly` and `Enforce` modes
- Portal visibility and live Dashboard polling
- Audit, capability activity, and identity activity evidence
- Package-based demo workers

### Simulated or not implemented

- The “production freeze” is a manually selected demo policy profile, not a scheduled window.
- Scheduled governance windows are not implemented.
- Change-request-linked exception tokens are not implemented.
- Break-glass or short-lived exception behavior is not implemented.
- Approval resolution is not implemented.
- Runtime mode and operational evidence are not durably persisted.
- Administrative authorization for mode changes is not implemented.

## Future direction

The future form of this scenario could define a production freeze from Friday at 5 PM through Monday at 9 AM. It would be time-zone-aware and persistent, with a narrow exception tied to an approved change request, short-lived scoped authorization, and complete administrative audit evidence.

This is future direction only. None of these scheduling, exception, persistence, or approval capabilities are part of the current demo.

## Demo readiness checklist

- [ ] A clean clone builds and tests successfully.
- [ ] `Seneschal__Configuration__PoliciesPath` selects `Policies/policies.production-freeze.yaml`.
- [ ] `.\demo.ps1` succeeds.
- [ ] All four workers appear active.
- [ ] The GitHub Actions gate runs with the scoped development key.
- [ ] Terraform or OpenTofu initializes, plans, and runs the gate.
- [ ] Dashboard polling continues while commands run.
- [ ] Both `LogOnly` and `Enforce` outcomes have been rehearsed.
- [ ] Dashboard, Runtime Governance, Capability Activity, Identity Activity, and Audit Trail links resolve.
- [ ] `.\stop-demo.ps1` removes the tracked demo processes.
