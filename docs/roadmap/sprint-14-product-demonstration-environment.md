# Sprint 14: Product Demonstration Environment

## Status

Proposed.

## Objective

Transform the current local sample into a believable fictional enterprise
deployment for **Northwind Financial**, a cloud-native payments company.

After startup, Seneschal should feel like a live operational control plane
rather than a sample application. The environment should support product
demonstrations, recorded videos, LinkedIn content, design-partner
conversations, and early customer walkthroughs without changing product
behavior or redesigning the portal.

The sprint succeeds when an operator can move from current activity to a
decision, policy, approval, incident, identity, capability, technology, and
governance-window explanation without encountering placeholder or
contradictory data.

## Non-goals

- No new product features.
- No schema redesign unless the existing model cannot represent required demo
  relationships honestly.
- No route changes.
- No UI redesign.
- No authentication or tenancy work.
- No new persistence engine.
- No real external integrations.
- No randomized or nondeterministic tests.
- No changes to policy evaluation, approval resolution, incident severity, or
  enforcement semantics solely to make a scenario easier to demonstrate.
- No attempt to model all of Northwind Financial's organization or traffic.

## Current repository baseline

### Configuration and demo startup

Current catalog and policy records live in:

- `Seneschal.Api/Policies/capabilities.yaml`
- `Seneschal.Api/Policies/identities.yaml`
- `Seneschal.Api/Policies/policies.yaml`
- `Seneschal.Api/Policies/policies.production-freeze.yaml`
- `Seneschal.Api/Policies/integration-keys.yaml`

`demo.ps1` starts the API and four package-based lab workers from
`labs/multi-application-adoption`. The API registers in-memory audit, activity,
approval, incident, governance-window, metrics, and mode stores in
`Seneschal.Api/Program.cs`. It does not currently load a baseline historical
dataset.

### Reusable domain models and stores

The implementation should reuse:

- `Capability` and `ICapabilityCatalog`
- `Identity` and `IdentityDefinition`
- `Policy` and the existing YAML policy projection
- `AuditEvent`, `IAuditEventStore`, and `IActivityStore`
- `ApprovalRecord` and `IApprovalStore`
- `GovernanceIncident` and `IGovernanceIncidentStore`
- `GovernanceWindow` and `IGovernanceWindowStore`
- existing decision, enforcement-mode, policy-evaluation, obligation, and
  approval lifecycle enums
- `TechnologyClassifier` and `TechnologyActivityService`

### Model constraints

The implementation must account for these constraints:

1. `IdentityDefinition` currently contains only name, description, and type.
   Core `Identity` has owner, environment, and attributes, but the API mapper
   currently derives owner from description and leaves environment empty.
2. There is no first-class Application record, application catalog, or
   application route. Technology pages currently infer applications from
   runtime identity IDs. Sprint 14 should maintain an explicit seed manifest
   mapping applications to identities without inventing links to nonexistent
   pages.
3. The API policy YAML shape supports one identity, capability, environment,
   decision, and reason per policy. Core policies support conditions,
   obligations, and priority, but the current YAML projection is intentionally
   narrower.
4. `InMemoryGovernanceWindowStore` exposes one manually controlled Production
   Freeze window. It is not a collection and has no schedule or timezone.
5. Incident identity is derived from capability, identity, reason, and first
   matched policy. Severity is derived from repetition, decision type, and
   capability risk. Incident records do not directly contain audit-event IDs.
6. Approval IDs and evaluation timestamps are generated during evaluation with
   GUIDs and `DateTimeOffset.UtcNow`. Replaying the public endpoint cannot
   produce stable 14-day history.
7. All operational stores are process-local and reset on restart.

These are planning constraints, not authorization to redesign the models.
Prefer a deterministic demo-data manifest and startup loader that writes
existing record types. Any minimal model change must be isolated, justified,
and reviewed before implementation.

## Demo environment model

