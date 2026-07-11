# Seneschal Validation Strategy

## Purpose

Seneschal requires two validation lenses because a correct policy decision is
not sufficient evidence of safe runtime operation.

- **Application Validation** proves that governance decisions, enforcement,
  evidence, and portal representations are correct.
- **Infrastructure / Operational Validation** proves that Seneschal and its
  integrations behave safely during runtime, network, configuration, and
  platform failures.

Status terms used below:

- **Automated** — exercised by a repeatable repository test.
- **Partial** — some behavior is automated, but important boundaries remain.
- **Manual** — a documented or repeatable human-run check exists.
- **Missing** — no adequate validation asset currently exists.

## 1. Application Validation

### Decision and enforcement behavior

| Scenario | Objective | Expected result | Current test coverage | Manual validation | Automation | Remaining gaps |
|---|---|---|---|---|---|---|
| Allow permits execution | Prove an allow decision is returned and honored. | Decision is Allow; LogOnly and Enforce both project execution. | Policy evaluator, decision service, API contract, client, and Dashboard derivation tests. | Run DeploymentWorker or RefundWorker in both modes; confirm `operation=EXECUTED`. | Automated + lab | Seneschal does not confirm that the external operation completed. |
| Deny in LogOnly | Prove monitoring records a denial without blocking. | Deny is returned and recorded; projected action is `Executed and recorded`. | Decision service, activity, audit, client/middleware, and Dashboard tests. | Run DatabaseMigrationWorker in LogOnly; inspect console, Dashboard, audit, and activity. | Automated + lab | External execution remains caller-reported behavior. |
| Deny in Enforce | Prove enforcement blocks a denied operation. | Deny is returned; integration blocks before its simulated operation. | Middleware and Dashboard action-derivation tests; policy tests. | Switch to Enforce and rerun DatabaseMigrationWorker. | Automated + lab | No cross-SDK conformance suite yet. |
| PendingApproval in LogOnly | Prove pending approval is visible but non-blocking in monitoring. | PendingApproval is recorded; projected action is `Executed and recorded`. | Core activity, mapper, Dashboard, policy, and ApprovalWorker configuration tests. | Run ApprovalWorker in LogOnly and inspect all live views. | Automated + lab | No approval request is created. |
| PendingApproval in Enforce | Prove pending approval blocks while enforcement is active. | Projected action is `Blocked pending approval`; simulated operation does not run. | Dashboard derivation and ASP.NET Core non-allow enforcement coverage. | Switch to Enforce and rerun ApprovalWorker. | Automated + lab | No approve, reject, resume, or expiry workflow. |
| Default deny | Prevent ungoverned requests from being implicitly allowed. | A request without a matching policy receives Deny with the default-deny reason. | Policy loader, evaluator, decision service, Monitor, and API tests. | Evaluate an unmatched identity/capability pair with a valid scoped key. | Automated | Default-deny behavior should be included in every future SDK conformance suite. |

### Request mapping and authorization

| Scenario | Objective | Expected result | Current test coverage | Manual validation | Automation | Remaining gaps |
|---|---|---|---|---|---|---|
| Scoped API keys | Prove keys cannot exceed configured identity, capability, or environment scope. | Valid scope evaluates; missing/invalid keys return 401; disabled or out-of-scope keys return 403. | `EvaluateApiKeyAuthorizationTests`, loader tests, and ApprovalWorker key-scope test. | Invoke `/evaluate` with valid, invalid, and cross-worker keys. | Automated | Expiry, rotation, revocation propagation, and secret-backed storage are absent. |
| Identity mapping | Preserve the caller identity used by policy evaluation and evidence. | Configured/resolved identity reaches evaluation, audit, and activity unchanged. | Client request tests, ASP.NET Core identity resolver tests, mapper and activity tests. | Use distinct workers and compare console, Dashboard, audit, and Identity Activity. | Automated + lab | No canonical enterprise identity normalization policy. |
| Capability mapping | Preserve the governed operation identifier. | Capability reaches evaluation and evidence unchanged and matches key/policy scope. | Mapper, authorization, catalog, activity, and Dashboard tests. | Exercise each worker and inspect Capability Activity. | Automated + lab | Naming/versioning conventions are guidance rather than enforced compatibility rules. |
| Resource and environment mapping | Apply policy and key scope to the correct operational context. | Resource and environment are transmitted; environment conditions and key scope are enforced. | Mapper, client serialization, policy conditions, and environment-key tests. | Change a worker environment or resource and verify the resulting decision/evidence. | Partial | Resource matching is less comprehensively tested than environment matching. |
| Safe response bodies | Avoid returning secrets or internal exception details. | Authorization and unavailable responses contain safe reasons; diagnostics omit raw policies and secrets. | API-key, ASP.NET Core hardening, diagnostics, and contract tests. | Send invalid requests and review 401, 403, and unavailable bodies. | Automated | No automated broad secret-pattern scan across every response and log. |

