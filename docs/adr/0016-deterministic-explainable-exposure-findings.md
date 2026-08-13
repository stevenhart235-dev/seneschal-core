# ADR-0016: Deterministic Explainable Exposure Findings

## Status

Accepted

## Context

Identity exposure analysis now combines current static governance context,
retained evaluation evidence, curated capability metadata, explicit evidence
coverage, and immutable evaluation-time configuration fingerprints. Operators need
those facts interpreted consistently without Seneschal implying business need,
overprivilege, unauthorized access, or a required policy change.

Putting interpretations directly in pages would make triggers and evidence
qualification inconsistent. Combining them with recommendations or scoring would
also turn factual investigation into prescriptive behavior without supporting
evidence or an established decision contract.

## Decision

Explainable Exposure Findings are a reusable, deterministic read-model layer over
identity exposure analysis. Every finding has a stable type, a documented factual
trigger, an explanation, supporting facts, coverage and observation-window context,
and an explicit statement of what the evidence does not prove.

The same exposure-analysis input produces the same finding types, facts, and order.
Coverage directly qualifies absence statements: Full supports absence during the
selected period, Partial supports only absence in retained evidence, and Unknown
suppresses v1 absence findings. Capability risk remains curated catalog metadata;
v1 adds neither finding severity nor confidence or composite scoring.

Findings are distinct from recommendations, automated remediation, policy
mutation, predictive need, and policy evaluation. They do not change evaluation
semantics or Capability Packs. The first projection is the existing Identity
Activity operator surface; a read-only API is deferred until a concrete consumer
requires one.

## Alternatives considered

### Embed finding rules in the operator page

Rejected because presentation code would become the contract, making deterministic
reuse and focused testing difficult.

### Add severity or a composite score

Rejected because no existing repository convention supplies non-arbitrary finding
severity, and combining coverage, risk, or activity would introduce semantics not
supported by the underlying facts.

### Emit absence findings for unknown coverage

Rejected because retained-evidence absence with no trustworthy completeness
boundary cannot support a useful non-use statement.

### Generate recommendations with each finding

Rejected because the available facts do not prove business necessity or justify a
specific governance change. Recommendations and remediation require a separate,
explicitly documented architecture and evidence contract.

### Add a findings API immediately

Deferred. V1 has an operator investigation consumer, and an endpoint without a
concrete integration need would expand the public contract unnecessarily.

## Consequences

- Operators can trace every finding to facts Seneschal possesses.
- Finding wording and ordering remain consistent across repeated analysis.
- Partial and unknown evidence cannot be presented as complete absence.
- Capability risk can focus attention without becoming a calculated finding score.
- Future consumers may reuse the findings service without duplicating triggers.
- Recommendations, scoring, workflow actions, and API versioning remain explicitly
  outside this decision.

## Related documentation

- [Explainable Exposure Findings v1](../explainable-exposure-findings.md)
- [Identity Governance Exposure Analysis](../identity-governance-exposure.md)
- [ADR-0014: Separate Configured Governance Exposure from Observed Evidence](0014-configured-governance-and-observed-exposure.md)
- [ADR-0015: Immutable Evaluation-Time Configuration Provenance](0015-immutable-evaluation-configuration-provenance.md)
