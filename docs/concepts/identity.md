# Identity

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Definition

An Identity represents any entity capable of requesting one or more Capabilities within the Seneschal platform.

Identities are the primary subjects of governance. Every authorization request originates from an Identity.

An Identity does not inherently possess permissions. Permissions are granted through Capabilities and governed by Policies.

---

# Purpose

The Identity model provides a consistent way to represent humans, AI systems, services, and autonomous workloads using a common governance model.

This abstraction allows policies to remain independent of implementation details such as AI frameworks, cloud providers, or runtime environments.

---

# Responsibilities

An Identity may:

- Submit Requests
- Possess Capabilities
- Be evaluated by Policies
- Generate Decisions
- Produce Audit Events
- Participate in Approval Workflows

---

# Identity Types

The following identity types are supported by the platform.

## Human

Represents an individual user or operator.

Examples:

- Administrator
- Security Engineer
- Auditor

---

## Agent

Represents an AI agent or autonomous system.

Examples:

- Customer Support Agent
- Finance Assistant
- Platform Engineer Agent

---

## Service

Represents an application, microservice, or API.

Examples:

- Payment API
- Notification Service
- Inventory Service

---

## Workflow

Represents an automated workflow or orchestration.

Examples:

- CI/CD Pipeline
- Scheduled Job
- Event-Driven Workflow

---

## MCP Server

Represents an external capability provider exposed through the Model Context Protocol (MCP).

---

## External

Represents identities managed outside of Seneschal.

Examples:

- Third-party SaaS
- External API
- Partner Platform

---

# Required Properties

| Property | Description |
|----------|-------------|
| Id | Globally unique identifier |
| Name | Human-readable name |
| Type | Identity classification |
| Status | Current lifecycle state |

---

# Optional Properties

| Property | Description |
|----------|-------------|
| Description | Additional context |
| Owner | Responsible team or individual |
| Labels | Searchable tags |
| Metadata | Arbitrary key/value data |
| CreatedAt | Creation timestamp |
| UpdatedAt | Last modification timestamp |
| Version | Resource version |

---

# Status Values

An Identity may exist in one of the following states.

- Draft
- Active
- Disabled
- Deprecated
- Deleted

---

# Relationships

Identity

↓

submits

↓

Request

↓

references

↓

Capability

↓

evaluated by

↓

Policy

↓

produces

↓

Decision

↓

recorded as

↓

Audit Event

---

# Lifecycle

1. Identity Created
2. Metadata Assigned
3. Capabilities Assigned
4. Policies Applied
5. Identity Activated
6. Requests Evaluated
7. Audit History Accumulated
8. Identity Retired

---

# Design Principles

An Identity should:

- Have a stable identifier.
- Be independent of runtime implementation.
- Support multiple capability assignments.
- Be discoverable.
- Be observable.
- Be auditable.

---

# Future Considerations

Future versions of Seneschal may support:

- Identity Groups
- Identity Hierarchies
- Dynamic Identity Discovery
- Identity Federation
- Identity Risk Scoring
- Temporary Identities
- Identity Trust Levels

These features are intentionally outside the scope of Version 1.

---

# Non-Goals

An Identity is not:

- An authentication provider
- A user account
- A role
- A policy
- A capability
- A permission

Identity answers the question:

**"Who is requesting this action?"**
