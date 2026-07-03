# Platform API

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Overview

The Seneschal Platform API provides a consistent interface for managing governance objects and submitting Requests for evaluation.

The API is divided into two categories:

- Management APIs
- Runtime APIs

---

# Management APIs

Management APIs configure and maintain the governance model.

These operations are administrative in nature.

## Identity

- Create Identity
- Update Identity
- Delete Identity
- Get Identity
- List Identities

---

## Capability

- Create Capability
- Update Capability
- Delete Capability
- Get Capability
- List Capabilities

---

## Policy

- Create Policy
- Update Policy
- Delete Policy
- Get Policy
- List Policies

---

# Runtime APIs

Runtime APIs participate in request evaluation.

## Submit Request

The primary runtime operation.

Every authorization request enters the platform through Submit Request.

Input:

- Identity
- Capability
- Resource (optional)
- Context (optional)

Output:

- Decision
- Explanation
- Audit Reference

---

## Get Decision

Retrieve a previously generated Decision.

---

## Query Audit Events

Retrieve immutable Audit Events.

Filters may include:

- Identity
- Capability
- Policy
- Decision
- Time Range
- Event Type

---

# Design Principles

The Platform API should:

- Be resource-oriented.
- Be deterministic.
- Be explainable.
- Support synchronous and asynchronous execution.
- Remain transport independent.

---

# API Categories

Management Plane

- Identity
- Capability
- Policy

Runtime Plane

- Request
- Decision

Observability Plane

- Audit
- Reporting

---

# Version 1 Scope

Version 1 exposes APIs for:

- Identity Management
- Capability Management
- Policy Management
- Request Submission
- Decision Retrieval
- Audit Query

Future versions may introduce additional APIs without changing the core runtime model.
