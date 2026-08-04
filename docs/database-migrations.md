# Database migration strategy

Seneschal uses explicit, forward-only Entity Framework Core migrations for the
opt-in PostgreSQL provider. The application validates the database at startup;
it never creates, resets, or migrates the schema. A deployment operator or a
single pre-deployment job must migrate the database before starting the new
application version.

## Supported version contract

The supported production pairing is the application release with that
release's complete migration set applied. Rolling upgrades and N-1 application
compatibility with the current schema are not yet guaranteed. Use a
single-instance, stop-the-writer deployment sequence until an additive
expand/contract policy and multi-instance coordination are implemented.

Startup fails when a known migration is pending. EF does not treat an unknown
future migration-history row as pending, so the deployment pipeline must also
verify that the ordered contents of `__EFMigrationsHistory` exactly match the
migrations shipped with the release. This prevents an older binary from being
started against a newer, unsupported schema.

## Build the release-owned migration bundle

Restore the repository-pinned EF Core tool from the repository root:

```powershell
dotnet tool restore
```

Build a bundle for the current workstation (useful for local validation):

```powershell
New-Item -ItemType Directory -Force artifacts/migrations | Out-Null
$env:SENESCHAL_MIGRATION_BUNDLE_BUILD = 'true'
dotnet tool run dotnet-ef migrations bundle `
  --project Seneschal.Persistence.PostgreSql `
  --startup-project Seneschal.Persistence.PostgreSql `
  --configuration Release `
  --output artifacts/migrations/seneschal-migrate.exe `
  --force
Remove-Item Env:SENESCHAL_MIGRATION_BUNDLE_BUILD
```

The bundle is a release artifact: build and publish it from the same source
revision as the matching Seneschal application image. It contains the complete
checked-in migration chain at that revision. Do not reuse a migration image
from another release, even when its tag appears compatible.

Build the Linux migration image, which creates a self-contained bundle during
the build and does not retain the SDK or EF tool in its final stage:

```powershell
docker build --file Dockerfile.migrations --tag seneschal-migrations:dev .
```

For a release, replace `dev` with the exact immutable application release tag
or digest-correlated version used by the deployment repository.

## Run against local PostgreSQL

Pass the connection string only at container runtime. For PostgreSQL running
on the host (Docker Desktop), run:

```powershell
docker run --rm `
  --env "ConnectionStrings__SeneschalPostgreSql=Host=host.docker.internal;Port=5432;Database=seneschal;Username=seneschal;Password=<local-password>" `
  seneschal-migrations:dev
```

`ConnectionStrings__SeneschalPostgreSql` is the only required environment
variable and the only supported way to supply the database connection string.
Keep it in the deployment platform's secret facility; it is not built into the
bundle or image. The image runs `/app/seneschal-migrate --verbose` from `/app`
as the non-root .NET `app` user.

`SENESCHAL_MIGRATION_BUNDLE_BUILD` is an internal build-time switch used only
to compile the bundle without a connection string. It is not present in the
final image and is not a migration runtime input.

With no target migration supplied, EF applies every pending checked-in
migration to the current head and records it in `__EFMigrationsHistory`.
Running the same release against the same database again is expected to report
that the database is already up to date and exit zero without dropping,
resetting, or recreating it. A missing or invalid connection string, an
unreachable database, or a migration error is logged and exits nonzero. Stop
the rollout on any nonzero result; the runner never responds by resetting the
database.

For SDK-based local troubleshooting, set the same runtime variable and invoke
the checked-in migration chain directly:

```powershell
$env:ConnectionStrings__SeneschalPostgreSql = `
  'Host=localhost;Port=5432;Database=seneschal;Username=seneschal;Password=<local-password>'
dotnet tool run dotnet-ef database update `
  --project Seneschal.Persistence.PostgreSql `
  --startup-project Seneschal.Persistence.PostgreSql `
  --verbose
```

Do not pass a target migration in normal operations. Confirm the result with:

```powershell
dotnet tool run dotnet-ef migrations list `
  --project Seneschal.Persistence.PostgreSql `
  --startup-project Seneschal.Persistence.PostgreSql
dotnet tool run dotnet-ef migrations has-pending-model-changes `
  --project Seneschal.Persistence.PostgreSql `
  --startup-project Seneschal.Persistence.PostgreSql
```

The database history can be independently inspected with:

```sql
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

## Deployment order

For the currently supported single-instance deployment:

1. Prevent new writes and stop the existing Seneschal instance.
2. Take and verify a recoverable database backup or managed snapshot.
3. Run one migration process using artifacts from the exact release being
   deployed and the target database's secret connection string.
4. Stop the deployment on any nonzero migration result. Inspect both the
   migration output and `__EFMigrationsHistory`; do not start the application
   or reset the database.
5. Verify the applied history exactly matches the release and run the pending
   model-change check in CI from the same source revision.
6. Deploy the application image, then require successful `/health` and
   `/ready` checks and exercise the critical evaluation path.

In Kubernetes, use a single pre-deployment Job or equivalent CI/CD stage. The
published Seneschal application image contains the provider assembly and
migrations but no migration tooling. The separate, versioned migration image
contains only the self-contained bundle and its Linux runtime dependencies; it
contains neither the .NET SDK nor `dotnet-ef`. Do not run one migrator per
application replica, and do not put connection strings into either image.

The `seneschal-demo-lab` deployment owns PostgreSQL provisioning, runtime secret
delivery, choosing the matching immutable migration-image tag, execution and
observation of the one-shot migration job, ordering successful completion
before the Seneschal rollout, and backup/restore operations. `seneschal-core`
owns the checked-in migrations, bundle and image definition, startup
validation, runtime contract, and release compatibility contract. This
repository intentionally provides no Kubernetes manifests.

## Failure, backup, and rollback

EF/Npgsql executes each migration transactionally when its operations permit;
the complete multi-migration deployment must not be assumed to be one atomic
transaction. After a failure, leave writes stopped, capture logs, inspect
schema and migration history, and either apply a reviewed forward repair or
restore the pre-migration backup. Never edit migration history merely to make
startup pass.

`Down` methods support development workflows but are not the production
rollback policy. An application rollback is safe only when that older release
has been explicitly validated against the already-upgraded schema. Otherwise,
restore the pre-migration database backup and redeploy the matching older
application image. Backup technology, retention, RPO, RTO, encryption, and
restore drills are platform responsibilities; a production deployment must
choose and test them before its first schema change.

## Release validation

Every migration change must pass all of the following before release:

- migration from an empty database to current head;
- upgrade from the previously released migration head with representative
  evidence and mutable operational state preserved;
- PostgreSQL provider and full solution tests;
- `migrations has-pending-model-changes`;
- startup failure against a database with pending known migrations;
- application health, readiness, evaluation, approval, incident, and restart
  smoke checks against the migrated database.

The incremental regression test preserves evidence, approval, runtime mode,
Governance Window, and incident operator state while advancing the previous
schema to current head. Older historical upgrade edges remain immutable and
should be exercised from production-like snapshots when a release or database
engine upgrade makes that risk material.
