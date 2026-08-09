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
