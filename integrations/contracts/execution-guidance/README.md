# Execution Guidance Conformance Contract

`execution-guidance-conformance.v1.json` is the language-neutral source of
truth for interpreting the API response property `executionGuidance`. Each case
contains the raw wire input and the expected normalized semantic state and
immediate-execution result. The input is an object so a missing property remains
distinct from an explicitly `null` value.

Callers MUST determine immediate execution from Execution Guidance or a
conforming SDK helper. Callers MUST NOT independently derive execution
permission from Decision, EffectiveAction, runtime mode, or approval state.

Known guidance names are matched case-insensitively. Only `Proceed` and
`ContinueLogOnly` authorize immediate execution. Missing, null, blank,
malformed, and unknown future values normalize to `Unknown` and MUST fail
closed. Integrations SHOULD retain an unknown raw value for diagnostics.

The adjacent JSON Schema documents the fixture format. It describes contract
semantics rather than any language's enum representation; semantic values are
names, never implementation-specific numeric values.

Every Seneschal SDK and first- or third-party integration is expected to run
these same cases through its wire-response parser and execution helper. A
non-.NET SDK can load each `input` object as an evaluation-response fragment,
then compare its normalized state and execution result with `expected`.

## Versioning policy

`contractVersion` identifies the major semantic contract. `revision` tracks
compatible updates to that major version. The manifest
`execution-guidance-contract.json` binds the current fixture and schema to their
version, revision, and semantic checksum. Every fixture revision must have a
matching changelog entry.

- No version or revision change is needed for spelling, formatting, comments,
  or documentation changes that do not alter fixture cases or their meaning.
- An additive compatible update keeps `contractVersion: "v1"` and increments
  `revision`. Examples include additional case variants, malformed-input cases,
  or a new non-authorizing semantic that older integrations safely treat as
  unknown. Update the manifest checksum and changelog.
- A new major fixture such as `v2` is required when changing an existing raw
  value's normalized semantic, changing any existing `shouldProceed` result,
  authorizing another immediate-execution semantic, removing or renaming
  required cases, changing case sensitivity, or incompatibly changing the
  fixture structure.

An SDK conforming to an older revision of the same major version may continue
to operate against newer responses. It may not recognize additive guidance,
but it MUST preserve safe behavior by treating unknown values as unrecognized
and failing closed. It should claim only the newest revision it actually runs
in its tests. Supporting a new major version requires an explicit SDK update;
there is no runtime negotiation and no API version header.
