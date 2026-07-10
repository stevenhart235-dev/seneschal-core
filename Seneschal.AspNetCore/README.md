# Seneschal.AspNetCore

`Seneschal.AspNetCore` adds middleware and endpoint metadata for evaluating a
capability with Seneschal before an ASP.NET Core handler runs.

```csharp
using Seneschal.AspNetCore;

app.UseRouting();
app.UseSeneschalCapabilityAttributes();

app.MapPost("/deploy", Deploy);

[RequiresCapability("DeployApplication", Environment = "dev")]
static IResult Deploy() => Results.Ok();
```

Register `ISeneschalClient` before enabling the middleware. This package
depends on `Seneschal.Client`.