### Evidence, portal, and operational interpretation

| Scenario | Objective | Expected result | Current test coverage | Manual validation | Automation | Remaining gaps |
|---|---|---|---|---|---|---|
| Dashboard live updates | Show current mode, recent evaluations, active identities, distribution, and projected actions. | Polling updates live regions every three seconds and distinguishes Allow, Deny, and PendingApproval. | Dashboard HTML, JSON safety, ordering, idle threshold, and derivation tests. | Run all four workers, keep Dashboard open, then change mode without reloading. | Partial + manual | Browser timing, focus preservation, long-duration polling, and accessibility announcements lack end-to-end automation. |
| Audit events | Preserve evidence for every completed evaluation. | Decision, identity, capability, environment, policy, reason, mode, time, and trace are available. | Decision service, audit endpoint, filtering, timeline, and detail tests. | Run an evaluation; locate it in Audit Trail and open its trace. | Automated | In-memory retention is not durable or production-bounded. |
| Capability Activity | Aggregate runtime usage by capability. | Counts, decisions, recency, and selected capability details reflect evaluations. | Activity store and Capability Activity page tests. | Run workers and compare per-capability totals with console output. | Automated + manual | Time-windowed trends and scalable aggregation are absent. |
| Identity Activity | Aggregate runtime usage by caller. | Identity totals and capability usage reflect evaluations. | Activity store and Identity Activity page tests. | Run workers and verify each identity and latest activity. | Automated + manual | Durable identity history and high-cardinality behavior are untested. |
| Incident generation | Aggregate repeated qualifying denials and support lifecycle actions. | Repeated denials create/aggregate incidents; acknowledge and resolve transitions work. | Incident endpoint and portal page suites. | Generate repeated denials; inspect, acknowledge, and resolve the incident. | Automated | Persistence, notification, deduplication under concurrency, and production thresholds remain open. |
| Readiness and Monitor | Present configuration readiness, observed activity, drift, and enforcement guidance consistently. | Endpoints and Monitor render deterministic state from current configuration and activity. | Health/diagnostics and extensive Monitor tests. | Start clean, run evaluations, and compare `/ready`, `/diagnostics`, and Monitor. | Automated | Readiness is product-level evidence, not a full platform dependency health model. |
| Runtime mode changes | Apply LogOnly/Enforce immediately and expose the active mode. | New evaluations use the new mode without restart; diagnostics and Dashboard reflect it. | Governance page, diagnostics, audit-mode, and Dashboard tests. | Switch mode while workers run and observe changed projected actions. | Automated + lab | Change is in memory and is not an administrative audit event. |
| Decision trace accuracy | Make the reason for a decision inspectable. | Trace agrees with decision, matched policy, mode, context, and reason. | Audit detail and mapping tests. | Compare an API response with its audit detail page. | Automated + manual | No immutable or signed evidence; complex multi-policy trace depth is limited. |
| Multi-application adoption lab | Validate independent packaged clients against one runtime. | Four workers show two Allow, one Deny, and one PendingApproval pattern across both modes. | Worker policy/key tests and supporting API/Dashboard tests. | Run API plus Deployment, Migration, Refund, and Approval workers. | Manual, repeatable | The full orchestration and assertions are not yet one CI test. |

## 2. Infrastructure / Operational Validation

### Dependency and network failures