### Company profile

Northwind Financial is a cloud-native payments company operating customer
checkout, payment processing, fraud detection, customer support, and finance
automation. Production workloads run primarily on Azure Kubernetes Service,
with GitHub-based delivery, Terraform/OpenTofu infrastructure management,
Azure-managed services, PostgreSQL, and selected OpenAI-assisted workflows.
Slack and Microsoft 365 carry operational notifications and documents.

The demo should use `production`, `staging`, `development`, and `shared`
consistently. Production traffic should be common but more tightly governed.

### Applications

Applications are seed-manifest relationships, not new product entities.

| Application | Owner | Purpose | Environment | Primary identities | Expected technologies |
|---|---|---|---|---|---|
| Payments API | Payments Platform | Authorize and capture payments | production | `payments-api` | Azure, Kubernetes, PostgreSQL |
| Checkout API | Checkout Experience | Customer checkout orchestration | production | `checkout-api` | Azure, Kubernetes, PostgreSQL |
| Customer Portal | Customer Experience | Customer account and payment views | production | `customer-portal` | Azure, Kubernetes, Microsoft 365 |
| Release Pipeline | Release Engineering | Build and production delivery | shared/production | `github-release-worker`, `release-approval-worker` | GitHub, Kubernetes, Slack |
| Terraform Cloud | Cloud Platform | Infrastructure plans and applies | production | `terraform-cloud` | Terraform/OpenTofu, Azure |
| Argo CD | Platform Engineering | Reconcile approved Kubernetes deployments | production | `argocd-production` | GitHub, Kubernetes |
| Platform Automation | Platform Engineering | Routine platform operations | shared | `platform-automation` | Azure, Kubernetes, Slack |
| Migration Service | Database Reliability Engineering | Controlled schema migrations | production | `database-migration-worker` | PostgreSQL |
| Support Assistant | Customer Support | Assist support cases without privileged access | production | `support-ai` | OpenAI, Microsoft 365, Azure |
| Finance Assistant | Finance Operations | Reconciliation and finance workflows | production | `finance-ai` | OpenAI, PostgreSQL, Microsoft 365 |
| Fraud Detection | Risk Engineering | Evaluate payment fraud signals | production | `fraud-detection-worker` | OpenAI, PostgreSQL, Slack |
| Developer Workstation | Developer Experience | Human development and troubleshooting | development/production | `developer-laptop` | GitHub, Azure, Kubernetes |

### Runtime identities

| Identity | Owner | Purpose | Environment | Application | Expected technology usage |
|---|---|---|---|---|---|
| `payments-api` | Payments Platform | Payment transaction processing | production | Payments API | Azure, Kubernetes, PostgreSQL |
| `checkout-api` | Checkout Experience | Checkout orchestration | production | Checkout API | Azure, Kubernetes, PostgreSQL |
| `customer-portal` | Customer Experience | Customer-facing portal operations | production | Customer Portal | Azure, Kubernetes, Microsoft 365 |
| `github-release-worker` | Release Engineering | Execute reviewed releases | production | Release Pipeline | GitHub, Slack |
| `terraform-cloud` | Cloud Platform | Plan and apply infrastructure | production | Terraform Cloud | Terraform/OpenTofu, Azure |
| `argocd-production` | Platform Engineering | Reconcile approved deployments | production | Argo CD | GitHub, Kubernetes |
| `platform-automation` | Platform Engineering | Routine platform maintenance | shared | Platform Automation | Azure, Kubernetes, Slack |
| `database-migration-worker` | Database Reliability Engineering | Apply approved database migrations | production | Migration Service | PostgreSQL |
| `support-ai` | Customer Support Engineering | Support-case assistance | production | Support Assistant | OpenAI, Microsoft 365 |
| `finance-ai` | Finance Systems | Finance reconciliation assistance | production | Finance Assistant | OpenAI, PostgreSQL, Microsoft 365 |
| `fraud-detection-worker` | Risk Engineering | Automated fraud analysis | production | Fraud Detection | OpenAI, PostgreSQL, Slack |
| `developer-laptop` | Developer Experience | Developer-initiated operations | development | Developer Workstation | GitHub, Azure, Kubernetes |
| `release-approval-worker` | Release Engineering | Correlate human release approval | production | Release Pipeline | GitHub, Slack |

