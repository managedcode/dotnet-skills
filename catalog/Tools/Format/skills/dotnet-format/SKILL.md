---
name: dotnet-format
description: "Use the free first-party `dotnet format` CLI for .NET formatting and analyzer fixes. Use when a .NET repo needs formatting commands, `--verify-no-changes` CI checks, or `.editorconfig`-driven code style enforcement."
compatibility: "Requires a .NET SDK-based repository; respects the repo's `AGENTS.md` commands first."
---

# dotnet format

## Trigger On

- the repo uses `dotnet format`
- you need a CI-safe formatting check for .NET
- the repo wants `.editorconfig`-driven style enforcement

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- repositories that intentionally use `CSharpier` as the only formatter
- analyzer strategy with no formatting command change

## Inputs

- the nearest `AGENTS.md`
- the solution or project path
- the current `.editorconfig`

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Prefer the SDK-provided `dotnet format` command instead of inventing custom format scripts.
2. Start with verify mode in CI: `dotnet format TARGET --verify-no-changes`.
3. Use narrower subcommands only when the repo needs them:
   - `whitespace`
   - `style`
   - `analyzers`
4. Keep `.editorconfig` as the source of truth for style preferences.
5. If the repo also uses `CSharpier`, document which tool owns which file types or rules.

## Bootstrap When Missing

If `dotnet format` is requested but not available yet:

1. Detect current state:
   - `dotnet --info`
   - `dotnet format --version`
2. Treat `dotnet format` as SDK-provided, not as a separate repo-local tool by default.
3. If the command is missing, install or upgrade to a supported .NET SDK, then recheck `dotnet format --version`.
4. Add explicit local and CI commands to `AGENTS.md`, usually:
   - `dotnet format TARGET --verify-no-changes`
5. Run the chosen command once and return `status: configured` or `status: improved`.
6. If the repo intentionally uses only `CSharpier` for formatting ownership, return `status: not_applicable`.

## Deliver

- explicit `dotnet format` commands for local and CI runs
- formatting that follows `.editorconfig`

## Validate

- formatting is reproducible on CI
- no overlapping formatter ownership is left ambiguous

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

- `references/dotnet-format.md`
- `references/commands.md`
- `references/config.md`

## Example Requests

- "Add `dotnet format` to this repo."
- "Make formatting fail CI if files drift."
- "Explain when to use `dotnet format` versus `CSharpier`."
