# Seneschal Protected API Sample

This sample's canonical path is `POST /deploy`: evaluate, check
`ShouldProceed`, then execute or stop. The attribute and middleware endpoints
remain as optional examples after the direct path is working.

The sample exposes:

```text
POST /deploy
POST /deploy/attribute
POST /deploy/middleware
```

All three paths evaluate the `DeployApplication` capability against a running
Seneschal instance.

## Configure

By default, the sample calls:

```text
http://localhost:5077
```

Override it with configuration:

```powershell
$env:Seneschal__BaseUrl = "http://localhost:5077"
```

The sample sends the development integration API key configured in
`appsettings.json`:

```json
{
  "Seneschal": {
    "ApiKey": "dev-sample-key"
  }
}
```

When calling Seneschal directly, integrations must send:

```text
X-Seneschal-Api-Key: dev-sample-key
```

## Run both applications

Start Seneschal from the repository root:

```powershell
dotnet run --project Seneschal.Api
```

Start the protected sample API:

```powershell
dotnet run --project . --urls http://localhost:5010
```

## Canonical direct evaluation

`POST /deploy` demonstrates direct use of `ISeneschalClient`.

The endpoint builds a `DecisionRequest`, calls `EvaluateAsync`, and uses
`ShouldProceed` as the execution instruction. Decision and runtime mode are
diagnostic fields, not execution instructions.

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5010/deploy `
  -ContentType "application/json" `
  -Body '{"identity":"Developer","environment":"dev","resource":"sample-api"}'
```

For approval-sensitive work, provide a stable `operationId`, stop when guidance
is `Pause`, and retry evaluation with that same ID after approval. Seneschal
does not retain or resume the application's work payload.

## Optional attribute-based protection

`POST /deploy/attribute` demonstrates endpoint/controller metadata:

```csharp
[RequiresCapability("DeployApplication")]
```

The sample registers `UseSeneschal()`, so endpoints with
`RequiresCapabilityAttribute` metadata are evaluated automatically before the
handler runs.

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5010/deploy/attribute
```

This style is useful when capability requirements should be declared near the
endpoint code.

## Optional middleware-based protection

`POST /deploy/middleware` demonstrates path-level protection with
`UseSeneschalCapability(...)`.

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5010/deploy/middleware
```

This style is useful when an entire branch of the pipeline should require the
same capability.

## Expected decision behavior

For all three integration styles:

- `Allow` continues the request and returns HTTP 200.
- `Deny` blocks the request and returns HTTP 403.
- Approval-required guidance stops execution. The direct endpoint can return an
  application-owned pending response; automatic protection returns HTTP 409.
- Monitor/log-only mode evaluates and audits the decision but allows the request
  to continue.

The default sample policies allow `Developer` to deploy to `dev`. Requests with
unmatched identity, capability, or environment values are expected to fall back
to deny behavior unless your local Seneschal configuration says otherwise.
