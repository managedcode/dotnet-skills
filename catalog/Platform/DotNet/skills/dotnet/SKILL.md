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
   - Web: `aspnet-core`, `minimal-apis`, `web-api`, `blazor`, `signalr`, `grpc`
   - Cloud and hosting: `aspire`, `azure-functions`, `worker-services`
   - Desktop and client: `maui`, `wpf`, `winforms`, `winui`
   - Data and distributed: `entity-framework-core`, `entity-framework6`, `orleans`
   - AI and agentic: `semantic-kernel`, `microsoft-extensions-ai`, `microsoft-agent-framework`, `mlnet`, `mixed-reality`
   - Legacy: `legacy-aspnet`, `wcf`, `workflow-foundation`
3. Route cross-cutting work to the companion skill instead of keeping it inside generic `.NET` advice:
   - project bootstrap or repo shape: `project-setup`, `architecture`
   - frontend asset analysis in mixed `.NET` plus Node repos: `eslint`, `stylelint`, `htmlhint`, `webhint`, `biome`, `sonarjs`, `metalint`, `chous`
   - code review: `code-review`
   - language features: `modern-csharp`
   - testing: `tunit`, `xunit`, `mstest`
   - format, analyzers, coverage, and CI: `format`, `code-analysis`, `quality-ci`, `coverlet`, `reportgenerator`
   - maintainability and architecture rules: `complexity`, `netarchtest`, `archunitnet`
4. If more than one specialized skill applies, prefer the one closest to the user-visible behavior first, then pull in the quality or tooling skill second.
5. Do not stop at this skill once a narrower match exists. This skill should classify and hand off, not become a generic dumping ground.
6. After code changes, validate with the repository's actual build, test, and quality workflow instead of generic `.NET` commands.

## Routing Heuristics

- If the repo contains `Microsoft.NET.Sdk.Web`, start from a web skill, not generic `.NET`.
- If the repo contains Blazor, Razor Components, or `.razor` pages, prefer `blazor`.
- If the repo contains `package.json`, frontend lint configs, or browser-facing asset pipelines inside the `.NET` solution, prefer the dedicated frontend analysis skills instead of generic `.NET`.
- If the repo contains Orleans grains or silo hosting, prefer `orleans`.
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
