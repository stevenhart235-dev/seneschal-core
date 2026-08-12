# ADR-0010: Execution Guidance as the Canonical Integration Contract

## Status

Accepted

## Context

Seneschal returns several related fields from evaluation: the policy Decision,
the runtime mode, EffectiveAction, approval state, and Execution Guidance.
Decision describes the governance outcome. EffectiveAction explains how runtime
mode projected that outcome. Approval fields describe workflow state. None of
those fields alone tells a caller whether it may execute governed work
immediately.

For example, Deny and RequireApproval under LogOnly allow the caller to
continue, while the same decisions under Enforce do not. Requiring each
integration to reproduce that matrix would distribute runtime semantics across
SDKs, pipelines, workers, and applications. Those copies could diverge as
guidance evolves.

## Decision

Execution Guidance is the canonical integration instruction for whether a
caller may execute governed work immediately.

Callers MUST use Execution Guidance directly or a conforming SDK helper such as
`ShouldProceed`. They MUST NOT independently derive execution permission from
Decision, EffectiveAction, runtime mode, approval state, or a combination of
those fields.

The immediate-execution contract is:

- `Proceed` and `ContinueLogOnly` authorize immediate execution.
- `Block`, `Pause`, `Queue`, and `Retry` do not authorize immediate
  execution.
- Missing, null, blank, malformed, and unknown future values do not authorize
  immediate execution.
- Unknown guidance fails closed.

Decision and EffectiveAction remain important evidence, explanation,
compatibility, and diagnostic fields. They are not caller execution
instructions.

SDKs and integrations should preserve an unrecognized raw guidance value for
diagnostics where practical while mapping it to a non-authorizing unknown
semantic.

## Language-neutral conformance

The versioned fixtures in
[`integrations/contracts/execution-guidance`](../../integrations/contracts/execution-guidance/README.md)
are the language-neutral source of truth for parsing and immediate-execution
behavior. SDKs and integrations should run those cases through their wire
parser and execution helper rather than translating .NET enum values.

The fixture contract distinguishes major semantic versions from compatible
fixture revisions. An older integration may safely encounter additive
non-authorizing values because unknown values fail closed. Authorizing another
semantic or changing an existing `shouldProceed` result requires a new major
contract version under the documented versioning policy.

## Alternatives considered

### Decision plus runtime mode

This was rejected as the integration contract. It requires callers to copy the
runtime's projection matrix and understand how Allow, Deny, RequireApproval,
LogOnly, and Enforce interact. It also makes approval and future guidance
behavior an integration concern. Two callers could receive the same response
and execute differently because their local matrices have drifted.

### Decision alone

This was rejected because a Deny or RequireApproval decision can produce
`ContinueLogOnly` under LogOnly. Decision is the governance result, not the
complete immediate-execution instruction.

### EffectiveAction alone

This was rejected because EffectiveAction is an existing diagnostic and
compatibility projection rather than the versioned caller contract. Parsing it
would create another implicit vocabulary without the conformance suite's
fail-closed rules.

### Approval state as permission

This was rejected because approval is one input to decision resolution.
Governance-window and runtime-mode effects still apply after approval
resolution, and approval state does not replace the final caller instruction.

## Consequences

- Callers have one field, or one conforming helper, for the immediate execution
  decision.
- Runtime decision and mode projection remains centralized in Seneschal.
- Unknown future guidance is forward-compatible and safe by default.
- SDKs need typed parsing and a fail-closed helper, while retaining raw values
  where practical.
- Decision, EffectiveAction, mode, and approval fields remain available for
  evidence, user interfaces, troubleshooting, and workflow-specific handling.
- Seneschal returns instructions; it does not itself pause, queue, retry, or
  execute the caller's operation.

## Compatibility implications

The existing wire value remains a string, so known values and raw unknown
values can deserialize without expanding a closed wire enum. Existing callers
that use Execution Guidance or a conforming `ShouldProceed` helper retain
their behavior. Callers that infer permission from other response fields are
non-conforming and can behave incorrectly, particularly under LogOnly.

Compatible additions within the current major conformance version must remain
non-authorizing to older integrations. A change that authorizes a new semantic
requires explicit SDK and integration adoption through a new major contract
version.

## Related documentation

- [Approval Execution Guidance](../product/approval-execution-guidance.md)
- [Policy Evaluation](../reference/policy-evaluation.md)
- [Execution Guidance Conformance Contract](../../integrations/contracts/execution-guidance/README.md)
- [ADR-0011: Shared Non-Mutating Evaluation Path for Preflight and Simulation](0011-shared-non-mutating-evaluation-path.md)
