# Seneschal CLI

This tool is not yet published to NuGet.org. After publication, install it with:

```powershell
dotnet tool install --global Seneschal.Cli
```

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
