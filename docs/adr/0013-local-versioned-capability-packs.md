# ADR-0013: Local Versioned Capability Packs

## Status

Accepted

## Context

The Capability Catalog already provides stable capability identifiers and
operator metadata through `ICapabilityCatalog`, but curated catalogs can only be
copied into one local `capabilities.yaml`. That loses catalog provenance and
makes reuse and conflict review informal. Packs are external YAML inputs, so
their structure and merge behavior must be deterministic and versioned without
creating another catalog or changing policy evaluation.

## Decision

Seneschal supports Capability Pack v1 as an additive, local filesystem catalog
input. A pack declares a lowercase pack ID, a `MAJOR.MINOR.PATCH` version,
optional description/provider metadata, and capabilities in the existing
capability shape. `Seneschal:Configuration:CapabilityPacksPath` selects one pack
file or a directory of `.yaml` and `.yml` pack files.

The local catalog is loaded first. Pack files are then loaded in ordinal path
order and capabilities retain document order. Capability `name` remains the
globally stable policy and evaluation key. Definitions with the same ID in the
same source fail. Cross-source duplicates are accepted only when every existing
capability field is semantically identical; they produce one catalog entry with
all sources preserved. Conflicts fail and never use last-file-wins behavior.

Provenance belongs to `CapabilityCatalogEntry`, not `Capability`, and records
local catalog or pack ID/version/path. It is operator evidence and is absent
from decision requests and policy evaluation.

Capability Pack v1 is local-only. It has no inheritance, composition, download,
registry, marketplace, dependency, signing, or automatic risk behavior.

## Alternatives considered

### Copy pack contents into capabilities.yaml

Rejected because it discards version and provider provenance and makes updates
and conflicts manual.

### Create a second pack catalog

Rejected because runtime and operator consumers already depend on the stable
`ICapabilityCatalog` boundary. Packs are inputs to that catalog, not a parallel
source of truth.

### Use last file wins

Rejected because filesystem order would silently alter governance inventory and
make policy references ambiguous.

### Fetch packs from a registry

Deferred. Network distribution introduces trust, availability, signing, and
upgrade policy decisions that are outside the first milestone.

## Consequences

- Existing local catalogs remain valid without configuration changes.
- Operators can trace imported capabilities to pack versions.
- Conflicting stable IDs stop startup or validation deterministically.
- Identical definitions can be shared without duplicating catalog entries.
- Pack authors must version changes and pass the existing capability metadata
  rules.
- Local file paths may appear in operator diagnostics but never enter decisions.

## Related documentation

- [Capability Pack Contract](../../integrations/contracts/capability-pack/README.md)
- [Capability Packs](../capability-packs.md)
- [ADR-0004: Capability Catalog Product Boundary](0004-capability-catalog-product-boundary.md)
- [ADR-0012: Versioned Policy Authoring Contract](0012-versioned-policy-authoring-contract.md)
