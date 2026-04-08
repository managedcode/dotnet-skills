---
name: dotnet-workflow-foundation
version: "1.0.0"
category: "Legacy"
description: "Maintain or assess Workflow Foundation-based solutions on .NET Framework, especially where long-lived process logic or legacy designer artifacts still matter."
compatibility: "Requires WF or a migration plan away from it."
---

# Windows Workflow Foundation

## Trigger On

- working on WF activities, workflows, or designer-backed process logic
- reviewing long-lived workflow state and persistence behavior
- assessing whether to keep, isolate, or replace Workflow Foundation

## Workflow

1. Treat WF as legacy infrastructure and start by understanding what workflow behavior is still business-critical before proposing replacement.
2. Separate workflow host concerns, activity logic, persistence, and integration points so risk is visible.
3. Avoid half-migrations that leave workflow state and business rules split across two orchestration systems without ownership.
4. If replacement is needed, define explicit equivalence for triggers, compensation, persistence, and audit expectations.
5. Stabilize current behavior with targeted tests or scenario captures before changing designer-driven artifacts.
6. Validate with representative long-running and failure scenarios, not just a single successful execution path.

## Deliver

- practical maintenance or migration guidance for WF
- clear boundaries around host, workflow, and persistence responsibilities
- risk-aware change plans for legacy process logic

## Validate

- business-critical workflow behavior is identified before change
- migration work preserves state and audit expectations
- designer artifacts are treated carefully

## References

- [Migration Guidance](references/migration.md) - decision framework for keeping, replacing, or isolating WF; migration targets and steps; common pitfalls
- [Maintenance Patterns](references/patterns.md) - host management, persistence, activity design, testing, and operational patterns for WF systems
