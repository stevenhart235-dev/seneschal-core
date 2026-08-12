# ADR-0012: Versioned Policy Authoring Contract

## Status

Accepted

## Context

Seneschal policies are authored outside the runtime as YAML. Until now, the
accepted document shape was defined implicitly by .NET DTO properties,
YamlDotNet behavior, loader defaults, checked-in examples, and validation code.
Editors and non-.NET tooling had no stable machine-readable contract, and a DTO
change could unintentionally alter the authoring format.

The YAML format is an external configuration boundary even though it is not an
HTTP API. It therefore needs an explicit compatibility policy without changing
the policy model or evaluation semantics.

## Decision

Seneschal publishes a versioned, language-neutral JSON Schema for its YAML
policy authoring format. Policy Schema v1 describes only the root and policy
properties implemented by the current model and loader.

The schema contract has a major version and compatible revision recorded in a
manifest with the schema checksum. Compatible revisions remain within the same
major version. Incompatible field, meaning, type, required-status, accepted
value, or document-structure changes require a new major version. There is no
runtime protocol negotiation.

`seneschal policy validate` applies the schema before existing model,
semantic, and referential validation. JSON Schema does not replace checks such
as duplicate policy IDs or configured identity and capability references.

## Alternatives considered

### Keep the DTO and loader as the implicit contract

This was rejected because reflection over implementation types does not provide
a stable language-neutral artifact, editor integration, or deliberate
compatibility review.

### Generate a schema from the .NET model

This was rejected for v1 because serializer defaults and CLR nullability do not
fully express functional requirements such as one scalar-or-plural target per
dimension. A reviewed schema is the external contract; tests protect its
compatibility with the loader.

### Replace semantic validation with JSON Schema

This was rejected because JSON Schema cannot establish references against the
loaded identity and capability catalogs or express all existing deterministic
configuration checks cleanly.

### Embed a schema version in every policy document

This was rejected because it would change the currently supported root shape.
Schema association belongs in tooling configuration or a non-semantic YAML
comment.

## Consequences

- Operators and editors have a machine-readable authoring contract.
- Authoring-format changes require explicit versioning and compatibility review.
- The CLI reports structural failures before loading and reference validation.
- Schema-valid configuration can still be operationally invalid.
- The hand-maintained schema and loader can drift, so contract tests must
  validate checked-in files and representative accepted/rejected shapes through
  both paths.
- Unknown properties are rejected by the authoring contract even though the
  runtime loader retains its existing permissive behavior outside CLI
  validation.

## Related documentation

- [Policy Schema Contract](../../integrations/contracts/policy/README.md)
- [Policy](../core-concepts/policy.md)
- [ADR-0007: Make Core the Authoritative Runtime](0007-api-runtime-convergence.md)
