# Capability Packs

A capability pack is a versioned, curated capability catalog loaded into the
existing Seneschal Capability Catalog. Packs make catalog reuse explicit while
preserving capability IDs and source provenance.

```yaml
pack:
  id: postgres
  version: 1.0.0
  description: PostgreSQL capability catalog
  provider: Seneschal

capabilities:
  - name: postgres.table.read
    displayName: Read table
    description: Read rows from a PostgreSQL table.
    category: data
    technology: postgresql
    risk: Low
    tags: [postgres, database]
```

Validate a local file before configuring it:

```powershell
seneschal capability pack validate .\CapabilityPacks\postgres.yaml
```

Configure either that file or a directory of packs with
`Seneschal:Configuration:CapabilityPacksPath` (environment variable
`Seneschal__Configuration__CapabilityPacksPath`). Directory files are loaded in
ordinal path order after the unchanged local `capabilities.yaml`.

## Built-in packs

Maintained built-in packs live under `capability-packs/`:

- [PostgreSQL](../capability-packs/postgres/README.md): `postgres` version `1.0.0`
- [Kubernetes](../capability-packs/kubernetes/README.md): `kubernetes` version `1.0.0`
- [GitHub Actions](../capability-packs/github-actions/README.md): `github-actions` version `1.0.0`

Built-in means maintained and validated in this repository. Packs remain
explicit local inputs and are not enabled, installed, or downloaded
automatically.

## IDs and conflicts

Capability `name` is the globally stable identifier used by policies, requests,
and audit evidence. A pack does not namespace or rewrite it. Duplicate IDs in
one source fail. Identical cross-source definitions are deduplicated and retain
all provenance. Any differing field makes the definitions conflicting and
loading fails; there is no override or last-file-wins behavior.

Pack ID, version, and local source are exposed through catalog read models for
investigation. Provenance, pack metadata, categories, tags, and descriptive
fields do not independently authorize work. Policies continue to reference
stable capability IDs and evaluation semantics are unchanged.

Capability Pack v1 reads only local files. Inheritance, pack composition,
dependencies, signing, installation state, registries, marketplaces, remote
downloads, and automatic risk scoring are deferred.
