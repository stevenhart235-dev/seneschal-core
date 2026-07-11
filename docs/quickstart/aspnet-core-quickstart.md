# ASP.NET Core Quickstart

Install the packaged SDK, protect one endpoint, and verify `LogOnly` versus
`Enforce` in about 15 minutes.

## 1. Pack and start Seneschal

From the repository root:

```powershell
dotnet pack Seneschal.AspNetCore/Seneschal.AspNetCore.csproj -c Release
dotnet run --project Seneschal.Api --urls http://localhost:5000
```

Verify readiness in another terminal:

```powershell
Invoke-RestMethod http://localhost:5000/ready
```

Seneschal starts in `LogOnly`.

## 2. Install the local package

In a .NET 8 ASP.NET Core application:

```powershell
dotnet nuget add source C:\path\to\seneschal-core\artifacts\packages --name SeneschalLocal
dotnet add package Seneschal.AspNetCore --version 0.1.0-alpha.1
```

`Seneschal.Client` resolves transitively.

## 3. Add configuration

`appsettings.json`:

```json
{
  "Seneschal": {
    "BaseUrl": "http://localhost:5000",
    "ApiKey": "dev-sample-key",
    "DefaultEnvironment": "dev",
    "FailureBehavior": "FailClosed"
  }
}
```

The sample key permits identity `anonymous` and capability
`DeployApplication`.

## 4. Register and protect

`Program.cs`:

```csharp
using Seneschal.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSeneschal(
    builder.Configuration.GetSection("Seneschal"));

var app = builder.Build();

app.UseSeneschal();

app.MapPost("/governed-operation", () =>
        Results.Ok(new { executed = true }))
    .RequireCapability("DeployApplication");

app.Run();
```

Attribute style is equivalent:

```csharp
[RequiresCapability("DeployApplication")]
static IResult GovernedOperation() =>
    Results.Ok(new { executed = true });
```

Run on port `5010`:

```powershell
dotnet run --urls http://localhost:5010
```

## 5. Verify LogOnly

```powershell
curl.exe -i -X POST http://localhost:5010/governed-operation
```

Expected:

```text
HTTP/1.1 200 OK
{"executed":true}
```

The underlying default deny appears in
`http://localhost:5000/audit`, but `LogOnly` permits execution.

## 6. Verify Enforce

Open `http://localhost:5000/governance` and select **Enforce**, then repeat:

```powershell
curl.exe -i -X POST http://localhost:5010/governed-operation
```

Expected:

```text
HTTP/1.1 403 Forbidden
{"decision":"deny","reason":"No matching allow policy found","policyMatched":"default-deny"}
```

## Troubleshooting

### Startup validation failure

- `BaseUrl is required`: add `Seneschal:BaseUrl`.
- `BaseUrl must be an absolute URI`: include `http://` or `https://`.
- `ApiKey is required`: configure a non-empty integration key.

Validation errors never print the configured key value.

### HTTP 401: authentication failed

The key is missing or unknown. Compare `Seneschal:ApiKey` with
`Seneschal.Api/Policies/integration-keys.yaml`.

### HTTP 403: integration forbidden

The key exists but is outside identity, capability, or environment scope.
This is distinct from a policy-denial 403, whose decision is `deny`.

### HTTP 502: invalid response

Seneschal returned malformed or unsupported decision content. Check the API
version and API logs.

### HTTP 503: timeout or unavailable API

Verify the configured port and readiness:

```powershell
Invoke-RestMethod http://localhost:5000/ready
```

`FailClosed` blocks by default. `FailOpen` must be selected explicitly and
allows governed operations to continue during evaluation failures.

### Wrong BaseUrl

- Seneschal: `http://localhost:5000`
- Application: `http://localhost:5010`
- `Seneschal:BaseUrl` must point to Seneschal, not the application.

## Success checklist

- [ ] Package installs without source project references.
- [ ] Invalid configuration fails during registration.
- [ ] `LogOnly` records deny and returns HTTP 200.
- [ ] `Enforce` blocks the same request with HTTP 403.
- [ ] Audit shows identity, capability, environment, policy, and mode.
