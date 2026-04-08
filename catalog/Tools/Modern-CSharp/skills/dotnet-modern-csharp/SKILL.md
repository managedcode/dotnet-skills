---
name: dotnet-modern-csharp
description: "Write modern, version-aware C# for .NET repositories. Use when choosing language features across C# versions, especially C# 13 and C# 14, while staying compatible with the repo's target framework and `LangVersion`."
compatibility: "Requires a C# or .NET repository; respects explicit `LangVersion` and target framework settings."
---

# Modern C# for .NET

## Trigger On

- the repo wants more modern idiomatic C# code
- a change depends on language-version compatibility
- the team is upgrading or reviewing C# feature usage across versions
- you need to know whether a C# 13 or C# 14 feature is safe to use

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- non-C# .NET languages such as F# or VB
- analyzer-only or formatter-only setup with no language feature choice

## Inputs

- target `TFM` or `TFMs`
- explicit `LangVersion`, if any
- current SDK version
- team style rules in `.editorconfig` and `AGENTS.md`

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Detect the real language ceiling from the repo's target framework and explicit `LangVersion`.
2. Prefer stable features that the current repo actually supports.
3. Use modern syntax when it reduces ceremony, improves correctness, or makes invariants clearer.
4. Do not mass-rewrite a codebase into newer syntax unless the repo wants that churn.
5. Treat preview features as opt-in only. Never assume preview because the current machine has a newer SDK.
6. Pay special attention to C# 13 and C# 14:
   - C# 13 is the stable language for `.NET 9`
   - C# 14 is the stable language for `.NET 10`
7. When feature selection changes architecture, style rules, or generated-code patterns, coordinate with:
   - `dotnet`
   - `dotnet-analyzer-config`
   - `dotnet-architecture`
8. After feature-driven refactors, run the repo's .NET quality pass through `dotnet`.

## Bootstrap When Missing

If the requested C# feature depends on SDK or language support the repo does not have yet:

1. Detect current state:
   - `dotnet --list-sdks`
   - `rg -n "TargetFramework|LangVersion|TargetFrameworks" -g '*.csproj' -g 'Directory.Build.*' .`
2. Confirm whether the repo wants to stay on the current stable language level or intentionally upgrade.
3. If the feature requires a newer supported SDK or target framework, upgrade the repo toolchain deliberately instead of relying on the local machine by accident.
4. If the repo needs explicit `LangVersion`, record it in project or shared MSBuild config.
5. Run `dotnet build SOLUTION_OR_PROJECT` after the feature or toolchain change and return `status: configured` or `status: improved`.
6. If the repo intentionally stays below the required language level, return `status: not_applicable`.

## Deliver

- modern C# code that fits the repo's real language version
- fewer obsolete patterns when a newer stable feature is clearer
- no accidental preview or unsupported-language drift

## Validate

- the chosen syntax is supported by the repo's `TFM` and `LangVersion`
- the feature improves clarity, correctness, or maintainability
- preview-only features are used only when the repo explicitly opted in
- style and analyzer rules still agree with the new syntax

## Ralph Loop

Use the Ralph Loop for every task, including docs, architecture, testing, and tooling work.

1. Plan first (mandatory):
   - analyze current state
   - define target outcome, constraints, and risks
   - write a detailed execution plan
   - list final validation skills to run at the end, with order and reason
2. Execute one planned step and produce a concrete delta.
3. Review the result and capture findings with actionable next fixes.
4. Apply fixes in small batches and rerun the relevant checks or review steps.
5. Update the plan after each iteration.
6. Repeat until outcomes are acceptable or only explicit exceptions remain.
7. If a dependency is missing, bootstrap it or return `status: not_applicable` with explicit reason and fallback path.

### Required Result Format

- `status`: `complete` | `clean` | `improved` | `configured` | `not_applicable` | `blocked`
- `plan`: concise plan and current iteration step
- `actions_taken`: concrete changes made
- `validation_skills`: final skills run, or skipped with reasons
- `verification`: commands, checks, or review evidence summary
- `remaining`: top unresolved items or `none`

For setup-only requests with no execution, return `status: configured` and exact next commands.

## Load References

- `references/patterns.md`
- `references/migration.md`
- `references/csharp-modern-features.md`

## Example Requests

- "Make this C# code more modern."
- "Which features can we use on .NET 9?"
- "Review this repo for C# 13 or C# 14 opportunities."
