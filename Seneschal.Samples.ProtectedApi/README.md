# Seneschal Protected API Sample

This sample shows three ways an ASP.NET Core application can ask Seneschal for
runtime capability decisions before executing protected work.

The sample exposes:

```text
POST /deploy
POST /deploy/attribute
POST /deploy/middleware
```

All three paths evaluate the `DeployApplication` capability against a running
Seneschal instance.

## Configure the Seneschal URL

By default, the sample calls:

```text
http://localhost:5000
```

Override it with configuration:

```powershell
$env:Seneschal__BaseUrl = "http://localhost:5000"
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

Start Seneschal:

```powershell
dotnet run --project ..\Seneschal.Api --urls http://localhost:5000
```

Start the protected sample API:

```powershell
dotnet run --project . --urls http://localhost:5010
```

## Manual client evaluation

`POST /deploy` demonstrates direct use of `ISeneschalClient`.

The endpoint builds a `DecisionRequest`, calls `EvaluateAsync`, and maps the
returned decision to an HTTP response itself.

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5010/deploy `
  -ContentType "application/json" `
  -Body '{"identity":"Developer","environment":"dev","resource":"sample-api"}'
```

This style is useful when the application needs full control over request
construction or custom response mapping.

## Attribute-based protection

`POST /deploy/attribute` demonstrates endpoint/controller metadata:

```csharp
[RequiresCapability("DeployApplication")]
```

The sample registers `UseSeneschalCapabilityAttributes()`, so endpoints with
`RequiresCapabilityAttribute` metadata are evaluated automatically before the
handler runs.

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5010/deploy/attribute
```

This style is useful when capability requirements should be declared near the
endpoint code.

## Middleware-based protection

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
- `PendingApproval` blocks the request and returns HTTP 409 with the reason and
  obligations.
- Monitor/log-only mode evaluates and audits the decision but allows the request
  to continue.

The default sample policies allow `Developer` to deploy to `dev`. Requests with
unmatched identity, capability, or environment values are expected to fall back
to deny behavior unless your local Seneschal configuration says otherwise.
