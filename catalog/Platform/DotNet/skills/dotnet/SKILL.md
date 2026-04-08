---
name: dotnet
description: "Primary router skill for broad .NET work. Classify the repo by app model and cross-cutting concern first, then switch to the narrowest matching .NET skill instead of staying at a generic layer."
compatibility: "Requires a .NET repository, solution, or project tree."
---

# .NET Router Skill

## Trigger On

- the user asks for general `.NET` help without naming a narrower framework or tool
- implementing, debugging, reviewing, or refactoring C# or `.NET` code in a repo with multiple app models or frameworks
- deciding which `.NET` skill should own a task before editing code
- tasks that combine platform work with testing, quality, architecture, setup, or migration decisions

## Workflow

1. Detect the real stack first:
   - target frameworks and SDK version
   - `LangVersion`
   - project SDKs and workload hints
   - hosting model and app entry points
   - test framework and runner
   - analyzers, formatters, coverage, and CI quality gates
2. Route to the narrowest platform skill as soon as the stack is known:
   - Web: `dotnet-aspnet-core`, `dotnet-minimal-apis`, `dotnet-web-api`, `dotnet-blazor`, `dotnet-signalr`, `dotnet-grpc`
   - Cloud and hosting: `dotnet-aspire`, `dotnet-azure-functions`, `dotnet-worker-services`
   - Desktop and client: `dotnet-maui`, `dotnet-wpf`, `dotnet-winforms`, `dotnet-winui`
   - Data and distributed: `dotnet-entity-framework-core`, `dotnet-entity-framework6`, `dotnet-orleans`
   - AI and agentic: `dotnet-semantic-kernel`, `dotnet-microsoft-extensions-ai`, `dotnet-microsoft-agent-framework`, `dotnet-mlnet`, `dotnet-mixed-reality`
   - Legacy: `dotnet-legacy-aspnet`, `dotnet-wcf`, `dotnet-workflow-foundation`
3. Route cross-cutting work to the companion skill instead of keeping it inside generic `.NET` advice:
   - project bootstrap or repo shape: `dotnet-project-setup`, `dotnet-architecture`
   - frontend asset analysis in mixed `.NET` plus Node repos: `dotnet-eslint`, `dotnet-stylelint`, `dotnet-htmlhint`, `dotnet-webhint`, `dotnet-biome`, `dotnet-sonarjs`, `dotnet-metalint`, `dotnet-chous`
   - code review: `dotnet-code-review`
   - language features: `dotnet-modern-csharp`
   - testing: `dotnet-tunit`, `dotnet-xunit`, `dotnet-mstest`
   - format, analyzers, coverage, and CI: `dotnet-format`, `dotnet-code-analysis`, `dotnet-quality-ci`, `dotnet-coverlet`, `dotnet-reportgenerator`
   - maintainability and architecture rules: `dotnet-complexity`, `dotnet-netarchtest`, `dotnet-archunitnet`
4. If more than one specialized skill applies, prefer the one closest to the user-visible behavior first, then pull in the quality or tooling skill second.
5. Do not stop at this skill once a narrower match exists. This skill should classify and hand off, not become a generic dumping ground.
6. After code changes, validate with the repository's actual build, test, and quality workflow instead of generic `.NET` commands.

## Routing Heuristics

- If the repo contains `Microsoft.NET.Sdk.Web`, start from a web skill, not generic `.NET`.
- If the repo contains Blazor, Razor Components, or `.razor` pages, prefer `dotnet-blazor`.
- If the repo contains `package.json`, frontend lint configs, or browser-facing asset pipelines inside the `.NET` solution, prefer the dedicated frontend analysis skills instead of generic `.NET`.
- If the repo contains Orleans grains or silo hosting, prefer `dotnet-orleans`.
- If the repo is mostly analyzers, CI, or coverage work, prefer the quality skill directly.
- If the user asks about “which skill should I use?”, answer with the narrowest matching skill and explain why in one short sentence.
- If no narrower skill matches, keep the work here and stay explicit about the missing specialization.

## Deliver

- the correct specialized skill choice for the task
- repo-compatible code or documentation changes that stay aligned with the detected stack
- validation evidence that matches the real project runner and quality toolchain

## Validate

- the chosen downstream skill actually exists in the catalog
- platform assumptions match project SDKs, packages, and workloads
- generic guidance has been replaced by framework-specific guidance whenever possible
- runner-specific commands are not mixed incorrectly
- language or runtime features are only used when the repo supports them

## Documentation

### References

- [`references/routing.md`](references/routing.md) - Decision tree for routing tasks to specialized .NET skills, including app model classification and cross-cutting concern handling.
- [`references/detection.md`](references/detection.md) - Project detection patterns for identifying SDK types, target frameworks, workloads, language versions, and app models.
