# Runtime

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Overview

The Seneschal Runtime is responsible for evaluating Requests and producing explainable Decisions.

The runtime is intentionally stateless.

It orchestrates evaluation by retrieving the required domain objects, executing policy evaluation, producing a Decision, and recording immutable Audit Events.

---

# Runtime Flow

Every Request follows the same lifecycle.

Identity

↓

Submit Request

↓

Load Identity

↓

Load Capability

↓

Load Applicable Policies

↓

Evaluate Request

↓

Produce Decision

↓

Generate Audit Events

↓

Return Response

---

# Responsibilities

The Runtime is responsible for:

- Receiving Requests
- Loading required domain objects
- Executing policy evaluation
- Producing Decisions
- Publishing Audit Events

The Runtime is NOT responsible for:

- Identity management
- Capability management
- Policy authoring
- Audit retention

---

# Stateless Design

The Runtime should remain stateless.

Persistent data is owned by the Identity Registry, Capability Catalog, Policy Store, and Audit Store.

The Runtime coordinates these components without owning their data.

---

# Deterministic Evaluation

Given:

- the same Request
- the same Policies
- the same Identity
- the same Capability

the Runtime must always produce the same Decision.

---

# Explainability

Every Decision produced by the Runtime must be explainable.

The Runtime should be capable of answering:

- Which Policies were evaluated?
- Which Policies matched?
- Which Policy produced the final Effect?
- Why was the Request allowed?
- Why was the Request denied?

---

# Extensibility

The Runtime should support future capabilities including:

- Approval workflows
- Distributed execution
- Event streaming
- Policy simulation
- Dry-run evaluation
- Multi-stage authorization