Add 7–17 secondary identities for staging automation, read-only reporting,
backup operations, support supervisors, and service-specific workers to reach
20–30 identities. Every identity must have one application relationship and
one accountable owner in the seed manifest.

## Technology and capability packs

Target 40–60 capabilities across at least eight technologies. Each capability
must declare `technology`, owner, risk, category, lifecycle, documentation
reference, tags, and one or more application relationships in the seed
manifest.

| Technology | Required capabilities | Default owner | Suggested risk |
|---|---|---|---|
| Azure | `azure.keyvault.secret.read`; `azure.managedidentity.create`; `azure.storage.key.rotate`; `azure.resourcegroup.delete` | Cloud Security / Cloud Platform | High, High, High, Critical |
| GitHub | `github.deployment.production.execute`; `github.branch.protected.merge`; `github.pullrequest.approve`; `github.workflow.dispatch` | Release Engineering | High, High, Medium, Medium |
| Terraform/OpenTofu | `terraform.plan`; `terraform.apply.production`; `terraform.destroy.production`; `terraform.network.create` | Cloud Platform | Low, Critical, Critical, High |
| Kubernetes/AKS | `kubernetes.deployment.scale`; `kubernetes.pod.exec`; `kubernetes.namespace.delete`; `kubernetes.secret.read` | Platform Engineering | Medium, High, Critical, High |
| OpenAI | `openai.model.invoke`; `openai.knowledge.upload`; `openai.production-secret.read`; `openai.customer-data.process` | AI Platform / Data Governance | Medium, High, Critical, High |
| PostgreSQL | `postgres.schema.migrate`; `postgres.table.drop`; `postgres.production.write`; `postgres.backup.restore` | Database Reliability Engineering | High, Critical, High, Critical |
| Slack | `slack.security-alert.send`; `slack.channel.history.read` | Security Operations / Collaboration Platform | Low, Medium |
| Microsoft 365 | `m365.mail.send`; `m365.sharepoint.document.read` | Collaboration Platform | Medium, Medium |

Additional low- and medium-risk capabilities should create believable healthy
activity: secret metadata reads, deployment status reads, Terraform plan
inspection, pod health reads, model inference, transaction reads, Slack
notifications, and SharePoint knowledge retrieval.

Application relationships should be plausible rather than exhaustive. For
example, Support Assistant may invoke models and read approved SharePoint
knowledge, but should not normally read Azure production secrets. Terraform
Cloud may plan and apply infrastructure, while production destroy remains an
exception path.

Preserve legacy catalog entries required by existing tests until those tests
are deliberately migrated. Do not rename identifiers in place merely to make
the catalog look cleaner.

## Named governance policies

The initial pack should contain 10–15 memorable policies. Where the current
YAML shape requires multiple rows to express one named rule across identities
or capabilities, use stable related names and document the grouping in the
seed manifest.

