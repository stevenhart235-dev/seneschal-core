# Run Seneschal in a container

Seneschal's API and Razor Pages portal run together in the
`Seneschal.Api` ASP.NET Core process. The image is intended for the current
single-instance application; orchestration and demo workloads belong in the
separate `seneschal-demo-lab` repository.

## Build and run

Build from the repository root:

```powershell
docker build --tag seneschal-core:dev .
```

## Publish release-matched images

Release the application runtime, PostgreSQL migration, and demo deployment
worker images from one clean working-tree revision with:

```powershell
.\scripts\publish-images.ps1 `
  -Tag demo-20260804 `
  -AwsAccountId 961381385086 `
  -AwsRegion us-east-2
```

The tag is required and `latest` is rejected. The script validates the active
AWS account, refuses dirty working trees, refuses to overwrite any existing ECR
tag, builds `Dockerfile`, `Dockerfile.migrations`, and
`Dockerfile.deployment-worker` from the repository root, verifies the Git
commit and working-tree state stayed unchanged between builds, stamps and
validates the same OCI source-revision label on every image, authenticates
Docker using `aws ecr get-login-password`, and pushes:

- `<account>.dkr.ecr.<region>.amazonaws.com/seneschal/core:<tag>`
- `<account>.dkr.ecr.<region>.amazonaws.com/seneschal/migrations:<tag>`
- `<account>.dkr.ecr.<region>.amazonaws.com/seneschal/demo-deployment-worker:<tag>`

On success it reports the source commit, tagged references, and digest-pinned
references for all three artifacts. It does not create repositories. For a
development-only publication from local changes, pass
`-AllowDirtyWorkingTree`; the warning notes that the reported commit does not
identify those uncommitted contents.

Run the container with host port `5077` mapped to container port `8080`:

```powershell
docker run --detach `
  --name seneschal-core `
  --publish 5077:8080 `
  seneschal-core:dev
```

Open the portal at `http://localhost:5077/dashboard`. The API, portal, static
assets, and health endpoints all use the same HTTP listener. TLS termination,
if required, should occur outside this container.

The image health check calls the existing lightweight `GET /health` endpoint.
`GET /ready` additionally reports whether the built-in catalog and policy
configuration loaded.

## Configuration

The image contains the product's checked-in `appsettings.json`, Razor Pages,
static web assets, and these non-secret or development/sample YAML defaults:

- `Policies/capabilities.yaml`
- `Policies/identities.yaml`
- `Policies/policies.yaml`
- `Policies/integration-keys.yaml`

The checked-in integration keys are explicitly development/sample values. Do
not use them as production credentials.

Normal ASP.NET Core environment-variable configuration is supported. In
particular:

| Environment variable | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Selects the ASP.NET Core environment. |
| `ASPNETCORE_URLS` | Overrides the listener; the image default is `http://0.0.0.0:8080`. |
| `Logging__LogLevel__Default` | Overrides the default application log level. |
| `Seneschal__Demo__NorthwindHistory__Enabled` | Enables the optional process-local Northwind history seed. |
| `Seneschal__Demo__NorthwindHistory__SeedVersion` | Overrides the deterministic seed identifier. |
| `Seneschal__Persistence__Provider` | Selects `InMemory` (default) or opt-in `PostgreSql` persistence. |
| `ConnectionStrings__SeneschalPostgreSql` | Supplies the connection string when PostgreSQL is selected. |
| `Seneschal__Configuration__CapabilitiesPath` | Selects an alternate capabilities YAML file. |
| Seneschal__Configuration__CapabilityPacksPath | Selects one local Capability Pack file or a directory of packs. |
| `Seneschal__Configuration__IdentitiesPath` | Selects an alternate identities YAML file. |
| `Seneschal__Configuration__PoliciesPath` | Selects an alternate policies YAML file. |
| `Seneschal__Configuration__IntegrationKeysPath` | Selects an alternate integration-key YAML file. |

For example, enable the Northwind history without changing the image:

```powershell
docker run --detach `
  --name seneschal-core `
  --publish 5077:8080 `
  --env Seneschal__Demo__NorthwindHistory__Enabled=true `
  seneschal-core:dev
```

No bind mount is required for the built-in defaults. To supply a deployment API
key without placing it in the image, create an integration-key YAML file
outside the repository, mount it read-only, and select it through configuration:

```powershell
docker run --detach `
  --name seneschal-core `
  --publish 5077:8080 `
  --mount type=bind,source=C:\secure\integration-keys.yaml,target=/config/integration-keys.yaml,readonly `
  --env Seneschal__Configuration__IntegrationKeysPath=/config/integration-keys.yaml `
  seneschal-core:dev
```

The same read-only mount pattern can override the other YAML files. The
container process runs as the non-root .NET `app` user, so mounted files must be
readable by that user.

An optional volume at `/home/app/.aspnet/DataProtection-Keys` can retain
ASP.NET Core Data Protection keys across container replacement. It is not
required to start Seneschal, and it does not persist Seneschal audit or
governance state.

## Evaluate a request

Supply the API key at runtime through the `X-Seneschal-Api-Key` header. Keep the
key in a secret manager or a local environment variable rather than embedding
it in a Dockerfile, image, command history, or committed file:

```powershell
$headers = @{
  'X-Seneschal-Api-Key' = $env:SENESCHAL_API_KEY
}

$body = @{
  identity = 'refund-worker'
  capability = 'payments.refund.create'
  context = @{
    environment = 'production'
    resource = 'payment-ledger'
  }
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5077/evaluate `
  -Headers $headers `
  -ContentType 'application/json' `
  -Body $body
```

The selected key must authorize the request identity, capability, and
environment. Decisions are visible through `GET /audit` and the portal Audit
Trail.

## Stop and remove

```powershell
docker stop seneschal-core
docker rm seneschal-core
```

## Persistence and runtime writes

The default profile does not write product data to the filesystem. Its audit
events, activity projections, metrics, approvals, incident operator state,
governance mode, and governance windows are process-local and reset when the
process restarts. The opt-in PostgreSQL provider durably stores evaluation
evidence, the complete approval lifecycle, runtime mode, Governance Window
state, and incident operator status. Derived projections remain recomputable;
see the [PostgreSQL setup guide](postgresql-persistence.md). With the Northwind
seed enabled, its deterministic baseline is regenerated relative to startup
time.

The application has no product-data directory to mount. The non-root process
can write ASP.NET Core Data Protection keys under
`/home/app/.aspnet/DataProtection-Keys` and can use the container's standard
writable temporary directory (`/tmp`) for framework or operating-system needs.
Neither path contains Seneschal audit or governance state. Mounting YAML
configuration preserves only configuration, not runtime state.

The application runtime image contains the PostgreSQL provider assembly and
checked-in migrations, but it does not contain the .NET SDK or `dotnet-ef` and
it never auto-migrates. Build the separate `seneschal-migrations:<release>`
image from `Dockerfile.migrations` at the same source revision as the
application release, then run it once as a pre-deployment step. See the
[database migration strategy](database-migrations.md) for its runtime contract.

The default in-memory stores and transient projections assume one process.
PostgreSQL shares its durable state, but coordinated multi-replica operation is
not yet supported. Secret distribution, database provisioning and backup,
migration-job execution and ordering, TLS/ingress, deployment manifests, demo
workloads, and lifecycle orchestration remain outside this image and, where
appropriate for the demonstration environment, belong in
`seneschal-demo-lab`.
