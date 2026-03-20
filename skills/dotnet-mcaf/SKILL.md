---
name: dotnet-mcaf
version: "1.2.0"
category: "Core"
description: "Adopt MCAF alongside the dotnet-skills catalog with the right AGENTS.md layout, repo-native docs, skill installation flow, verification rules, and non-trivial task workflow."
compatibility: "Best for repositories that want MCAF governance and also use dotnet-skills for actual .NET implementation work."
---

# MCAF Adoption

## Trigger On

- bootstrapping MCAF in a new or existing repository that also contains `.NET` work
- updating root or project-local `AGENTS.md` files to follow a durable repo workflow
- deciding which MCAF skills and `dotnet-*` skills a solution should install together
- organizing repo-native docs for architecture, features, ADRs, testing, development, and operations
- aligning AI-agent workflow with explicit build, test, format, analyze, and coverage commands

## Workflow

1. Treat the canonical bootstrap surface as URL-first:
   - tutorial: `https://mcaf.managed-code.com/tutorial`
   - concepts: `https://mcaf.managed-code.com/`
   - public MCAF skills: `https://mcaf.managed-code.com/skills`
2. Place root `AGENTS.md` at the repository or solution root. Add project-local `AGENTS.md` files when the `.NET` solution has multiple projects or bounded modules with stricter local rules.
3. Keep MCAF bootstrap small and repo-native:
   - durable instructions in `AGENTS.md`
   - durable engineering docs in the repository
   - workflow details in skills, references, and repo docs instead of chat memory
4. Treat MCAF as its own skill catalog, not one monolithic rule file. This catalog now mirrors the net-new MCAF governance surfaces as dedicated `dotnet-mcaf-*` skills while keeping clear boundaries against overlapping `dotnet-*` implementation skills.
5. Route to the narrowest local MCAF skill once the governance problem is clear:
   - delivery workflow and feedback loops: `dotnet-mcaf-agile-delivery`
   - developer onboarding and local inner loop: `dotnet-mcaf-devex`
   - durable docs structure and source-of-truth placement: `dotnet-mcaf-documentation`
   - executable feature behaviour docs: `dotnet-mcaf-feature-spec`
   - human review sequencing for large AI-generated drops: `dotnet-mcaf-human-review-planning`
   - ML/AI product delivery process: `dotnet-mcaf-ml-ai-delivery`
   - explicit quality attributes and trade-offs: `dotnet-mcaf-nfr`
   - branch, merge, and release hygiene: `dotnet-mcaf-source-control`
   - design-system, accessibility, and front-end direction: `dotnet-mcaf-ui-ux`
6. Expect partial conceptual overlap with existing `dotnet-*` skills. Keep MCAF for repo governance, documentation, delivery, and cross-cutting process policy. Keep `dotnet-*` skills for framework-specific implementation, .NET-specific quality tooling, and concrete code changes. Use the overlap map in `references/skill-map.md` before adding duplicate surfaces.
7. For `.NET` repositories, install the local MCAF governance mirrors from this catalog and install the narrow framework implementation skills from the same catalog. When the upstream MCAF catalog evolves faster than the local mirror, refer back to `https://mcaf.managed-code.com/` for the canonical source and update path.
8. Keep documentation explicit enough for direct implementation:
   - `docs/Architecture.md`
   - `docs/Features/`
   - `docs/ADR/`
   - `docs/Testing/`
   - `docs/Development/`
   - `docs/Operations/`
9. Encode the non-trivial task flow directly in `AGENTS.md`: root-level `<slug>.brainstorm.md`, then `<slug>.plan.md`, then implementation and validation.
10. Treat verification as part of done. The change is not complete until the full repo-defined quality pass is green, including tests, analyzers, formatters, coverage, and any architecture or security gates the repo configured.

## Architecture

```mermaid
flowchart LR
  A["Adopt MCAF in a repo with .NET work"] --> B["Root AGENTS.md"]
  B --> C{"Multi-project solution?"}
  C -->|Yes| D["Project-local AGENTS.md files"]
  C -->|No| E["Keep root policy only"]
  B --> F["Install local dotnet-mcaf-* governance skills"]
  B --> G["Install local dotnet-* implementation skills"]
  D --> H["Document local boundaries and commands"]
  E --> H
  F --> I["Repo-native docs and workflow scaffolds"]
  G --> J["Stack-specific .NET implementation guidance"]
  H --> K["Run repo-defined quality pass"]
  I --> K
  J --> K
```

## Deliver

- a repository-ready MCAF adoption shape for the solution
- clear root and local `AGENTS.md` responsibilities
- the right split between overlapping `mcaf-*` governance skills and `dotnet-*` implementation skills
- local installable MCAF governance skills for the net-new process areas that this catalog did not cover before
- explicit repo docs and verification expectations instead of chat-only instructions

## Validate

- root `AGENTS.md` exists at the repository or solution root
- project-local `AGENTS.md` files exist where the solution actually needs stricter local rules
- the repo documents exact `.NET` build, test, formatting, analyzer, and coverage commands
- durable docs exist for architecture and behavior, not only inline comments or chat context
- non-trivial work requires the brainstorm-to-plan flow before implementation
- the full relevant quality pass is part of done, not only a narrow happy-path test run

## References

- [references/adoption.md](references/adoption.md) - Canonical MCAF entry points, bootstrap rules for repos that also use dotnet-skills, and the local-mirror boundary between MCAF governance skills and `.NET` implementation skills
- [references/skill-map.md](references/skill-map.md) - Current MCAF catalog map, including the locally mirrored `dotnet-mcaf-*` skills and the overlap-vs-new split so teams can route precisely instead of treating MCAF as a single blob