| Policy | Purpose and behavior | Targets | Environment | Expected effect |
|---|---|---|---|---|
| Production Secret Access | Keep production secrets limited to approved service identities | Support AI, developer laptop; Azure and OpenAI secret reads | production | Deny anomalous identities; require approval only for explicitly approved exception paths |
| Production Freeze | Control production releases during the demo freeze | GitHub release worker; production deployment | production | Require approval through policy while the window contributes visible governance evidence |
| Infrastructure Protection | Prevent destructive infrastructure changes | Terraform Cloud; destroy and resource-group delete | production | Deny |
| Database Change Control | Require human review before schema changes | Migration worker; schema migrate and restore | production | Require approval |
| Privileged AI Operations | Prevent AI agents from privileged platform actions | Support AI, Finance AI; secret and privileged data capabilities | production | Deny |
| Protected Branch Policy | Restrict protected-branch merge to reviewed automation | GitHub release worker and developer laptop | production | Allow worker; deny direct developer merge |
| Customer Data Protection | Govern AI and data operations involving customer records | Support AI, Finance AI, Fraud Detection | production | Allow expected processing with obligations; deny unexpected bulk/secret access |
| Emergency Break Glass | Represent explicitly approved exceptional operations | Terraform Cloud and named operator operation IDs | production | Require approval, then allow once when consumed |
| Weekend Release Control | Make weekend deployment restrictions visible | Release Pipeline and Argo CD | production | Require approval for release worker; preserve healthy Argo CD path where policy permits |
| Kubernetes Privileged Operations | Restrict exec, namespace deletion, and secret access | Developer laptop and platform automation | production | Deny developer; require approval for controlled platform automation |
| Routine Production Reconciliation | Keep normal GitOps activity visibly healthy | Argo CD deployment reconciliation | production | Allow |
| Security Notification Delivery | Permit incident notifications | Platform and fraud automation | shared/production | Allow |

Policy reasons must read naturally in Decision Trace and must not contradict
the resulting decision. Obligations can be used only where the existing core
policy path can represent them without broadening the public configuration
contract unexpectedly.

## Hero demo scenarios

All scenario timestamps should be offsets from one captured seed anchor
(`seedNow`) rather than direct calls to the system clock. IDs should use stable
scenario prefixes and sequence numbers.

### Scenario A — Friday Production Freeze

- **Time:** Friday 16:55–17:12 local business time, represented in stored UTC.
- **Identity/application:** `github-release-worker` / Release Pipeline.
- **Capability/technology:** `github.deployment.production.execute` / GitHub.
- **Policies:** Production Freeze and Weekend Release Control.
- **Window:** Production Freeze is enabled in an evidence-bearing mode.
- **Sequence:** Pending Approval → approval created → approval approved →
  retry with the same operation ID → Allow and approval Consumed.
- **Incident:** At most informational; this is controlled activity.
- **Links:** Decision Trace ↔ approval; capability and identity activity;
  filtered audit; policy; governance window.
- **Constraint:** The current window engine converts Allow to Deny in Enforce
  and does not produce Require Approval. The approval must therefore come from
  policy, with the window contributing evidence without contradicting it.

### Scenario B — Terraform Destroy Prevented

- **Time:** Current day, 10:02–10:19.
- **Identity/application:** `terraform-cloud` / Terraform Cloud.
- **Capability/technology:** `terraform.destroy.production` /
  Terraform/OpenTofu.
- **Policies:** Infrastructure Protection, followed by an Emergency Break
  Glass approval path represented by a distinct, supportable policy context.
- **Sequence:** repeated Deny → Critical incident → approval requested →
  approval approved → later single-use Allow with existing approval evidence
  and obligations where supported.
- **Incident:** Critical requires repeated denied events against a Critical
  capability with a stable reason and matched policy.
- **Links:** Incident detail → identity/capability activity → Decision Trace;
  approval and audit correlation through the operation ID.
- **Constraint:** Existing policy resolution must be checked before claiming a
  Deny can later become Require Approval without a configuration/profile
  transition. Do not alter evaluator precedence for the demo.

### Scenario C — AI Agent Requests Production Secret

- **Time:** Yesterday at 14:20, after recurring normal support activity.
- **Identity/application:** `support-ai` / Support Assistant.
- **Capabilities/technologies:** normal `openai.model.invoke` and
  `m365.sharepoint.document.read`; anomalous `azure.keyvault.secret.read`.
