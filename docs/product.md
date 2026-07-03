# Product

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Product Overview

Seneschal is an AI governance platform that enables organizations to discover, govern, authorize, monitor, and audit AI capabilities across enterprise environments.

Rather than replacing AI frameworks or orchestration platforms, Seneschal provides the governance layer responsible for understanding what AI systems can do, determining what they should be allowed to do, and recording every decision for operational visibility and compliance.

---

# Problem Statement

As organizations adopt AI agents, assistants, and autonomous workflows, they face a new governance challenge.

Existing security and identity platforms were designed around users and applications—not autonomous systems capable of making decisions and executing actions.

Organizations often cannot answer critical questions:

- Which AI agents exist?
- What capabilities do they possess?
- Which resources can they access?
- Which policies govern their actions?
- Why was a specific request approved or denied?
- What capabilities have changed since the last audit?

Without centralized governance, organizations risk inconsistent policies, limited visibility, and increasing audit complexity.

---

# Solution

Seneschal provides a centralized governance platform for AI capabilities.

It continuously discovers AI identities and capabilities, evaluates requests against organizational policies, records every authorization decision, and presents operational insights through a unified dashboard.

---

# Target Customers

Seneschal is designed for organizations building or operating AI-enabled systems.

Primary users include:

- Platform Engineering
- Cloud Engineering
- Security Engineering
- Site Reliability Engineering (SRE)
- Enterprise Architecture
- Compliance and Audit
- DevSecOps

---

# Core Capabilities

## Discovery

Discover AI identities, capabilities, and integrations across supported platforms.

---

## Policy Management

Define policies that determine which actions are permitted, denied, or require approval.

---

## Decision Engine

Evaluate requests against organizational policies before actions occur.

---

## Audit

Record every policy evaluation and authorization decision.

---

## Observability

Provide dashboards, timelines, and operational insights into AI behavior.

---

# Version 1 Scope

Version 1 focuses on governance fundamentals.

Included:

- Capability inventory
- Identity inventory
- Policy management
- Policy evaluation engine
- Audit logging
- Enforcement modes
- Dashboard
- REST API
- CLI

Not Included:

- Compliance frameworks
- AI model hosting
- Workflow orchestration
- Agent development
- Multi-tenancy
- Billing
- Marketplace
- SaaS deployment

---

# Design Goals

- Vendor neutral
- Framework agnostic
- API first
- Policy driven
- Observable by default
- Auditable by design
- Simple to deploy

---

# Success Criteria

Organizations using Seneschal should be able to answer:

- What AI systems exist?
- What capabilities do they possess?
- Which policies govern them?
- What decisions were made?
- Why were those decisions made?
- What changed over time?
