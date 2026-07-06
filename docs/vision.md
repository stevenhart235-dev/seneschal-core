# Seneschal Vision

## Why Seneschal Exists

Organizations increasingly operate through capabilities exposed by cloud
platforms, automation systems, internal services, external APIs, and AI agents.
The practical unit of organizational power is no longer only a server, role, or
credential. It is the ability to do something: deploy software, read a secret,
modify infrastructure, approve a payment, query sensitive data, or invoke a
tool.

Traditional security systems focus primarily on infrastructure, networks,
applications, and identities. Those remain essential, but they do not fully
answer which capabilities exist, who or what can use them, which resources they
affect, and why a decision was made.

Seneschal exists to govern capabilities. It provides a platform-agnostic model
for understanding, explaining, and eventually enforcing how capabilities are
used across modern software systems.

## Product Philosophy

Seneschal is a capability governance platform, not simply a policy engine.

Policy evaluation is one part of the system, but the product is broader than a
runtime allow-or-deny decision. Capabilities need to be cataloged, related to
identities and resources, explained through governance relationships, observed
over time, and surfaced through explorer-style read models.

Policies, catalogs, graphs, and explorers are parts of one system. Each owns a
clear responsibility, and together they describe how organizational
capabilities are understood, governed, and enforced.

## Core Principles

- Capabilities are first-class.
- Decisions are deterministic.
- Governance is explainable.
- Observation precedes enforcement.
- Relationships are projected, never manually maintained.
- Core remains platform-agnostic.
- Runtime adapters should not own business logic.

## Architectural Pillars

### Decision Engine

The Decision Engine evaluates requests against policies and produces
deterministic, explainable decisions. It is responsible for runtime evaluation
and decision resolution, and it remains independent from transport adapters,
storage mechanisms, and platform-specific integrations.

### Capability Catalog

The Capability Catalog describes known capabilities and their product metadata:
identity, provider, category, description, risk, ownership, versioning, and
tags. It is the authoritative inventory boundary for capabilities, not an
evaluation mechanism.

### Governance Graph

The Governance Graph represents relationships between identities,
capabilities, resources, policies, and future observed entities. It is a read
model populated by projectors from authoritative sources. It supports
explanation, exploration, audit, and visualization without becoming a source of
truth itself.

### Capability Explorer

The Capability Explorer composes the Capability Catalog and Governance Graph
into capability-centered read models. It helps users understand what a
capability is, who or what is related to it, which policies govern it, and what
has been observed around it. It owns no data.

## Progressive Enforcement

Seneschal supports governance as an incremental path:

Observe
↓
Understand
↓
Govern
↓
Enforce

Organizations should not be forced to begin with hard enforcement. Capability
governance is most effective when teams first observe real behavior, understand
relationships and risk, encode governance intent, and then selectively enforce
where confidence and business context justify it.

This progression allows Seneschal to support discovery, audit, simulation,
approval workflows, and enforcement without requiring every organization to
adopt the most restrictive posture on day one.

## Long-Term Direction

Seneschal should integrate with the systems where capabilities are declared,
configured, invoked, and observed, including:

- Azure
- Terraform
- GitHub
- Kubernetes
- MCP
- AI Agents

These integrations should become sources projected into the Governance Graph.
They should not become independent policy engines. Azure resources, Terraform
state, GitHub repositories, Kubernetes workloads, MCP tools, and AI agent
manifests each remain authoritative for their own domain. Seneschal translates
their relevant facts into a common governance relationship model.

This keeps the Core platform-agnostic while allowing the product to grow across
cloud, infrastructure, software delivery, automation, and AI ecosystems.

## Closing Statement

Seneschal exists to become the system of record for organizational
capabilities, enabling organizations to understand, govern, and safely evolve
the capabilities that power modern software.