- **Policies:** Privileged AI Operations and Production Secret Access.
- **Sequence:** normal Allows → secret-read Deny → repeated Deny if needed for
  incident severity → incident opened → incident resolved without approval.
- **Links:** Capability Activity shows the anomalous capability; Identity
  Activity supplies the stronger behavioral context; Decision Trace names both
  policies where existing evaluation supports multiple matches.

### Scenario D — Database Migration Approval

- **Time:** Two days ago, 09:30–09:48.
- **Identity/application:** `database-migration-worker` / Migration Service.
- **Capability/technology:** `postgres.schema.migrate` / PostgreSQL.
- **Policy:** Database Change Control.
- **Sequence:** Pending Approval → approval created → approved → retry with the
  same operation ID → Allow and Consumed.
- **Links:** Approval detail/context, audit sequence, identity activity,
  capability activity, Decision Trace, policy.

### Scenario E — Suspicious Developer Activity

- **Time:** Last week, a 12-minute burst outside normal business hours.
- **Identity/application:** `developer-laptop` / Developer Workstation.
- **Capabilities/technologies:** Azure secret read and resource-group delete;
  Kubernetes pod exec, secret read, and namespace delete.
- **Policies:** Production Secret Access, Infrastructure Protection, and
  Kubernetes Privileged Operations.
- **Sequence:** several Denies with stable reason/policy groupings → Warning
  incident(s) → repeated Critical-capability Deny → Critical incident.
- **Links:** Identity Activity is the primary pivot, then related capability
  activity, audit entries, policies, and Decision Traces.
- **Constraint:** Incidents group by capability, identity, reason, and first
  policy, so this narrative will naturally create multiple related incidents
  rather than one cross-capability incident.

### Scenario F — Healthy Automated Deployment

- **Time:** recurring weekdays, with the newest event in the current hour.
- **Identity/application:** `argocd-production` / Argo CD.
- **Capabilities/technologies:** approved GitHub deployment evidence and
  Kubernetes deployment reconciliation/scale.
- **Policies:** Routine Production Reconciliation and Customer Data Protection
  only where relevant.
- **Sequence:** Allow → audited → no incident.
- **Links:** Technology Detail → Decision Trace → Capability Activity and
  Identity Activity.
- **Purpose:** Demonstrate that Seneschal is an operational governance console,
  not only a denial console.

Each scenario should have a manifest-level assertion listing its stable
identity, application, capabilities, policies, technology keys, decisions,
approval IDs/statuses, incident grouping keys, timestamps, operation IDs, and
expected route targets.

## Seed data requirements

### Volume and time range

- 300–500 audit evaluations.
- 20–30 identities.
- 40–60 capabilities.
- At least 8 technologies.
- 10–15 named policies.
- 6–10 incidents.
- 3–5 governance-window definitions if the existing window abstraction is
  minimally extended; otherwise one implemented Production Freeze plus
  documented future window fixtures must not be presented as active product
  records.
- Several approvals across Pending, Approved, Rejected, and Consumed states.
- At least 14 days of timestamps, including current activity, yesterday, last
  week, and two weeks ago.

### Deterministic generation

Introduce a demo-only manifest and loader with:

- one captured `seedNow`, rounded to a stable minute;
- an injectable/fixed time source for tests;
- stable IDs derived from scenario and sequence, not `Guid.NewGuid()`;
- deterministic ordering;
- explicit scenario events plus deterministic baseline patterns;
- idempotent startup behavior within a process;
- an environment/configuration switch so normal and test startup can remain
  empty unless the Northwind profile is selected.

Do not call the public evaluation endpoint hundreds of times at startup. It
uses current time and generated IDs and would make historical results
nondeterministic. Seed existing stores coherently or add narrowly scoped
initial-state constructors/loaders.

### Weighted behavior

Suggested distribution across 400 evaluations:

- 72% Allow.
- 14% Deny.
- 8% Require Approval.
- 6% observe/LogOnly or warning-style evidence supported by existing enums.

