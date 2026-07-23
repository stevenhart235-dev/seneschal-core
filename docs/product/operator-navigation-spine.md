# Operator navigation spine

Seneschal's customer-facing investigation model is:

```text
Overview
  → Technology
  → Application
  → Capability
  → Decision
  → Policy
  → Audit
```

| Level | Operator question | Current route |
|---|---|---|
| Overview | Where should I begin? | `/dashboard`, `/monitor` |
| Technology | Which governed platforms are present and where is attention concentrated? | `/technologies`, `/technologies/{technologyKey}` |
| Application | Which workload used the technology and capabilities? | `/identity-activity?identityId=...` |
| Capability | What governed action was requested and what happened? | `/capability-explorer?capabilityId=...`, `/capability-activity?capabilityId=...` |
| Decision | Why was this individual request allowed, denied, or held for approval? | `/audit/{decisionId}` |
| Policy | Which configured policy contributed to the outcome? | `/policies` and filtered audit evidence |
| Audit | Can the runtime result be filtered and proven? | `/audit?...` |

Technology is a customer-facing projection over real capability catalog, activity, and retained audit data. It is not currently an integration registry or automated discovery system. Application is contextual presentation of the existing identity domain model when an identity represents a workload; underlying identity data and routes remain unchanged. Capability remains Seneschal's first-class governance object. Decision Trace explains one runtime outcome, while Audit Trail provides retained evidence across outcomes.

## Classification strategy

`TechnologyClassifier` owns all classification rules. Rules prefer structured catalog metadata—provider and tags—then explicit integration documentation metadata, then unambiguous capability namespaces. Checks are not scattered through Razor pages.

The capability catalog supports an optional `technology` field containing a stable lowercase technology key such as `azure`, `github`, `terraform`, `kubernetes`, `postgresql`, `aws`, `openai`, or `custom`. When present, this explicit value takes precedence over provider, tags, documentation, and namespace heuristics. Unsupported explicit values remain Unclassified rather than silently becoming a new vendor grouping.

| Technology | Classification evidence |
|---|---|
| Azure | `azure` provider/tag or `azure.*` capability namespace |
| GitHub | `github` provider/tag, `github.*`, or catalog documentation under `/github-actions/` |
| Terraform | `terraform`/`opentofu` tag or provider, `terraform.*`, or catalog documentation under `/terraform/` |
| Kubernetes | `kubernetes` provider/tag, `kubernetes.*`, `k8s.*`, or `aks.*` |
| OpenAI | `openai` provider/tag or `openai.*` |
| AWS | `aws` provider/tag or `aws.*` |
| PostgreSQL | `postgres`/`postgresql` tag or `postgres.*`/`postgresql.*` |
| Custom | Explicit `technology: custom` for customer-specific or internal platform capabilities |
| Unclassified | Missing, ambiguous, or unsupported evidence |

Generic `infrastructure.*`, deployment, database, and business capability names are not assumed to belong to a vendor. For example, an infrastructure capability is Terraform only when its catalog tags or integration documentation say so. Unmapped capabilities remain visible in the **Unclassified** group with their real catalog and runtime values.

The sample catalog explicitly classifies the documented GitHub Actions deployment capability and Terraform/OpenTofu infrastructure capabilities. Database migration, refund, and release-approval demonstrations are Custom because the repository does not establish a specific vendor platform. `DeployApplication` intentionally has no explicit metadata and exercises the Unclassified fallback.

## Aggregation boundaries

Technology and capability totals use the existing activity snapshot. Application membership, runtime modes, matched policies, environments, recent decisions, and Decision Trace links use retained audit evidence. Catalog-only technologies and capabilities show zero runtime counts and “Not observed” timestamps. Since audit storage is currently in-memory and bounded, application-level evidence represents the retained audit window rather than permanent history.

## Future route evolution

An explicit application model and route may eventually replace contextual identity presentation. Explicit integration metadata may replace heuristic technology classification. Those changes should preserve the spine and redirect existing investigation links; this phase does not claim either capability exists today.
