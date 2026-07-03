# ADR-0001: Request-Centric Runtime

**Status:** Accepted

**Date:** 2026-07-03

---

# Context

Seneschal governs capability usage across AI agents, services, workflows, and other autonomous systems.

Early design discussions considered Capabilities as the center of the platform. However, Policies do not evaluate Capabilities in isolation. Policies evaluate an attempted use of a Capability by an Identity within a specific Context.

---

# Decision

Seneschal will use Requests as the central runtime object.

A Request represents an Identity asking to perform a Capability against an optional Resource within a given Context.

Policies evaluate Requests.

Decisions are produced from Requests.

Audit Events record the lifecycle of Requests and Decisions.

---

# Consequences

- The primary runtime API is request-oriented.
- SDKs, REST APIs, CLI commands, and integrations should model Request submission as the primary interaction.
- Capabilities remain static definitions in the Capability Catalog.
- Identities remain independent managed subjects.
- Audit timelines are centered around Requests.