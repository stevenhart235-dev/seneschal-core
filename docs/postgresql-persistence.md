# PostgreSQL persistence

PostgreSQL persistence is opt-in. Seneschal uses process-local in-memory stores
unless `Seneschal:Persistence:Provider` is set to `PostgreSql`.

## Local setup

```powershell
docker run --detach --name seneschal-postgres `
  --publish 5432:5432 `
  --env POSTGRES_DB=seneschal `
  --env POSTGRES_USER=seneschal `
  --env POSTGRES_PASSWORD='<local-password>' `
  postgres:17-alpine

$env:Seneschal__Persistence__Provider = 'PostgreSql'
$env:ConnectionStrings__SeneschalPostgreSql = `
  'Host=localhost;Port=5432;Database=seneschal;Username=seneschal;Password=<local-password>'
```

Do not place a real password in source-controlled settings or environment
files.

## Apply migrations

Migrations are explicit. Startup validates connectivity and fails clearly when
migrations are pending, but never creates, resets, or migrates the database.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project Seneschal.Persistence.PostgreSql `
  --startup-project Seneschal.Persistence.PostgreSql
```

The migration command reads `ConnectionStrings__SeneschalPostgreSql`.
Separating migration execution supports controlled deployment and rollback.

## Start and verify

```powershell
dotnet run --project Seneschal.Api
```

Submit authenticated evaluations, inspect `GET /audit`, restart Seneschal
without removing PostgreSQL, and confirm the evidence remains. To return to
transient behavior, unset the variables or select `InMemory`; in-memory
evidence and approvals reset on restart.

The evidence table stores indexed identifiers and timestamps alongside the
complete `AuditEvent` JSONB payload. A SHA-256 fingerprint of canonical JSON
distinguishes identical retries from conflicting writes under the same evidence
ID. A database primary key enforces uniqueness and an identity sequence makes
equal-timestamp ordering deterministic.

## Current limitations

Only evaluation evidence and the minimal approval state required by its atomic
transaction are durable. Incidents, runtime mode, governance windows, activity,
metrics, catalog, and graph projections remain process-local or recomputable.
Policies, capabilities, identities, and integration keys remain YAML-backed.
Backup, restore, retention, high availability, multitenancy, and secret delivery
remain deployment concerns.

See [ADR 0009](adr/0009-operational-state-and-persistence.md).
