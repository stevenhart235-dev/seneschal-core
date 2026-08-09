# Seneschal CLI

## Integration preflight

Validate an endpoint, scoped API key, catalog identifiers, and the Execution
Guidance contract without executing work or creating governance state:

```powershell
dotnet run --project Seneschal.Cli -- preflight `
  --url http://localhost:5077 `
  --api-key dev-sample-key `
  --identity Developer `
  --capability DeployApplication `
  --environment dev
```

If installed or published as the `seneschal` command, the equivalent is:

```text
seneschal preflight --url <url> --api-key <key> --identity <id> --capability <id> --environment <name>
```

The API key is never printed. Deny and approval-required decisions are valid
governance results: preflight reports `Integration: Ready` while showing whether
execution would proceed or stop. Endpoint, readiness, authentication, catalog,
scope, and malformed-guidance failures return a non-zero exit code.

Preflight uses the read-only `/preflight` endpoint. It does not execute the
governed action or create approvals, audit evidence, incidents, policies,
runtime-mode changes, or governance-window changes.
