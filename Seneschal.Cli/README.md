# Seneschal CLI

This tool is not yet published to NuGet.org. After publication, install it with:

```powershell
dotnet tool install --global Seneschal.Cli
```

## Policy validation

Validate policy structure and references before simulation or enforcement:

    seneschal policy validate .\Policies\policies.yaml

Validation parses YAML, applies
[Policy Schema v1](../integrations/contracts/policy/README.md), loads the
existing policy model, and then validates against `identities.yaml` and
`capabilities.yaml` in the same directory. Schema-valid does not mean
operationally valid: duplicate IDs and unknown catalog references are checked
after structural validation. Validation is read-only. Errors return a non-zero
exit code; warnings are displayed but do not fail validation.

Schema-aware YAML editors can associate the versioned contract through their
normal schema mapping, or with this portable modeline after adjusting the
relative path:

    # yaml-language-server: $schema=../integrations/contracts/policy/policy-schema.v1.json

A valid policy entry:

    policies:
      - name: production-deployment
        identity: deployment-worker
        capability: production.deployment.execute
        environment: production
        decision: allow
        reason: Approved release automation may deploy to production

A failing entry with an unknown capability:

    policies:
      - name: invalid-production-deployment
        identity: deployment-worker
        capability: production.deployment.missing
        environment: production
        decision: allow
        reason: Production deployment

Use the authoring workflow in order: validate configuration, simulate a
representative request, observe the result in non-enforcing operation, then
enable enforcement through the existing runtime governance controls.

## Integration preflight

Validate an endpoint, scoped API key, catalog identifiers, and the Execution
Guidance contract without executing work or creating governance state:

```powershell
seneschal preflight `
  --url http://localhost:5077 `
  --api-key <key> `
  --identity Developer `
  --capability DeployApplication `
  --environment dev
```

The compact form is:

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

## Policy simulation

Explain the policy outcome for a specific request without executing or recording
it:

```powershell
seneschal policy simulate `
  --url http://localhost:5077 `
  --api-key <key> `
  --identity deployment-worker `
  --capability production.deployment.execute `
  --environment production `
  --resource northwind-api
```

Simulation is a thin presenter over the same non-mutating `/preflight` endpoint.
It displays request identity, capability, environment and resource; the decision,
effective action, canonical Execution Guidance and `ShouldProceed` result; every
matched policy; the reason and approval status; and any matching governance
window's name, mode, reason, and influence on the result.

Allow, Deny, and RequireApproval are all successful simulation outcomes. Whether
the hypothetical caller would execute is derived only from the canonical
Execution Guidance contract. Unknown guidance returns a non-zero exit code and
fails closed. Simulation creates no audit, approval, activity, metric, incident,
runtime-mode, policy, or governance-window state.

## Versions and local package testing

Pin a published version when reproducibility matters:

```powershell
dotnet tool install --global Seneschal.Cli --version 0.1.0-alpha.1
```

To test a package locally without publishing it to NuGet, pack the project and
use the generated package directory as a source:

```powershell
dotnet pack Seneschal.Cli/Seneschal.Cli.csproj -c Release
dotnet tool install --global Seneschal.Cli `
  --add-source artifacts/packages `
  --version 0.1.0-alpha.1
```

Uninstall the tool with:

```powershell
dotnet tool uninstall --global Seneschal.Cli
```

Publishing `Seneschal.Cli` to NuGet is a separate release concern; local
packaging and installation do not publish anything.
