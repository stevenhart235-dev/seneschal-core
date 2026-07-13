# Terraform/OpenTofu governance-gate proof

This integration proves that a Terraform or OpenTofu workflow can call Seneschal after planning and before applying. It is a pre-apply governance gate, not a Terraform provider or backend.

## Prerequisites

- Seneschal running locally with `./demo.ps1`
- PowerShell
- Terraform 1.5+ or a compatible OpenTofu release

The example uses the built-in `terraform_data` resource and requires no cloud credentials or provider downloads.

## Terraform flow

Run from the repository root:

```powershell
terraform -chdir=integrations/terraform/examples/production-apply init
terraform -chdir=integrations/terraform/examples/production-apply plan -out=tfplan

powershell -File integrations/terraform/invoke-seneschal-gate.ps1 `
  -BaseUrl http://localhost:5000 `
  -ApiKey dev-terraform-production-key `
  -Identity terraform-production `
  -Capability infrastructure.production.apply `
  -Environment production `
  -Resource prod-subscription `
  -PlanFile integrations/terraform/examples/production-apply/tfplan

if ($LASTEXITCODE -eq 0) {
  terraform -chdir=integrations/terraform/examples/production-apply apply tfplan
}
```

Replace `terraform` with `tofu` for the equivalent OpenTofu flow.

## Deny and pending-approval checks

The repository already contains scenarios usable with the same gate:

- Deny: `platform-agent`, `infrastructure.production.destroy`, `dev-capability-control-key`
- Pending approval: `release-approval-worker`, `production.release.approve`, `dev-release-approval-worker-key`

Expected behavior:

- **Allow:** exit `0`; apply may run.
- **Deny in LogOnly:** exit `0`; output states that the deny was observed but not enforced; apply may run.
- **Deny in Enforce:** exit `1`; apply must not run.
- **Pending Approval in Enforce:** exit `1`; apply must not run.
- **Invalid key or unavailable runtime:** exit `2`; apply must not run.
- **Missing plan file:** exit `3` before Seneschal is called.

When `-PlanFile` is supplied, the wrapper verifies that the file exists and prints only its filename and byte size. It does not parse or upload the plan.

## Current limitations

- Seneschal evaluates the supplied identity, capability, environment, and resource; it does not understand Terraform plan semantics.
- Plan contents and sensitive values are not inspected.
- The checked-in API key is development-only.
- The caller is responsible for running `apply` only after exit code `0`.
- Runtime mode is in memory and resets to `LogOnly` when Seneschal restarts.
