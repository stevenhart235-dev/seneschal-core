# Enforcement Modes

**Status:** Draft

**Version:** 0.1

**Last Updated:** 2026-07-03

---

# Overview

Enforcement Modes describe how Seneschal responds when Policies are evaluated.

They allow organizations to adopt governance progressively.

---

# Modes

## Discover

Seneschal records known Identities, Capabilities, and Requests without enforcing authorization outcomes.

---

## LogOnly

Seneschal evaluates Policies and records the resulting Decision, but does not block execution.

---

## RequireApproval

Seneschal requires human approval before the requested Capability may proceed.

---

## Enforce

Seneschal actively allows or denies execution based on the final Decision.

---

# Version 1 Scope

Version 1 supports:

- LogOnly
- Enforce

Discover and RequireApproval may be represented in the model but do not require full workflow implementation in Version 1.

---

# Design Principle

Organizations should be able to move from visibility to enforcement without redesigning their systems.