The distribution is a target range, not a brittle exact assertion. Shape the
data deliberately:

- healthy Argo CD, payments, checkout, and fraud automation recur;
- business-hour traffic is denser on weekdays;
- weekend release activity is sparse and more approval-heavy;
- incident-related evaluations arrive in short bursts;
- destructive capabilities are rare;
- normal AI inference is common, privileged AI activity is exceptional;
- each technology has a distinct operational state;
- at least one catalog technology is configured but not observed.

## Relationship integrity

The seed validator must fail fast on:

- identity references missing from the identity catalog;
- capability references missing from the capability catalog;
- technology keys missing from `TechnologyClassifier`/icon mapping;
- policy targets missing from identity or capability catalogs;
- application mappings with missing identities;
- audit matched-policy names absent from configuration;
- approval identity/capability/environment/resource/operation scopes that do
  not match their source evaluation;
- Consumed approvals without a consuming decision;
- incidents whose grouping facts do not match source decisions;
- governance-window evidence naming an unavailable window;
- duplicate IDs;
- timestamps outside the declared range or impossible temporal order;
- scenario route targets that return missing/not-found states.

Every relevant object should connect naturally across technology, application
manifest, identity, capability, policy, decision, approval, incident,
governance window, and audit record. Existing investigation links must resolve
to meaningful current pages. Do not invent application links while no
application route exists.

## Dashboard and page expectations

These are data expectations, not redesign requirements.

| Surface | Expected startup condition |
|---|---|
| Dashboard | Current runtime posture, six recent varied events, a concise investigation queue, healthy recurring activity, and at least one current attention item |
| Live Monitor | Fresh Allow, Deny, and Pending Approval rows with meaningful identities, capabilities, environments, and investigation links |
| Technology Explorer | Azure, GitHub, Terraform, Kubernetes, OpenAI, PostgreSQL, Slack, and Microsoft 365 represented; healthy, attention, and configured/not-observed states |
| Technology Detail | Recent decisions, multiple applications where evidence exists, capability ownership/risk, and coherent governance context |
| Capability Explorer | 40–60 realistic catalog entries with owners, risk, technology metadata, and related policy evidence |
| Capability Activity | Recurring healthy history plus burst-shaped exceptional activity and working filtered audit links |
| Identity Activity | Recognizable normal behavior and anomalies; especially strong narratives for `support-ai`, `developer-laptop`, and `terraform-cloud` |
| Application surfaces | Use identity/application labels already derivable by current pages; do not claim a first-class application page until one exists |
| Audit Trail | 14 days of varied, nonuniform evidence with usable filters and stable Decision Trace targets |
| Decision Trace | Complete policy, runtime mode, effective action, approval, operation ID, governance-window, and partial-state evidence where applicable |
| Incidents | 6–10 Info/Warning/Critical examples with Open, Acknowledged, and Resolved states where current lifecycle APIs support them |
| Incident Detail | Source identity, capability, policy, reason, occurrence range, and matching audit investigation links |
| Approvals | Pending, Approved, Rejected, and Consumed examples with honest operation correlation |
| Policies | 10–15 memorable enterprise names with visible recent-match evidence |
| Governance Windows | Production Freeze with coherent affected capabilities and visible audit evidence; do not imply scheduling |

## Demo script documentation

Follow-up deliverable: `docs/demo/northwind-demo.md`.

Use this outline:

1. Northwind Financial overview and fictional-company disclaimer.
2. Environment and ownership summary.
3. Major technologies and applications.
4. Hero scenario prerequisites and expected outcomes.
5. Suggested 90-second path:
   Dashboard → attention item → Decision Trace → related approval or incident.
6. Suggested 5-minute path:
   Dashboard → Live Monitor → Technology Explorer → identity/capability
   investigation → audit evidence.
