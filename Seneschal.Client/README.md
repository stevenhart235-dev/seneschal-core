# Seneschal.Client

`Seneschal.Client` is the .NET client for requesting capability decisions from
a running Seneschal API.

```csharp
using Seneschal.Client;

builder.Services.Configure<SeneschalClientOptions>(options =>
{
    options.BaseUrl = new Uri("http://localhost:5000");
    options.ApiKey = builder.Configuration["Seneschal:ApiKey"];
});
builder.Services.AddHttpClient<ISeneschalClient, SeneschalClient>();
```

This package requires .NET 8 or later and a reachable Seneschal runtime.
