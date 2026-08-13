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