7. Suggested 15-minute technical walkthrough:
   healthy automation, production freeze approval, destructive Terraform
   prevention, AI anomaly, policy evidence, incident lifecycle.
8. Startup, deterministic reset, and shutdown instructions.
9. What is implemented versus simulated.
10. Known limitations and presenter-safe language.

Create this document only after the seed profile and route-integrity checks are
implemented so the script cannot drift from actual behavior.

## Implementation plan

Each commit should be independently reviewable and keep the application
buildable.

### Commit 1 — Sprint roadmap

- Add this roadmap.
- Record current model constraints and target volumes.
- No runtime changes.

### Commit 2 — Northwind manifest and catalog normalization

- Add a demo-profile manifest format within existing configuration boundaries.
- Add/normalize 40–60 capability records with ownership, risk, tags, and
  technology metadata.
- Preserve legacy catalog entries required by tests.
- Add configuration and duplicate/reference validation tests.

### Commit 3 — Applications, identities, and scoped integration keys

- Add 20–30 realistic identity definitions.
- Add an explicit demo-only application-to-identity relationship manifest.
- Add scoped development keys only for identities exercised through live demo
  workers.
- Test ownership and relationship completeness.

### Commit 4 — Named policies and supported window configuration

- Add 10–15 named Northwind policies using current semantics.
- Normalize Production Freeze capability targets.
- Decide and document whether the single-window store is sufficient; do not
  add a multi-window product feature implicitly.
- Add behavioral policy and window tests.

### Commit 5 — Deterministic baseline history

- Add the profile-gated startup seed loader.
- Seed 14 days of stable audit and activity history using existing record
  types.
- Add fixed-time, stable-ID, idempotency, distribution-range, and ordering
  tests.

Implementation note: the baseline loader is selected with
`Seneschal:Demo:NorthwindHistory:Enabled`, captures one injectable UTC startup
anchor, and writes directly to the existing audit and activity stores. It does
not pass seeded history through evaluation, approval, incident, or
governance-window services. IDs use the seed version, workload key, and
deterministic ordinal. See `docs/demos/northwind-history.md`.

### Commit 6 — Approval scenario records

- Add Friday Production Freeze and Database Migration Approval sequences.
- Add Pending, Approved, Rejected, and Consumed examples.
- Assert operation-ID reuse, temporal order, and Decision Trace correlation.

### Commit 7 — Incident scenarios and timelines

- Add Terraform, AI-secret, and suspicious-developer bursts through existing
  incident rules.
- Include Open, Acknowledged, and Resolved incidents.
- Assert severity, occurrence count, source facts, and matching audit links.

### Commit 8 — Healthy automation and relationship integrity

- Add recurring Argo CD, payments, checkout, fraud, and collaboration activity.
- Run the cross-object validator.
- Add route-level tests for all hero investigation targets.

### Commit 9 — Northwind demo script

- Add `docs/demo/northwind-demo.md` using the approved outline.
- Align startup/reset instructions with `demo.ps1` and the profile switch.
- Explicitly separate implemented behavior from narrative framing.

### Commit 10 — Final tuning and evidence

- Tune density and timestamps without changing page layouts.
- Replace exact-count UI tests with intent/range assertions where seed volume
  legitimately changes.
- Run full validation and capture representative screenshots.
- Document final known limitations.

## Test impact

Tests currently coupled to sample catalog records include:

- `ApiContractTests`
- `AuditTrailPageTests`
- `CapabilityExplorerPageTests`
- `CapabilityOverviewEndpointTests`
- `DecisionExportEndpointTests`
- `ExecutionGuidanceTests`
- `GraphEndpointTests`
- `GovernanceIncidentEndpointTests`
- `GovernanceIncidentsPageTests`
- `MetricsEndpointTests`
- `PolicyExplorerPageTests`
- `PortalRoutingTests`
- `TechnologyActivityServiceTests`
- `TechnologyPageTests`
- `Services/CoreDecisionServiceTests`

