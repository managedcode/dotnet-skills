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
| `mcaf-agile-delivery` | `mcaf-agile-delivery` |
| `mcaf-devex` | `mcaf-devex` |
| `mcaf-documentation` | `mcaf-documentation` |
| `mcaf-feature-spec` | `mcaf-feature-spec` |
| `mcaf-human-review-planning` | `mcaf-human-review-planning` |
| `mcaf-ml-ai-delivery` | `mcaf-ml-ai-delivery` |
| `mcaf-nfr` | `mcaf-nfr` |
| `mcaf-source-control` | `mcaf-source-control` |
| `mcaf-ui-ux` | `mcaf-ui-ux` |

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

Start with `mcaf` when the ask is "adopt MCAF" or "make this repo follow MCAF".

Then route:

- repo bootstrap and root/local `AGENTS.md` work -> `mcaf`
- delivery workflow -> `mcaf-agile-delivery`
- developer onboarding and local loop -> `mcaf-devex`
- docs bootstrap -> `mcaf-documentation`
- feature behaviour docs -> `mcaf-feature-spec`
- large generated-drop review sequencing -> `mcaf-human-review-planning`
- ML or AI delivery policy -> `mcaf-ml-ai-delivery`
- source-control policy -> `mcaf-source-control`
- explicit quality attributes -> `mcaf-nfr`
- UI/UX and accessibility direction -> `mcaf-ui-ux`
- overlapping architecture, testing, CI, security, observability, and maintainability areas -> keep the boundary guidance below and route into the existing implementation-focused skills as needed

After governance routing is clear, switch to the matching implementation-focused skill for real framework or code changes.

## Overlap Versus Net-New Surface

Some MCAF skills overlap conceptually with areas that already exist in `dotnet-skills`, but they operate at a different layer.

### Conceptual overlap with existing implementation-focused skills

| MCAF skill | Closest current `dotnet-skills` surface | Boundary |
|---|---|---|
| `mcaf-architecture-overview` | `architecture` | MCAF defines repo architecture docs and decision shape; `architecture` covers actual .NET solution structure and technical design |
| `mcaf-code-review` | `code-review` | MCAF defines review process and merge hygiene; `code-review` reviews .NET code changes for bugs and regressions |
| `mcaf-testing` | `quality-ci`, test-framework skills such as `xunit`, `nunit`, `mstest`, `tunit` | MCAF defines verification policy; implementation-focused skills define concrete .NET test and CI implementation |
| `mcaf-ci-cd` | `quality-ci`, `project-setup` | MCAF defines release-flow and governance policy; implementation-focused skills wire concrete .NET pipeline commands and quality gates |
| `mcaf-solution-governance` | `project-setup` | MCAF defines root/local `AGENTS.md`, rule precedence, and repo policy; `project-setup` defines solution and project structure |
| `mcaf-solid-maintainability` | `complexity`, analyzer skills | MCAF defines maintainability expectations; implementation-focused skills implement concrete metrics, analyzers, and refactoring mechanics |
| `mcaf-security-baseline` | `codeql`, analyzer and platform security skills | MCAF defines baseline security process; implementation-focused skills cover concrete .NET tooling and framework-specific security work |
| `mcaf-observability` | platform/runtime skills such as `aspnet-core`, `worker-services`, `aspire`, `orleans` | MCAF defines telemetry policy; implementation-focused skills implement logging, tracing, metrics, and diagnostics per framework |

### Surfaces that are effectively new relative to this catalog

These MCAF skills did not have a close one-to-one equivalent in the original `dotnet-skills` catalog and are now mirrored locally as:

- `mcaf-agile-delivery`
- `mcaf-devex`
- `mcaf-documentation`
- `mcaf-feature-spec`
- `mcaf-human-review-planning`
- `mcaf-ml-ai-delivery`
- `mcaf-nfr`
- `mcaf-source-control`
- `mcaf-ui-ux`

Treat those as genuinely additive. They extend repo workflow and delivery governance rather than duplicating .NET implementation guidance.
