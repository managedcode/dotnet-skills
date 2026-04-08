---
name: dotnet-csharpier
description: "Use the open-source free `CSharpier` formatter for C# and XML. Use when a .NET repo intentionally wants one opinionated formatter instead of a highly configurable `dotnet format`-driven style model."
compatibility: "Requires a .NET SDK-based repository; respects the repo's `AGENTS.md` commands first."
---

# CSharpier for .NET

## Trigger On

- the repo uses or wants `CSharpier`
- the team prefers an opinionated formatter over many configurable style knobs

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- repos that already standardized on `dotnet format` as the only formatter

## Inputs

- the nearest `AGENTS.md`
- current formatting ownership model
- any `.csharpierignore` or `.editorconfig`

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Decide whether CSharpier is the primary formatter or only complements other tools.
2. Use `check` mode in CI.
3. Keep ignore files and config explicit in repo.
4. Do not let `CSharpier` and `dotnet format` both own the same formatting space without documentation.

## Bootstrap When Missing

If `CSharpier` is not configured yet:

1. Detect current state:
   - `rg --files -g '.config/dotnet-tools.json' -g '.csharpierignore'`
   - `dotnet tool list --local`
   - `dotnet tool list --global`
2. Prefer local tool installation for reproducible CI:
   - `dotnet new tool-manifest` (if missing)
   - `dotnet tool install csharpier`
3. Add `.csharpierignore` when needed and define ownership vs `dotnet format` in `AGENTS.md`.
4. Add `dotnet csharpier check .` to CI.
5. Run `dotnet csharpier check .` and return `status: configured` or `status: improved`.
6. If the repo intentionally uses only `dotnet format`, return `status: not_applicable` unless migration is requested.


## Deliver

- explicit CSharpier ownership and commands
- CI-safe formatter checks

## Validate

- formatter ownership is not ambiguous
- the repo is comfortable with opinionated formatting decisions

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

- `references/commands.md`
- `references/config.md`
- `references/csharpier.md`

## Example Requests

- "Set up CSharpier for this repo."
- "Compare CSharpier and dotnet format."
