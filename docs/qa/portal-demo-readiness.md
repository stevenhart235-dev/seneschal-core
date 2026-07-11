# Portal Demo Readiness QA Review

## Scope and test setup

Reviewed against one live `Seneschal.Api` process with these package-only
workers:

- DeploymentWorker — Allow
- DatabaseMigrationWorker — Deny
- RefundWorker — Allow
- ApprovalWorker — Pending Approval

The workers were exercised in LogOnly and Enforce without restarting the API.
All exposed navigation routes and primary Dashboard drill-downs returned HTTP
200.

Severity meanings:

- **Blocker** — likely to stop or materially invalidate the demo.
- **High** — likely to make behavior appear incorrect or unreliable.
- **Medium** — visible inconsistency or awkward behavior with a reasonable
  demo workaround.
- **Low** — polish issue unlikely to disrupt the narrative.

Status values are **Fixed in this pass** or **Open**.

## Findings

| Page | Issue | Severity | Reproduction steps | Expected behavior | Actual behavior | Recommended fix | Demo blocker | Status |
|---|---|---:|---|---|---|---|---|---|
| Dashboard | Polling stopped after the first successful refresh. | Blocker | Open Dashboard with activity and wait for two polling intervals. | Live regions continue refreshing every three seconds. | The first refresh replaced the subtree containing `#live-last-evaluation`; the next refresh dereferenced the removed node and aborted. | Preserve the timestamp node and update it in place. | Yes | Fixed in this pass |
| Dashboard | Live feed changed from six rows to ten after the first poll, causing a large layout jump. | High | Load Dashboard with at least ten audit events and wait three seconds. | The approved six-row density remains stable. | Initial Razor output used six events; JavaScript rendered all ten returned by the endpoint. | Limit polled feed rendering to six events. | No | Fixed in this pass |
| Dashboard | Needs Attention detail counts became stale. | High | Keep Dashboard open while workers create more Deny/Pending Approval events. | KPI and attention counts update together. | Polling updated only top KPI IDs; detail counts had no update targets. | Add distinct detail IDs and update both regions. | No | Fixed in this pass |
| Dashboard | Empty decision distribution appeared entirely amber. | High | Start with clean in-memory stores and open Dashboard. | Zero activity renders a neutral empty ring and zero labels. | Zero percentages left the final conic segment filling the ring as Pending Approval. | Apply a neutral empty-chart state when total decisions are zero. | No | Fixed in this pass |
| Dashboard | Derived action text could be read as confirmed external execution. | High | Inspect any Allow, Deny, or Pending Approval feed row. | Derived action is explicitly identified as projected. | Rows displayed `Executed`, `Blocked`, or similar without a projected qualifier. | Prefix action labels with `Projected:` while retaining exact semantics. | No | Fixed in this pass |
| Dashboard | Long identity names can wrap unpredictably in the dominant feed. | Medium | Evaluate with a very long identity and view at 1024–1440px. | Identifier remains readable without overlapping the result column. | Capability truncates, but identity had no explicit break policy. | Allow identity links to wrap anywhere; consider title text for truncated capabilities. | No | Fixed partially; wrapping added |
| Dashboard | Needs Attention incident section cannot show an actual open count. | Medium | Create incidents and compare Dashboard with Incidents. | Dashboard either shows an accurate count or clearly presents navigation only. | It says “Operational queue” without count because current Dashboard data does not include incident summaries. | Keep navigation-only wording until incident data is intentionally added to the presentation model. | No | Open |
| Dashboard | Historical LogOnly entries remain beside Enforce entries after mode switch. | Low | Run workers in LogOnly, switch to Enforce, and view the feed. | Each entry retains the mode used for that evaluation. | Mixed modes appear together, which is correct but may need explanation during demo. | Explain that audit evidence preserves evaluation-time mode; do not rewrite history. | No | Open; behavior is correct |
| Monitor | Mode explanation was LogOnly-specific and the new conditional compared with the wrong display label. | High | Switch to Enforce and open Monitor; then return to LogOnly. | Copy accurately explains the selected mode. | `CurrentMode` displays `Monitor`, not `LogOnly`; comparison against `LogOnly` selected the Enforce explanation in monitoring mode. | Branch on the actual display value and provide mode-specific copy. | Yes | Fixed in this pass |
| Monitor | Uses `Monitor` where Dashboard/Governance use canonical `LogOnly`. | Medium | Compare the three pages in LogOnly. | Display terminology explains both friendly and canonical names consistently. | Monitor says `Monitor`; Governance says `LogOnly`; Dashboard says Monitoring plus canonical LogOnly. | Later standardize to `Monitoring (LogOnly)` without changing the enum. | No | Open |
| Runtime Governance | Switching mode is not authorization-protected and mode changes are not administrative audit events. | High | POST the mode handler directly, then inspect audit. | Administrative changes have authorization and durable evidence before production use. | Any caller reaching the handler can switch in-memory mode; decision audit does not record the administration event. | Add administrative authorization and audit in a backend security pass. | No for controlled local demo; yes for external demo | Open; not safe/localized UX work |
| Runtime Governance | Impact summary means “latest 100 events,” not an explicit time window. | Medium | Generate more than 100 events and inspect Current Impact. | Scope is obvious and counts remain interpretable. | Subtitle states the 100-event bound, but “Recent” can still be interpreted as time-based. | Use “latest 100 evaluations” consistently. | No | Open |
| Runtime Governance | Native dialogs meet basic focus design, but no automated browser test covers Escape/focus return. | Medium | Open each dialog by keyboard, press Escape, reopen and cancel. | Focus enters dialog, Escape closes, and focus returns to trigger. | Implementation uses native dialog plus explicit focus restoration; verified by code inspection, not browser automation. | Add Playwright or equivalent accessibility smoke coverage when browser testing is introduced. | No | Open |
| Policies | Page uses legacy sidebar markup and labels `Audit` rather than `Audit Trail`. | Medium | Navigate Dashboard → Policies and compare shell/navigation. | Navigation remains visually and semantically consistent. | Policy renderer duplicates older icon-based navigation. | Migrate renderer to shared navigation in a dedicated portal consistency pass. | No | Open |
| Capabilities | Capability Explorer uses legacy navigation and the longer title `Seneschal Capability Explorer`. | Medium | Navigate from Dashboard coverage or sidebar. | Shared shell and concise page naming persist. | Older navigation icons/grouping and naming appear. | Migrate to shared navigation and standard page header later. | No | Open |
| Capabilities | Long capability identifiers are not consistently accompanied by full-value tooltips. | Low | Open a long capability at narrower width. | Truncation never hides the only accessible full value. | Some components wrap; others truncate without a `title`. | Add accessible full-value labels/tooltips where truncation is used. | No | Open |
| Identities | Sidebar `Identities` link opens raw JSON instead of a portal page. | High | Click Governance → Identities. | Navigation destination looks like part of the portal. | `/identities` returns API JSON in the browser. | Add a real identity explorer or remove the route from portal navigation until one exists. | Yes | Open; not a safe localized fix |
| Resources | Page prominently says `Resource Explorer Coming Soon`. | High | Click Governance → Resources. | Commercial demo navigation contains implemented destinations only. | A placeholder page is exposed in primary navigation. | Hide Resources from demo navigation or complete the page in a separately approved product pass. | Yes | Open; navigation/product decision required |
| Capability Activity | Table remains inside its horizontal scroll region and filtered capability link works. | Low | Open mixed activity, select `database.migration.execute`, and resize. | Table remains usable; filtered detail and audit link retain context. | No blocking defect observed. Average duration remains visually secondary. | Retain current behavior. | No | No issue requiring fix |
| Identity Activity | Activity table could exceed its panel at narrower desktop widths. | High | Open mixed activity at approximately 1024px. | Wide table scrolls within its own labeled region. | Unlike Capability Activity, it had no `.table-scroll` wrapper. | Add the same keyboard-focusable horizontal scroll region. | No | Fixed in this pass |
| Identity Activity | Uses legacy sidebar and icon strategy. | Medium | Compare with Dashboard or Governance. | Shared navigation remains consistent. | Older duplicated navigation is visible. | Migrate to shared partial in a later navigation-only pass. | No | Open |
| Audit Trail | Timeline and full table create a long, dense page; older renderer still duplicates navigation. | Medium | Generate eight or more events and open Audit Trail. | Newest evidence remains easy to locate and shell remains consistent. | Timeline precedes a second full representation; navigation is separately rendered. | Address in the explicitly deferred Audit redesign; keep filters collapsed. | No | Open |
| Audit Trail | Deny and Pending Approval filters preserve the intended question. | Low | Follow Dashboard investigation links. | Results contain only the selected decision class. | `/audit?decision=deny` and `/audit?decision=requires_approval` returned filtered HTML successfully. | Retain current links and tests. | No | No issue requiring fix |
| Audit detail | Trace links resolve and expose decision evidence. | Low | Open Audit Trail and select a trace. | Detail route returns the selected evaluation. | Existing automated coverage verifies trace rendering; no dead route found. | Retain. | No | No issue requiring fix |
| Incidents | Empty and populated states exist; Dashboard link opens the queue. | Low | Open before and after repeated qualifying denials. | Empty state is clear; populated incidents link to detail. | Existing integration/page suites cover lifecycle and links; no route failure observed. | Retain. | No | No issue requiring fix |
| Relationship Graph | Cytoscape loads from `unpkg.com`, so the main visualization can fail in an offline/restricted demo. | High | Disconnect external network and open Relationship Graph. | Demo assets load locally and deterministically. | Page shows a fallback message when the CDN script cannot load. | Vendor the pinned Cytoscape asset locally and remove runtime CDN dependence. | Yes in restricted environments | Open; dependency vendoring requires a separate approved change |
| Diagnostics | Primary navigation opens raw JSON. | High | Click System → Diagnostics. | Navigation destination is a readable portal diagnostics page, or clearly labeled as raw API. | `/diagnostics` returns JSON directly. | Add a portal diagnostics view or label the link `Diagnostics JSON` and open it as a technical endpoint. | Yes for nontechnical demo audiences | Open; product/navigation decision required |
| Shared navigation | Only Dashboard, Governance, Capability Activity, and Audit use the newer grouping; most other pages duplicate legacy icon navigation. | Medium | Traverse all sidebar routes. | Active state, group names, labels, and brand treatment remain stable. | Shell visibly changes between routes; some pages include `Governance` under Overview and use `Explore` rather than `System`. | Migrate pages/renderers to one shared navigation source in a dedicated pass. | No | Open |
| Shared navigation | Responsive sidebar becomes a long multi-group header above content. | Medium | Resize below 860px and navigate between pages. | Navigation remains accessible without obscuring the operational content. | All navigation groups expand into the document flow; no collapse control exists. | Add an accessible disclosure/navigation pattern in a scoped responsive pass. | No for desktop demo | Open |
| Portal unavailable | When the API process is down, no portal route or client-side outage event is available. | Medium | Stop API and refresh Dashboard. | Failure behavior is documented; integrations apply configured fail behavior. | Browser receives connection failure; portal cannot report its own outage. | Use platform health checks and application telemetry; future external monitoring is required. | No if API is supervised | Open; architectural limitation |
| High event count | Audit/activity stores are in memory and current retention/load behavior is not production-bounded. | Medium | Generate sustained high-volume evaluations and inspect memory/UI. | Demo remains bounded and responsive. | Dashboard is bounded to six rows, but underlying stores remain in-memory and scaling is untested. | Use bounded test traffic for demos; add retention/load work separately. | No for bounded lab | Open |

