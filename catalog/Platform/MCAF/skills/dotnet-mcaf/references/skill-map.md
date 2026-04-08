# Current MCAF Skill Map

This map is sourced from the current local `managedcode/MCAF` catalog under `skills/`. Use it when a `.NET` repository wants MCAF but the task is specific enough that "use MCAF" is too vague to be useful.

## Governance And Delivery Flow

| MCAF skill | Use it for |
|---|---|
| `mcaf-solution-governance` | root and local `AGENTS.md`, rule precedence, topology, maintainability-limit placement, and solution-wide agent policy |
| `mcaf-agile-delivery` | backlog quality, planning flow, ceremonies, and turning delivery feedback into durable process changes |
| `mcaf-source-control` | branch naming, merge strategy, commit hygiene, release-policy guardrails, and secrets-in-git discipline |
| `mcaf-human-review-planning` | large AI-generated changes that need a practical human review sequence instead of a flat file-by-file read |

## Local Mirrors In This Catalog

The following net-new MCAF surfaces are now mirrored locally in `dotnet-skills`:

| Canonical MCAF skill | Local mirror in this catalog |
|---|---|
| `mcaf-agile-delivery` | `dotnet-mcaf-agile-delivery` |
| `mcaf-devex` | `dotnet-mcaf-devex` |
| `mcaf-documentation` | `dotnet-mcaf-documentation` |
| `mcaf-feature-spec` | `dotnet-mcaf-feature-spec` |
| `mcaf-human-review-planning` | `dotnet-mcaf-human-review-planning` |
| `mcaf-ml-ai-delivery` | `dotnet-mcaf-ml-ai-delivery` |
| `mcaf-nfr` | `dotnet-mcaf-nfr` |
| `mcaf-source-control` | `dotnet-mcaf-source-control` |
| `mcaf-ui-ux` | `dotnet-mcaf-ui-ux` |

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

- repo bootstrap and root/local `AGENTS.md` work -> `dotnet-mcaf`
- delivery workflow -> `dotnet-mcaf-agile-delivery`
- developer onboarding and local loop -> `dotnet-mcaf-devex`
- docs bootstrap -> `dotnet-mcaf-documentation`
- feature behaviour docs -> `dotnet-mcaf-feature-spec`
- large generated-drop review sequencing -> `dotnet-mcaf-human-review-planning`
- ML or AI delivery policy -> `dotnet-mcaf-ml-ai-delivery`
- source-control policy -> `dotnet-mcaf-source-control`
- explicit quality attributes -> `dotnet-mcaf-nfr`
- UI/UX and accessibility direction -> `dotnet-mcaf-ui-ux`
- overlapping architecture, testing, CI, security, observability, and maintainability areas -> keep the boundary guidance below and route into the existing `dotnet-*` implementation skills as needed

After governance routing is clear, switch to the matching `dotnet-*` skill for real framework or code changes.

## Overlap Versus Net-New Surface

Some MCAF skills overlap conceptually with areas that already exist in `dotnet-skills`, but they operate at a different layer.

### Conceptual overlap with existing `dotnet-*` skills

| MCAF skill | Closest current `dotnet-skills` surface | Boundary |
|---|---|---|
| `mcaf-architecture-overview` | `dotnet-architecture` | MCAF defines repo architecture docs and decision shape; `dotnet-architecture` covers actual .NET solution structure and technical design |
| `mcaf-code-review` | `dotnet-code-review` | MCAF defines review process and merge hygiene; `dotnet-code-review` reviews .NET code changes for bugs and regressions |
| `mcaf-testing` | `dotnet-quality-ci`, test-framework skills such as `dotnet-xunit`, `dotnet-nunit`, `dotnet-mstest`, `dotnet-tunit` | MCAF defines verification policy; `dotnet-*` skills define concrete .NET test and CI implementation |
| `mcaf-ci-cd` | `dotnet-quality-ci`, `dotnet-project-setup` | MCAF defines release-flow and governance policy; `dotnet-*` skills wire concrete .NET pipeline commands and quality gates |
| `mcaf-solution-governance` | `dotnet-project-setup` | MCAF defines root/local `AGENTS.md`, rule precedence, and repo policy; `dotnet-project-setup` defines solution and project structure |
| `mcaf-solid-maintainability` | `dotnet-complexity`, analyzer skills | MCAF defines maintainability expectations; `dotnet-*` skills implement concrete metrics, analyzers, and refactoring mechanics |
| `mcaf-security-baseline` | `dotnet-codeql`, analyzer and platform security skills | MCAF defines baseline security process; `dotnet-*` skills cover concrete .NET tooling and framework-specific security work |
| `mcaf-observability` | platform/runtime skills such as `dotnet-aspnet-core`, `dotnet-worker-services`, `dotnet-aspire`, `dotnet-orleans` | MCAF defines telemetry policy; `dotnet-*` skills implement logging, tracing, metrics, and diagnostics per framework |

### Surfaces that are effectively new relative to this catalog

These MCAF skills did not have a close one-to-one equivalent in the original `dotnet-skills` catalog and are now mirrored locally as:

- `dotnet-mcaf-agile-delivery`
- `dotnet-mcaf-devex`
- `dotnet-mcaf-documentation`
- `dotnet-mcaf-feature-spec`
- `dotnet-mcaf-human-review-planning`
- `dotnet-mcaf-ml-ai-delivery`
- `dotnet-mcaf-nfr`
- `dotnet-mcaf-source-control`
- `dotnet-mcaf-ui-ux`

Treat those as genuinely additive. They extend repo workflow and delivery governance rather than duplicating .NET implementation guidance.