| Scenario | Objective | Expected result | Current test coverage | Manual validation | Automation | Remaining gaps |
|---|---|---|---|---|---|---|
| Seneschal.Api unavailable | Verify integrations follow configured failure behavior. | FailClosed blocks safely; FailOpen continues intentionally; response does not leak internals. | ASP.NET Core unavailable-runtime tests. | Stop API during requests and compare both failure modes. | Partial | Direct-client application guidance and portal-visible outage evidence need definition. |
| DNS failure | Treat name-resolution failure as runtime unavailability. | Failure is bounded by timeout and mapped to configured fail behavior. | General middleware exception coverage only. | Point BaseUrl at an unresolvable hostname. | Missing/Manual | No dedicated DNS fault test or classified telemetry. |
| Network timeout | Prevent indefinite evaluation waits. | Configured client timeout terminates the call and invokes fail behavior. | Option validation and some failure-path tests. | Route to a non-responsive endpoint and measure elapsed time. | Partial | No deterministic timeout integration test across client and middleware. |
| High latency | Understand governance overhead and application impact. | Calls remain bounded; latency is observable; no duplicate operation occurs. | Evaluation-duration metrics exist. | Add a delaying proxy/handler and test below and above timeout. | Missing/Manual | No latency SLO, percentile test, or degradation policy. |
| TLS/certificate failure | Fail safely when server identity cannot be validated. | TLS error invokes configured failure behavior; certificates are not bypassed. | None specific. | Use an untrusted/expired local certificate. | Missing | No automated certificate-expiry or trust-chain scenarios. |
| Malformed response | Prevent malformed payloads from becoming implicit allows. | Client throws a controlled error; middleware applies FailClosed/FailOpen configuration. | Client non-success/deserialization coverage is partial. | Return invalid JSON, missing fields, and unknown decision values from a stub server. | Partial | A formal response compatibility and unknown-enum policy is needed. |
| Recovery after outage | Prove callers resume evaluation after service recovery. | Later calls succeed without process restart or stale failure state. | No dedicated recovery sequence. | Stop API, issue requests, restart API, and repeat. | Missing/Manual | No automated outage/recovery timeline or recovery-time target. |

### Authentication and client behavior

| Scenario | Objective | Expected result | Current test coverage | Manual validation | Automation | Remaining gaps |
|---|---|---|---|---|---|---|
| Invalid or expired API key | Reject unauthenticated integration traffic safely. | Invalid key returns 401; no policy evaluation occurs. Expiry is not implemented. | Invalid and missing key tests. | Send a bad key and inspect response and evidence surfaces. | Partial | Key expiry is not a current capability. Pre-evaluation rejection auditing is absent. |
| Out-of-scope API key | Contain a valid key to declared scope. | Request returns 403 before evaluation. | Identity, capability, and environment scope tests. | Use one worker key for another worker. | Automated | Rejected requests are not represented in normal decision activity. |
| FailClosed | Preserve safety when a decision cannot be obtained. | Protected operation is blocked with a safe unavailable response. | ASP.NET Core hardening tests. | Stop API with FailClosed configured and invoke an endpoint. | Automated + manual | Direct-client users must implement equivalent control correctly. |
| FailOpen | Preserve explicitly selected availability behavior. | Operation continues when evaluation is unavailable. | ASP.NET Core hardening tests. | Stop API with FailOpen configured and invoke an endpoint. | Automated + manual | No dedicated audit signal can reach the unavailable portal; local telemetry contract is incomplete. |
| Client retry behavior | Establish whether calls are retried and prevent duplicate evaluation ambiguity. | Current client performs no documented retry policy. | No retry framework tests because none is implemented. | Observe request count against a failing stub. | Current: none | Retry/backoff/circuit-breaker behavior must be designed before testing. |

### Runtime, configuration, packaging, and state

