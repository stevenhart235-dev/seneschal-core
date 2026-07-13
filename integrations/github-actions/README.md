# GitHub Actions integration proof

This integration proves that a GitHub Actions job can call Seneschal before a production deployment step and use the returned decision and runtime mode as a governance gate.

## Required repository secrets

- `SENESCHAL_URL` — a URL reachable from the GitHub-hosted or self-hosted runner.
- `SENESCHAL_API_KEY` — a key scoped to the workflow identity and capability.

See [`examples/production-deployment.yml`](examples/production-deployment.yml) for a Windows runner workflow. The deployment step runs only when the gate exits successfully.

## Local validation

Start Seneschal with `./demo.ps1`, then run the allow scenario from the repository root:

```powershell
powershell -File integrations/github-actions/invoke-seneschal-gate.ps1 `
  -BaseUrl http://localhost:5000 `
  -ApiKey dev-github-actions-key `
  -Identity github-actions-production `
  -Capability production.deployment.execute `
  -Environment production `
  -Resource checkout-api
```

To exercise a configured deny through the same gate script:

```powershell
powershell -File integrations/github-actions/invoke-seneschal-gate.ps1 `
  -BaseUrl http://localhost:5000 `
  -ApiKey dev-migration-worker-key `
  -Identity migration-worker `
  -Capability database.migration.execute `
  -Environment production `
  -Resource customer-db
```

Expected behavior:

- **Allow:** exit code `0`; the following deployment step runs.
- **Deny in LogOnly:** exit code `0`; the deny is recorded and the deployment step runs.
- **Deny in Enforce:** exit code `1`; the workflow stops before deployment.
- **Pending Approval in Enforce:** exit code `1`; the workflow stops before deployment.
- **Authentication or runtime failure:** exit code `2`; the workflow fails closed.

Switch modes at `http://localhost:5000/governance`. The script never writes the API key to output.

## Current limitations

- This is a repository script and workflow example, not a Marketplace action.
- The sample uses a checked-in development key; production key management is not implemented.
- GitHub OIDC authentication is not implemented.
- The runner must have network access to Seneschal.
- Runtime mode is in memory and resets to `LogOnly` when Seneschal restarts.
