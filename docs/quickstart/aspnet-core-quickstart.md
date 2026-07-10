# ASP.NET Core Quickstart

Run Seneschal locally, protect one ASP.NET Core endpoint, and verify
`LogOnly` versus `Enforce` behavior.

> **Current requirement:** `Seneschal.Client` and `Seneschal.AspNetCore` are
> source projects. They are not installed from published packages.

## 1. Prerequisites

- .NET 8 SDK
- This repository checked out locally
- PowerShell
- An ASP.NET Core project in or near this repository
- Ports `5000` and `5010` available

The test uses the checked-in development configuration:

- API key: `dev-sample-key`
- Identity: `anonymous`
- Capability: `DeployApplication`
- Environment: `dev`
- Expected decision: default `Deny`

## 2. Start Seneschal on port 5000

From the repository root:

```powershell
dotnet run --project Seneschal.Api --urls http://localhost:5000
```

In another terminal, verify readiness:

```powershell
Invoke-RestMethod http://localhost:5000/ready
```

Seneschal starts in `LogOnly` mode.

## 3. Verify the development integration key

Confirm `Seneschal.Api/Policies/integration-keys.yaml` contains this scope:

```yaml
integrationKeys:
  - name: sample-protected-api
    key: dev-sample-key
    enabled: true
    allowedIdentities:
      - Developer
      - UnknownService
      - anonymous
    allowedCapabilities:
      - DeployApplication
```

Restart Seneschal after any YAML change.

> **Development only:** The checked-in key is plaintext sample configuration.
> Do not use it in production.

## 4. Reference and register the client

From the repository root, set the application project path and add both
references:

```powershell
$appProject = "MyApi/MyApi.csproj"
dotnet add $appProject reference Seneschal.Client/Seneschal.Client.csproj
dotnet add $appProject reference Seneschal.AspNetCore/Seneschal.AspNetCore.csproj
```

Add these namespaces to `Program.cs`:

```csharp
using Seneschal.AspNetCore;
using Seneschal.Client;
```

Register the client before `builder.Build()`:

```csharp
builder.Services.Configure<SeneschalClientOptions>(options =>
{
    options.BaseUrl = new Uri("http://localhost:5000");
    options.ApiKey = "dev-sample-key";
});
builder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();
```

The inline key keeps this quickstart short. Move it to configuration or a
development secret for normal use.

## 5. Protect one endpoint

Register middleware after routing and map an attributed handler:

```csharp
var app = builder.Build();

app.UseRouting();
app.UseSeneschalCapabilityAttributes();

app.MapPost("/governed-operation", GovernedOperation);

app.Run();

[RequiresCapability(
    "DeployApplication",
    Environment = "dev",
    ResourceId = "quickstart-api")]
static IResult GovernedOperation()
{
    return Results.Ok(new { executed = true });
}
```

With no ASP.NET Core authentication configured, middleware submits identity
`anonymous`. No policy allows that identity, so Seneschal returns default deny.

Start the application on port `5010`:

```powershell
$appProject = "MyApi/MyApi.csproj"
dotnet run --project $appProject --urls http://localhost:5010
```

## 6. Test in LogOnly mode

Confirm `LogOnly` at:

```text
http://localhost:5000/governance
```

Call the protected endpoint:

```powershell
curl.exe -i -X POST http://localhost:5010/governed-operation
```

Expected result:

```text
HTTP/1.1 200 OK
{"executed":true}
```

Seneschal records a default `Deny`, but `LogOnly` allows the handler to run.
Inspect `/audit` or `/capability-activity` on port `5000`.

## 7. Switch runtime governance to Enforce

Open:

```text
http://localhost:5000/governance
```

Select **Enforce**. No application restart or policy change is required.

## 8. Rerun and verify blocking

Run the same request:

```powershell
curl.exe -i -X POST http://localhost:5010/governed-operation
```

Expected result:

```text
HTTP/1.1 403 Forbidden
```

The response contains the deny decision and reason. The endpoint handler does
not run; `/audit` records the decision with `Enforce` mode.

## 9. Troubleshooting

### 401 from Seneschal

- Cause: missing, blank, or unknown `X-Seneschal-Api-Key`.
- Check `options.ApiKey` and `integration-keys.yaml` for an exact match.
- Restart Seneschal after changing YAML.

### 403 before policy evaluation

- Cause: the key is disabled or does not allow the submitted identity,
  capability, or environment.
- For this quickstart, allow `anonymous` and `DeployApplication`.
- Key-scope denial says: `The Seneschal API key is not authorized.`

### 403 after switching to Enforce

- This is the expected policy result for the quickstart.
- Confirm `/audit` shows `Deny`, `Enforce`, and the default-deny reason.

### Unexpected default deny

- Default deny means no allow policy matched the submitted context.
- Check exact identity, capability, environment, and resource values.
- This quickstart uses default deny intentionally.

### Wrong port or unavailable runtime

- Seneschal URL: `http://localhost:5000`
- Application URL: `http://localhost:5010`
- Ensure `SeneschalClientOptions.BaseUrl` points to port `5000`.
- Verify Seneschal directly with
  `Invoke-RestMethod http://localhost:5000/ready`.

## Success looks like

- [ ] Seneschal reports ready on port `5000`.
- [ ] The application runs on port `5010`.
- [ ] The integration key permits `anonymous` and `DeployApplication`.
- [ ] `LogOnly` returns HTTP `200` and executes the handler.
- [ ] Audit shows an underlying default `Deny` in `LogOnly` mode.
- [ ] `Enforce` returns HTTP `403` for the same request.
- [ ] Audit shows the second `Deny` in `Enforce` mode.
- [ ] No application or policy change was needed between the two calls.