## Route and drill-down results

All returned HTTP 200 during the mixed-activity LogOnly run:

- `/dashboard`
- `/monitor`
- `/governance`
- `/policies`
- `/capability-explorer`
- `/identities` — raw JSON concern noted above
- `/resources` — placeholder concern noted above
- `/capability-activity`
- `/identity-activity`
- `/audit`
- `/incidents`
- `/graph-view`
- `/diagnostics` — raw JSON concern noted above
- `/audit?decision=deny`
- `/audit?decision=requires_approval`
- `/capability-activity?capabilityId=database.migration.execute`
- `/identity-activity?identityId=migration-worker`

Existing automated tests additionally cover audit trace links, incident detail
links, capability/identity audit links, and missing-detail states.

## State coverage

| State | Result |
|---|---|
| No runtime activity | Covered by Dashboard, Audit, Activity, Monitor, and Incident automated empty-state tests. Empty donut defect fixed. |
| Allow only | Covered by policy/client/activity tests and individual workers. |
| Mixed Allow/Deny/Pending Approval | Live four-worker review completed. |
| LogOnly | Two Allow, one Deny, and one Pending Approval observed; denial/pending projected as recorded and non-blocking. |
| Enforce | Allow continued; migration projected Blocked; approval projected Blocked pending approval. |
| Runtime unavailable | ASP.NET Core FailClosed/FailOpen tests exist; portal itself is unavailable with the API. |
| Empty incidents | Automated page coverage exists. |
| Empty audit | Automated page coverage exists. |
| Long identifiers | Code inspection identified inconsistent wrapping/tooltips; dedicated browser fixture remains missing. |
| High event count | Dashboard rendering is bounded; retention/load remains untested. |

## Accessibility basics

- Global `:focus-visible` treatment is present.
- Dashboard and governance states include text labels and do not rely on color
  alone.
- Dashboard new-event motion honors `prefers-reduced-motion`.
- Governance uses native dialogs with explicit labels, initial focus, Escape
  behavior, and trigger-focus restoration.
- Wide Capability and Identity Activity tables use focusable, labeled scroll
  regions after this pass.
- Formal keyboard and screen-reader browser automation is not present.

## Demo readiness assessment

**Conditionally demo-ready for a controlled, desktop, local environment.**

The live Dashboard, mode switch, decision semantics, activity pages, audit,
and incident flows are stable after the Blocker/High localized fixes. Before a
customer-facing walkthrough, the presenter should avoid or explicitly frame:

1. Identities, because the navigation destination is raw JSON.
2. Resources, because it is visibly a coming-soon placeholder.
3. Diagnostics, because it is raw JSON.
4. Relationship Graph when external network access is not guaranteed.

Those four open High issues prevent an unconditional “all navigation is demo
ready” assessment and require product/navigation or dependency decisions
beyond a safe localized QA fix.
