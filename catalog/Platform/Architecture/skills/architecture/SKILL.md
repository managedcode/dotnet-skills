---
name: architecture
description: "Design or review .NET solution architecture across modular monoliths, clean architecture, vertical slices, microservices, DDD, CQRS, and cloud-native boundaries without over-engineering."
compatibility: "Best when project structure, service boundaries, or long-term maintainability are in scope."
---

# .NET Architecture

## Trigger On

- choosing architecture for a new or evolving .NET system
- reviewing layer boundaries, domain boundaries, or service decomposition
- deciding whether clean architecture, vertical slices, CQRS, or microservices are justified

## Workflow

1. Start from business capability boundaries and change frequency, not from a preferred diagram style.
2. Use simple modular monolith patterns by default, and move to microservices only when team autonomy, scale, or deployment boundaries justify the added operational cost.
3. Apply DDD and CQRS where business rules are genuinely complex; avoid forcing aggregates and command pipelines into CRUD-heavy code with no payoff.
4. Keep dependencies flowing inward when using clean architecture, but avoid creating extra projects that add ceremony without ownership clarity.
5. Make integration boundaries explicit: contracts, storage ownership, messaging, consistency model, and observability expectations.
6. Use `aspire` when local orchestration, service discovery, and developer observability are part of the architecture story.

## Deliver

- an architecture direction that matches system complexity
- clear project and dependency boundaries
- migration notes or tradeoffs when changing an existing structure

## Validate

- the proposed structure reduces rather than increases accidental complexity
- data ownership and integration paths are explicit
- the architecture is testable and operable, not just diagram-friendly

## References

- [references/patterns.md](references/patterns.md) - detailed implementations of Clean Architecture, Vertical Slices, DDD, CQRS, Modular Monolith, and Microservices with C# 12+ examples
- [references/anti-patterns.md](references/anti-patterns.md) - common architectural mistakes including over-abstraction, anemic domain models, premature microservices, and cargo cult patterns
