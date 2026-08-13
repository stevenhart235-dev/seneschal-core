# Identity governance exposure analysis

Identity governance exposure analysis compares current static governance context
with retained runtime evidence for a selected UTC observation period. The default
period is the last 30 days; operators can select 7 through 365 days. Results are
also bounded by the evidence retained by the configured audit store.

## Terms

- **Configured governance context**: a current policy directly targets the
  identity and capability under Seneschal's static operator-context rules. This
  proves a configured relationship, not that a future evaluation will authorize
  a request.
- **Observed capability activity**: evaluation evidence for the identity and
  capability is retained inside the selected inclusive UTC period. This proves
  recorded use, not business necessity or current authorization.
- **Configured + observed**: both facts are present.
- **No observed use in selected period**: configured context exists but no
  retained evaluation evidence falls within the period. This does not mean the
  capability is unused, unneeded, excessive, or safe to remove.
- **Observed outside current configured governance context**: evidence exists but
  no current static policy target relationship is present. Historical policy
  changes, imported evidence, or configuration drift may explain this state; it
  is not automatically unauthorized.

The analysis reports counts, curated capability risk, technology, provenance,
contributing policies and decisions, configured environments, observed evaluation
count, and most-recent evidence time. It deliberately does not calculate a
composite exposure score or infer necessity.

Automatic remediation, removal recommendations, predictive capability need,
business-necessity inference, and composite risk scoring remain future analytical
layers and require additional evidence and explicit architecture.
## Evidence coverage

The requested observation window is an inclusive UTC interval. Evidence coverage
qualifies what the configured audit source can prove:

- **Full**: the store's durable completeness boundary is at or before the
  requested start. Absence of evidence can be described as no observed use during
  the selected period.
- **Partial**: the completeness boundary is after the requested start. Absence
  means no observed use was found in retained evidence; earlier activity cannot
  be determined.
- **Unknown**: the source exposes no trustworthy completeness boundary. Absence
  of retained evidence is not proof of non-use.

The in-memory provider establishes its boundary when the store is initialized.
PostgreSQL persists a singleton boundary initialized when the evidence-coverage
migration is applied. The oldest event is never treated as a retention boundary.

## Historical configuration provenance

New committed evaluation evidence records a `sha256:` governance configuration
fingerprint. It canonicalizes evaluation-relevant projected policies, runtime
mode, and governance-window semantics. Policy and condition ordering is stable,
and YAML whitespace/comments are not inputs. Identity and capability descriptive
metadata, file paths, integration keys, and secrets are excluded because they do
not participate in current evaluation semantics.

Exposure analysis reports distinct fingerprints observed during the window,
events whose provenance is unavailable, and whether all available fingerprints
match the current configuration. A changed fingerprint means some
evaluation-relevant governance configuration differs. It does not prove that a
specific policy changed, that the historical result would now differ, or that
current configuration is incorrect.

Historical rows without the field remain readable and are shown as configuration
provenance unavailable. Seneschal does not backfill them with the current
fingerprint. This milestone stores fingerprints, matched-policy evidence, and
policy evaluations, but not complete historical configuration snapshots; old
configuration contents therefore cannot be reconstructed from the fingerprint.

## Explainable findings

The operator exposure surface derives deterministic, non-prescriptive findings from
these facts. See [Explainable Exposure Findings v1](explainable-exposure-findings.md)
and [ADR-0016](adr/0016-deterministic-explainable-exposure-findings.md).