Many intentionally assert `Developer`, `SupportAgent`, `DeployApplication`,
`DeleteProductionDatabase`, and existing policy names. Prefer preserving this
compatibility fixture or giving tests isolated configuration. Do not update
hundreds of assertions merely because the default demo profile changed.

New tests should assert relationships, scenario semantics, ordering, bounded
distributions, and route resolution. Avoid snapshot tests and exact global
counts unless the count is itself part of a manifest invariant.

## Acceptance criteria

- The application builds successfully.
- All tests pass.
- Selecting the Northwind demo profile produces a populated environment at
  startup.
- Normal/test startup behavior remains intentionally selectable and documented.
- No seeded record has a broken investigation link.
- All six hero scenarios can be followed end to end within existing routes and
  semantics.
- Dashboard and explorer pages contain believable, coherent activity.
- Technology pages show healthy, attention, and configured/not-observed states;
  incident context appears where current pages support it.
- Approvals include Pending, Approved, Rejected, and Consumed examples.
- Incidents include Warning and Critical examples plus lifecycle variety.
- Timestamps span at least 14 days.
- Generation is deterministic for a fixed seed anchor.
- Repeated initialization does not duplicate records.
- Audit, activity, approval, and incident temporal order is possible and
  consistent.
- No production feature behavior is changed.
- A new user can understand what Seneschal does within five minutes.
- The final demo documentation accurately separates implemented behavior from
  simulated company context.

## Risks and guardrails

| Risk | Guardrail |
|---|---|
| Over-seeding makes pages unreadable | Bound portal queries, target 300–500 events, and inspect each primary page at demo viewports |
| Tests become tied to exact counts | Assert ranges, relationships, and named hero records; isolate compatibility fixtures |
| Timestamps vary by machine/run | Capture one seed anchor and use deterministic offsets/fixed test clock |
| Duplicate IDs or repeated startup duplication | Stable ID scheme, uniqueness validation, and idempotency test |
| Impossible temporal order | Validate request < resolution < retry/consumption and first-seen ≤ last-seen |
| Incidents do not match decisions | Generate incidents through the existing store where possible and verify grouping inputs |
| Approvals are disconnected | Reuse exact identity, capability, environment, resource, and operation ID scope |
| Policy behavior contradicts narrative | Add behavioral evaluations for every hero scenario before documentation |
| Existing sample-dependent tests fail | Preserve legacy fixtures or supply isolated test configuration |
| Demo data changes production startup | Require an explicit Northwind profile/configuration switch |
| “Random” data looks artificial | Use deterministic weighted schedules plus hand-authored bursts |
| Window narrative exceeds implementation | Never describe scheduling, multiple active windows, or approval-producing windows unless implemented and approved separately |
| Application links are invented | Keep application ownership in the seed manifest and use existing identity/technology views |

## Open questions

These questions remain because the current repository does not answer them:

1. Should Sprint 14 accept the current single Production Freeze window and
   document other window concepts, or approve a minimal collection-backed
   `IGovernanceWindowStore` extension? The requested 3–5 visible windows cannot
   be represented by the current interface without a product-model change.
2. Should application ownership remain demo-manifest metadata, or should the
   existing identity configuration minimally expose owner, environment, and
   application attributes already present on core `Identity`? There is no
   first-class application page or model today.
3. Should Northwind be the default `demo.ps1` profile or an explicit
   `-Profile Northwind` option? Explicit selection is safer for compatibility;
   default selection is simpler for demonstrations.
4. Should deterministic timestamps be relative to a startup anchor or pinned
   to a documented fixed demo date? Relative data always appears fresh; a fixed
   date produces byte-for-byte stable screenshots.
5. For Scenario B, can existing policy configuration express the intended
   transition from unconditional infrastructure Deny to break-glass approval
   without a presenter-controlled profile change? This must be proven with
   evaluator tests before the scenario is promised.
