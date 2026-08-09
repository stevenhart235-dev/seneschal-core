# Getting Started

For an existing ASP.NET Core service, follow the canonical
[ASP.NET Core quickstart](quickstart/aspnet-core-quickstart.md):

1. Install `Seneschal.AspNetCore`.
2. Configure `BaseUrl` and `ApiKey`.
3. Run the read-only `seneschal preflight` check.
4. Register `AddSeneschal`.
5. Evaluate one action, check `ShouldProceed`, and run the service.

The package README contains optional automatic endpoint protection and advanced
failure behavior. Start with the direct evaluation path so the execution and
approval contract is explicit before adopting convenience middleware.
