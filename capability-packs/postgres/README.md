# PostgreSQL Capability Pack

The built-in PostgreSQL pack is a curated starter catalog for governing common
database access, schema, table-data, access-management, administration, and
recovery operations. It is not a complete PostgreSQL SQL privilege model.

- Pack ID: `postgres`
- Version: `1.0.0`
- Provider: `Seneschal`

The catalog groups capabilities into Database Access, Schema Management, Table
Data, Database Change, Access Management, Database Administration, and Database
Recovery.

Risk values are opinionated, deterministic starter classifications based on
operational blast radius: discovery is generally Low, broad reads and backups
are Medium, writes and structural changes are High, and destructive,
privileged, or restore operations are Critical. They are authored metadata, not
automatically calculated scores. Organizations that need different metadata
should maintain their own non-conflicting catalog or pack definition under the
normal catalog ownership process.

Validate the pack:

```powershell
seneschal capability pack validate .\capability-packs\postgres\postgres.capability-pack.yaml
```

Load it by setting `Seneschal:Configuration:CapabilityPacksPath` to the pack
file or its directory. In environment-variable form:

```powershell
$env:Seneschal__Configuration__CapabilityPacksPath = '.\capability-packs\postgres'
```

Policies reference stable capability IDs directly, for example:

```yaml
capability: postgres.table.write
```

Pack provenance is available through the Capability Catalog and Capability
Explorer. It does not affect policy matching or decisions.
