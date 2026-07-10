# Seneschal.AspNetCore

`Seneschal.AspNetCore` adds capability governance to ASP.NET Core endpoints.
It depends on `Seneschal.Client` and registers the client through the golden-path
setup below.

## Register Seneschal

```csharp
using Seneschal.AspNetCore;

builder.Services.AddSeneschal(options =>
{
    options.BaseUrl = new Uri("http://localhost:5000");
    options.ApiKey = "dev-sample-key";
    options.IdentityResolver = context =>
        context.User.Identity?.Name ?? "anonymous";
    options.DefaultEnvironment = "dev";
});
```

Enable endpoint evaluation after routing and authentication:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSeneschal();
```

## Protect an endpoint

Use an attribute:

```csharp
[RequiresCapability("ProductionDeployment")]
static IResult Deploy() => Results.Ok();
```

Or endpoint conventions:

```csharp
app.MapPost("/deploy", () => Results.Ok())
    .RequireCapability("ProductionDeployment");
```

Endpoint metadata can override the default environment and resource:

```csharp
[RequiresCapability(
    "ProductionDeployment",
    Environment = "production",
    ResourceId = "deployment-api")]
static IResult DeployToProduction() => Results.Ok();
```

## Advanced integration

`AddSeneschal` also registers `ISeneschalClient` for direct decision requests.
Existing lower-level APIs remain available:

- Manual `SeneschalClientOptions` and `AddHttpClient` registration
- `UseSeneschalCapabilityAttributes(...)`
- `UseSeneschalCapability(...)`
- `SeneschalEnforcementBehavior`

The package does not discover capabilities or add policies automatically.
