# Governance Lifecycle

Capability governance should be introduced progressively. Organizations need to
understand capabilities, observe real usage, and review policy outcomes before
turning on strict enforcement.

Seneschal supports a four-stage governance lifecycle:

1. Discover
2. Monitor
3. Enforce
4. Optimize

Each stage increases confidence. The model is intended to reduce operational
risk while moving steadily toward governed runtime capability use.

## Discover

### Purpose

Establish an inventory of capabilities, identities, policies, and relationships.

### Organization Goals

- Understand which capabilities exist.
- Identify who owns or uses those capabilities.
- Map policies to capabilities, identities, and resources.
- Build a shared model before changing runtime behavior.

### What Seneschal Provides

- Capability catalog.
- Capability Explorer.
- Policy Explorer.
- Identity views.
- Governance Graph.
- Interactive relationship graph.
- Dashboard inventory summaries.

### Expected Outcomes

- Teams can see the capability surface area.
- Governance relationships become explainable.
- Missing ownership, unclear policy scope, and risky capabilities become visible.

## Monitor

### Purpose

Observe runtime capability activity without blocking operations.

### Organization Goals

- See which capabilities are exercised at runtime.
- Understand which identities are active.
- Measure allow, deny, and pending approval patterns.
- Validate policies before enforcing them.

### What Seneschal Provides

- Runtime decision API.
- .NET SDK.
- ASP.NET middleware.
- `RequiresCapability` attribute.
- Audit trail.
- Runtime activity aggregation.
- Dashboard activity widgets.

### Expected Outcomes

- Teams understand real usage instead of relying only on static configuration.
- Policy mismatches are detected before enforcement causes disruption.
- Runtime decisions become auditable and explainable.

## Enforce

### Purpose

Require applications and middleware to honor Seneschal runtime decisions.

### Organization Goals

- Block denied capability use.
- Route pending approval decisions appropriately.
- Keep enforcement behavior deterministic.
- Ensure runtime adapters do not reimplement policy logic.

### What Seneschal Provides

- Decision API.
- Core policy evaluation.
- Runtime decisions.
- ASP.NET middleware.
- `RequiresCapability` attribute.
- SDK-based integration path.
- Audit records for enforced decisions.

### Expected Outcomes

- Capability use is governed consistently at runtime.
- Applications rely on Seneschal for decision logic.
- Enforcement becomes traceable through audit and activity views.

## Optimize

### Purpose

Continuously improve policies, ownership, and governance posture using observed
runtime activity.

### Organization Goals

- Identify stale, noisy, or overly broad policies.
- Review frequently denied capabilities.
- Detect active identities and capability hotspots.
- Improve governance coverage over time.

### What Seneschal Provides

- Activity Explorer views.
- Capability Activity.
- Identity Activity.
- Audit insights.
- Governance Graph.
- Interactive graph exploration.

### Expected Outcomes

- Policies become more accurate.
- Governance gaps become visible.
- Teams can evolve from reactive review to continuous governance.

## Recommended Adoption Timeline

An example adoption path:

```text
Inventory
↓
Monitor
↓
Review Activity
↓
Enable Enforcement
↓
Continuous Optimization
```

Example progression:

- Week 1: inventory capabilities, identities, policies, and relationships.
- Week 2: enable monitor-mode runtime integrations.
- Week 3: review audit history and runtime activity.
- Week 4: enable enforcement for low-risk or well-understood capability paths.
- Ongoing: optimize policies, ownership, and governance relationships.

The timeline is intentionally progressive. Teams should avoid enforcing broad
capability policies before they understand the operational impact.

## Lifecycle Feature Mapping

| Lifecycle Stage | Goal | Current Features |
| --- | --- | --- |
| Discover | Build inventory and relationship understanding. | Dashboard, Capability Explorer, Policy Explorer, Identity Explorer, Interactive Graph |
| Monitor | Observe runtime decisions without disrupting operations. | Runtime API, SDK, Middleware, Audit, Runtime Activity |
| Enforce | Apply deterministic capability decisions at runtime. | Runtime decisions, ASP.NET middleware, `RequiresCapability` attribute, Decision API |
| Optimize | Improve governance using observed activity and relationships. | Activity Explorer, Capability Activity, Identity Activity, Audit Insights, Governance Graph |

## Sprint 9 Roadmap

### Commit 1: Governance Lifecycle Documentation

Document the operational adoption model for capability governance.

### Commit 2: Monitor Mode Dashboard

Add dashboard indicators that distinguish monitor-mode activity from enforced
runtime decisions.

### Commit 3: Policy Recommendation Engine

Introduce recommendations derived from runtime activity, audit outcomes, and
governance relationships.

### Commit 4: Enforcement Readiness Assessment

Assess whether a capability, identity, or policy path is ready for enforcement
based on observed activity and audit history.

### Commit 5: Governance Health Dashboard

Summarize governance coverage, activity, denials, pending approvals, stale
policies, and high-risk capabilities in one operational view.
