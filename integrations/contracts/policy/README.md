# Policy Schema Contract

`policy-schema.v1.json` is the machine-readable source of truth for the
existing Seneschal YAML policy authoring format. It uses JSON Schema Draft
2020-12; JSON Schema validators can validate YAML after parsing it into the
equivalent JSON data model.

Policy Schema v1 defines this document shape:

- a root object containing only a non-empty `policies` array;
- required non-blank `name`, `reason`, and `decision` values;
- at least one scalar or plural identity, capability, and environment target;
- optional `displayName`, `description`, `owner`, `severity`, and
  `rationale` strings;
- decisions `allow`, `deny`, `warn`, `log_only`, and
  `requires_approval`; and
- no properties beyond those represented in the current policy model.

Scalar and plural target properties may both appear. The existing loader merges
them, removes blank entries, and de-duplicates targets case-insensitively.

Schema validity establishes document structure only. It does not establish
unique policy IDs, known identity or capability references, or other semantic
configuration validity. Run:

    seneschal policy validate ./Policies/policies.yaml

The command performs YAML parsing, Policy Schema v1 validation, existing model
loading, and semantic/referential validation in that order.

## Editor association

YAML-aware editors that support the YAML Language Server schema-modeline
convention can associate a file without changing its parsed content:

    # yaml-language-server: $schema=../../integrations/contracts/policy/policy-schema.v1.json

Adjust the relative path for the policy file's location. Editors may also map
`policy-schema.v1.json` to policy file patterns through their standard JSON
Schema/YAML configuration. The contract does not require a particular editor,
extension, or vendor workflow.

## Versioning policy

`contractVersion` is the major authoring contract and `revision` tracks
compatible updates within that major version. The manifest in
`policy-contract.json` binds the current schema filename, version, revision,
and SHA-256 checksum. Every revision must have a changelog entry.

Compatible additive changes retain `v1` and increment the revision. Examples
include adding an optional property that older readers safely ignore only after
the authoring and reader compatibility policy explicitly permits it, or adding
non-semantic schema annotations. Readers should claim only the newest revision
they validate in tests. A reader for an older revision may reject a document
using a newer optional property; authors targeting mixed readers should remain
within the oldest deployed revision.

A new major version is required to remove or rename a supported property,
change a property's meaning or type incompatibly, make an optional property
required, make a required property optional where that changes accepted
semantics, change accepted decision semantics incompatibly, or restructure the
policy document incompatibly.

There is no runtime schema negotiation or API version header.

## Unsupported concepts

Policy Schema v1 does not define resource or action targets, arbitrary
conditions or operators, environment catalogs, time windows, composition,
inheritance, proposed-policy comparison, or policy templates.