| Scenario | Objective | Expected result | Current test coverage | Manual validation | Automation | Remaining gaps |
|---|---|---|---|---|---|---|
| Runtime restart | Understand restart semantics. | API restarts from configured files; in-memory mode, audit, activity, incidents, and metrics reset. | Configuration startup and mode defaults are covered separately. | Record activity/change mode, restart, then inspect Dashboard and diagnostics. | Partial/Manual | Reset semantics need an explicit operational contract. |
| Process crash and restart | Verify recovery after an ungraceful termination. | Process can restart from application-owned configuration without corrupt state. | No crash-injection test. | Kill the API process and relaunch the compiled DLL. | Missing/Manual | No supervisor, HA, or recovery-time expectation exists. |
| Configuration path resolution | Remove dependency on process working directory. | Relative paths resolve from content root; absolute paths remain supported. | `YamlConfigurationLoaderPathTests`. | Launch compiled API from repository root and another directory. | Automated + smoke | Reload/rotation behavior is not implemented. |
| Package installation and dependency resolution | Ensure a clean application can consume packages without source references. | Local NuGet packages install and a clean ASP.NET Core sample builds. | Package-only smoke work and package metadata validation are manual assets. | Pack, create clean app, add local source/packages, build. | Manual, repeatable | Not a committed CI gate; restore behavior depends on source availability. |
| Package upgrade/version compatibility | Prevent unexpected SDK/API incompatibility. | Supported version combinations compile and preserve wire behavior. | Current alpha package is tested only against current source. | Upgrade a clean sample between produced versions and run contract calls. | Missing | Compatibility policy, matrix, deprecation rules, and upgrade tests are absent. |
| Logging and secret redaction | Keep keys and sensitive configuration out of logs and responses. | API keys are not rendered; validation errors do not echo secret values. | Configuration hardening and diagnostics safety tests. | Search API/client logs and HTTP bodies after failures. | Partial | No repository-wide structured-log redaction test or log schema. |
| In-memory state reset | Make volatile-state loss explicit and predictable. | Restart clears current runtime evidence and restores LogOnly. | Individual stores and mode behavior are tested; restart sequence is not. | Populate all stores, restart, inspect each surface. | Partial/Manual | No durable alternative exists. |
| Audit persistence limitations | Prevent treating current audit data as durable evidence. | Current events exist only for process lifetime. | In-memory store behavior is tested. | Generate events, restart, confirm loss. | Current limitation | Persistence, migration, retention, backup, and restore are future work. |

### Scale and longevity

| Scenario | Objective | Expected result | Current test coverage | Manual validation | Automation | Remaining gaps |
|---|---|---|---|---|---|---|
| High-volume evaluation | Identify throughput, allocation, retention, and UI limits. | Defined throughput/latency targets are met without unbounded degradation. | Small functional aggregation tests only. | Generate sustained concurrent `/evaluate` traffic and inspect memory, latency, stores, and portal. | Missing | No load targets, bounded retention, or production-scale evidence. |
| Concurrency and lock contention | Verify correctness and latency with simultaneous evaluations and mode changes. | Counts remain correct; no races, deadlocks, or excessive lock waits. | Limited repository-level concurrency confidence; no focused load suite. | Run parallel workers/load while repeatedly changing mode. | Missing | Store and mode-lock contention are not characterized. |
| Long-running portal polling | Ensure Dashboard polling remains stable. | One request every three seconds while visible; pauses when hidden; DOM and memory remain stable. | JavaScript behavior is code-reviewed; server handler has functional tests. | Leave Dashboard open for hours, switch visibility, monitor requests and memory. | Partial/Manual | No browser endurance or accessibility automation. |

## 3. Current Validation Assets

| Asset | Current use | Limitation |
|---|---|---|
| Unit tests | Policy evaluation, mappings, repositories, loaders, client behavior, middleware options, and presentation derivation. | Primarily deterministic in-process behavior. |
| API integration tests | HTTP contracts, authorization, audit, activity, incidents, Monitor, diagnostics, portal HTML, and mode changes. | Uses in-memory hosting and stores. |
| Package-only smoke tests | Confirms generated NuGet packages can be used without source project references. | Manually orchestrated; not a standard CI job. |
| Multi-application adoption lab | Four independent console workers exercise Allow, Deny, and PendingApproval. | Manual startup and assertions. |
| Runtime governance demo | Demonstrates LogOnly versus Enforce without restarting. | Mode is process-local and volatile. |
| Dashboard Live Operations | Shows live decisions, active identities, distribution, and projected actions. | Polling and browser longevity are not end-to-end automated. |
| Readiness and diagnostics endpoints | Expose configuration state, counts, mode, and component types without raw secrets. | Do not replace platform health, dependency, or HA monitoring. |
| Manual LogOnly / Enforce validation | Confirms worker output and portal behavior across a live mode switch. | Repeatability depends on operator discipline until scripted. |

## 4. Recommended Test Environments

