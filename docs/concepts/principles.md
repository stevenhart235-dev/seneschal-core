# Principles

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Principles

The following principles guide every architectural and product decision within Seneschal.

These principles are intentionally technology-independent and are expected to remain stable as the platform evolves.

---

# Governance First

Every capability should be understood before it is trusted.

Governance should occur before actions are executed, not after incidents have occurred.

---

# Least Privilege

Identities should possess only the capabilities required to perform their intended function.

Additional permissions should be explicitly granted rather than implicitly assumed.

---

# Explicit Authorization

Every capability invocation should produce an authorization decision.

Authorization decisions should never be implicit or hidden.

---

# Observable by Default

Governance without visibility is incomplete.

Every authorization decision, policy evaluation, and significant capability invocation should be observable without requiring additional instrumentation.

---

# Explainable Decisions

Every authorization decision should include an explanation.

Operators should understand:

- Which policies were evaluated
- Which conditions matched
- Why the request was allowed
- Why the request was denied
- Whether approval was required

---

# Vendor Neutral

Governance should remain independent of AI providers, orchestration frameworks, and runtime implementations.

Organizations should be free to adopt new technologies without replacing their governance platform.

---

# Policy as Code

Policies should be version-controlled, testable, reviewable, and deployable through existing engineering workflows.

---

# Progressive Enforcement

Organizations should adopt governance incrementally.

Seneschal supports multiple enforcement modes:

- Discover
- Audit
- Log Only
- Require Approval
- Enforce

Organizations should determine when policies become mandatory.

---

# Human Control

Organizations retain ultimate authority.

High-risk capabilities should support human approval workflows before execution.

---

# Secure by Default

Default platform behavior should favor safety over convenience.

When uncertainty exists, organizations should be able to choose conservative enforcement strategies.

---

# Simplicity

Governance should reduce operational complexity rather than introduce it.

Policies should be understandable, predictable, and easy to reason about.

---

# Extensibility

New AI frameworks, runtimes, providers, and capability types should integrate into the existing governance model without requiring architectural changes.
