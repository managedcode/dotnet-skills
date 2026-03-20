# Current MCAF Skill Map

This map is sourced from the current local `managedcode/MCAF` catalog under `skills/`. Use it when a `.NET` repository wants MCAF but the task is specific enough that "use MCAF" is too vague to be useful.

## Governance And Delivery Flow

| MCAF skill | Use it for |
|---|---|
| `mcaf-solution-governance` | root and local `AGENTS.md`, rule precedence, topology, maintainability-limit placement, and solution-wide agent policy |
| `mcaf-agile-delivery` | backlog quality, planning flow, ceremonies, and turning delivery feedback into durable process changes |
| `mcaf-source-control` | branch naming, merge strategy, commit hygiene, release-policy guardrails, and secrets-in-git discipline |
| `mcaf-human-review-planning` | large AI-generated changes that need a practical human review sequence instead of a flat file-by-file read |

## Docs And Architecture

| MCAF skill | Use it for |
|---|---|
| `mcaf-architecture-overview` | creating or updating `docs/Architecture.md` as the global system map |
| `mcaf-feature-spec` | feature behavior specs under `docs/Features/` |
| `mcaf-adr-writing` | ADRs under `docs/ADR/` for technical decisions and trade-offs |
| `mcaf-documentation` | durable engineering docs structure, navigation, source-of-truth placement, and writing quality |
| `mcaf-nfr` | explicit non-functional requirements such as reliability, accessibility, maintainability, scalability, and compliance |

## Quality, Testing, And Review

| MCAF skill | Use it for |
|---|---|
| `mcaf-testing` | repository-aligned automated tests, verification flows, and test strategy updates |
| `mcaf-code-review` | PR scope, review checklists, reviewer expectations, and merge hygiene |
| `mcaf-solid-maintainability` | SOLID, SRP, cohesion, splitting large files or classes, and maintainability-limit enforcement |
| `mcaf-security-baseline` | secure defaults, secrets handling, review checkpoints, and baseline security guardrails |
| `mcaf-observability` | logs, metrics, traces, diagnostics, alerts, and runtime visibility policy |

## Tooling, Ops, And Product Surfaces

| MCAF skill | Use it for |
|---|---|
| `mcaf-ci-cd` | CI/CD pipelines, quality gates, release flow, deployment stages, and rollback policy |
| `mcaf-devex` | onboarding, local inner loop, reproducible setup, and developer workflow quality |
| `mcaf-ui-ux` | design-system, accessibility, front-end technology selection, and design-to-dev collaboration |
| `mcaf-ml-ai-delivery` | ML or AI product delivery, experimentation, responsible-AI workflow, and model/inference delivery planning |

## Practical .NET Routing

Start with `dotnet-mcaf` when the ask is "adopt MCAF" or "make this repo follow MCAF".

Then route:

- repo bootstrap and root/local `AGENTS.md` work -> `mcaf-solution-governance`
- architecture and docs bootstrap -> `mcaf-architecture-overview`, `mcaf-documentation`
- feature or ADR authoring -> `mcaf-feature-spec`, `mcaf-adr-writing`
- testing and review process -> `mcaf-testing`, `mcaf-code-review`
- release and pipeline policy -> `mcaf-ci-cd`, `mcaf-source-control`
- maintainability, observability, security, or NFR policy -> the corresponding narrow MCAF skill

After governance routing is clear, switch to the matching `dotnet-*` skill for real framework or code changes.