### Local development

- Build and run the full automated suite.
- Pack the client and ASP.NET Core packages locally.
- Run the package-only sample and four-worker adoption lab.
- Exercise Dashboard, Audit Trail, activity pages, incidents, diagnostics, and
  both runtime modes.
- Use short-lived local fault stubs for malformed responses and timeouts.

This environment validates developer feedback and demo integrity. It does not
establish durability, scale, or platform resilience.

### CI

- Run clean restore, build, test, and pack jobs.
- Validate package contents and install packages into a generated clean app.
- Automate the bounded four-worker LogOnly/Enforce scenario.
- Add deterministic API outage, timeout, malformed-response, invalid-key, and
  recovery tests.
- Retain test results and package artifacts.

CI should avoid tests that depend on public ports, arbitrary sleeps, or
long-running processes without explicit lifecycle management.

### Pre-release / staging

- Deploy the same versioned artifacts intended for release.
- Use non-production keys and representative policy/configuration files.
- Validate TLS, DNS, process restart, configuration mounting, key rotation,
  rollback, logging, and operational access.
- Run moderate sustained traffic and portal endurance checks.

No dedicated staging infrastructure is implied to exist today. This is the
minimum environment needed before a private pilot expands.

### Production-like resilience test environment

- Use production-equivalent network boundaries, TLS, process supervision,
  storage design, observability, and deployment topology once those exist.
- Inject API outages, latency, packet loss, restarts, and dependency failures.
- Validate recovery time, data retention, backup/restore, concurrency, and load
  against explicit targets.

This environment is future work. It should not be claimed until persistence,
HA, retention, and operational expectations have been designed.

## 5. Release Gates

| Release stage | Minimum gate |
|---|---|
| **Local alpha** | Clean build and tests pass; packages pack; package-only clean sample builds; four-worker adoption lab passes in LogOnly and Enforce; known volatile-state limitations are documented. |
| **Private pilot** | API outage/recovery, timeout, TLS, invalid/scope key, restart, rollback, and recovery scenarios are validated; secret handling is reviewed; persistent-storage plan is defined; operational ownership and escalation path are documented. |
| **Public beta** | Package publishing is automated; compatibility and support policy exists; supported failure behavior is documented; upgrade tests and customer troubleshooting are available; bounded performance and portal-endurance results exist. |
| **Production release** | Durable persistence and retention are implemented; HA/recovery and backup/restore expectations are tested; load, concurrency, and failure-injection targets pass; security review and operational runbooks are complete; administrative controls produce durable evidence. |

A later-stage gate must include all earlier-stage gates. A waiver should name
the risk owner, scope, expiry, and compensating control.

## 6. Open Gaps

- Approval workflow is not implemented. PendingApproval currently represents a
  blocking decision, not an approver queue or resumable operation.
- Runtime mode changes are not administrative audit events.
- Runtime, audit, incident, metric, and activity data are in memory.
- No durable persistence, retention, backup, or restore capability exists.
- No retry, backoff, or circuit-breaker framework is provided.
- Fail-open events lack a dedicated audit signal.
- Client-side outage events cannot reach the portal while Seneschal is
  unavailable.
- Rejected integration authentication requests do not enter normal decision
  evidence.
- High-volume retention, concurrency, memory use, and scaling remain untested.
- Package compatibility and upgrade guarantees are not defined.
- Browser polling endurance and accessibility behavior are not automated.

## 7. Immediate Next QA Priorities

1. **Automate the four-worker scenario.** Start one API and four package-only
   workers, assert the LogOnly distribution, switch mode, and assert Enforce
   outcomes and Dashboard JSON without restarting.
2. **Add outage and recovery validation.** Cover unavailable API, bounded
   timeout, FailClosed, FailOpen, restart, and successful recovery.
3. **Add invalid-key and scope-failure operational tests.** Assert status,
   safe bodies, logging/redaction, and the intentional evidence gap.
4. **Add mode-switch administrative audit coverage after the product signal is
   implemented.** Validate actor, old/new mode, reason, timestamp, and durable
   evidence expectations.
5. **Add bounded high-volume coverage.** Establish an initial request count,
   concurrency level, duration, latency budget, and memory ceiling; fail the
   test on lost counts or unbounded growth.

