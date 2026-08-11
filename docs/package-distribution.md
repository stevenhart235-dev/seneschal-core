# .NET package distribution

Seneschal has three intended public .NET artifacts:

| Package | Purpose | Dependency relationship |
|---|---|---|
| `Seneschal.Client` | Typed client and Execution Guidance contract | Depends on its declared Microsoft.Extensions packages |
| `Seneschal.AspNetCore` | ASP.NET Core registration, middleware, and endpoint protection | Depends transitively on the matching `Seneschal.Client` package |
| `Seneschal.Cli` | Installable `seneschal` .NET tool | Carries the Client and Core runtime assemblies required by the tool |

All three packages currently use the same pre-release version,
`0.1.0-alpha.1`. Keep their versions aligned while they ship as a coordinated
release. A version change must be made in all three project files, followed by
package-only consumer validation.

## Local packaging and testing

Local-source commands are development and release-validation workflows. They
do not indicate that the packages are already available from NuGet.org.

```powershell
dotnet pack Seneschal.Client/Seneschal.Client.csproj -c Release
dotnet pack Seneschal.AspNetCore/Seneschal.AspNetCore.csproj -c Release
dotnet pack Seneschal.Cli/Seneschal.Cli.csproj -c Release
```

Packages are written to `artifacts/packages`. Copy the three generated
Seneschal `.nupkg` files into an empty directory to create an isolated
validation feed. For a fully offline/source-isolated test, also mirror the
published Microsoft dependency packages declared by `Seneschal.Client` into
that feed. Consumers used for validation must reference the feed rather than
repository projects or repository-relative sources.

After publication, the intended commands will be:

```powershell
dotnet add package Seneschal.Client
dotnet add package Seneschal.AspNetCore
dotnet tool install --global Seneschal.Cli
```

## Public publication prerequisites

NuGet.org publication is a separate release operation and is not performed by
local packaging. Before publishing:

1. The repository owner must select and document a software license. The
   current empty `LICENSE` file grants no license, and packages intentionally
   omit license metadata until that decision is made.
2. Select the release version and update all three packages together.
3. Add release notes for that version and verify the repository release history.
4. Repeat isolated-feed installation, fresh-consumer compilation, CLI runtime,
   package-content, full-test, and full-build checks.
5. Publish through an authenticated release workflow only after owner approval.
