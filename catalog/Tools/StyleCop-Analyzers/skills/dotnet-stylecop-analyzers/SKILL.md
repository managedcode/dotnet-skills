---
name: dotnet-stylecop-analyzers
description: "Use the open-source free `StyleCop.Analyzers` package for naming, layout, documentation, and style rules in .NET projects. Use when a repo wants stricter style conventions than the SDK analyzers alone provide."
compatibility: "Requires a .NET SDK-based repository; respects the repo's `AGENTS.md` commands first."
---

# StyleCop Analyzers

## Trigger On

- the repo wants `StyleCop.Analyzers`
- naming, layout, or documentation style needs stronger enforcement
- the team needs `stylecop.json` guidance

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- repos that intentionally rely only on SDK analyzers
- repos where `StyleCop` overlaps too heavily with an existing style package and no consolidation is planned

## Inputs

- the nearest `AGENTS.md`
- current `.editorconfig`
- any existing `stylecop.json`

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Add `StyleCop.Analyzers` only if the repo wants its opinionated style rules.
2. Keep severity in the root `.editorconfig`.
3. Use `stylecop.json` only for StyleCop-specific behavioral options.
4. Prefer one checked-in `stylecop.json` per repo unless a project genuinely needs its own behavior.
5. Avoid rule duplication with SDK analyzers or other analyzer packs when possible.

## Bootstrap When Missing

If `StyleCop.Analyzers` is not configured yet:

1. Detect current state:
   - `rg -n "StyleCop\\.Analyzers|stylecop\\.json" -g '*.csproj' -g 'stylecop.json' .`
2. Add package to the intended scope:
   - `dotnet add PROJECT.csproj package StyleCop.Analyzers`
3. Keep severity in root `.editorconfig` and use `stylecop.json` only for StyleCop-specific behavior.
4. Prevent overlap with existing analyzer packs by defining ownership.
5. Run `dotnet build SOLUTION_OR_PROJECT` and return `status: configured` or `status: improved`.
6. If the repo intentionally uses SDK analyzers only, return `status: not_applicable`.


## Deliver

- explicit StyleCop package setup
- repo-owned StyleCop rule configuration
- clear split between root `.editorconfig` and `stylecop.json`

## Validate

- StyleCop severity is versioned in repo config
- `stylecop.json` is used only where it adds value

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

- `references/stylecop-analyzers.md`
- `references/rules.md`
- `references/config.md`

## Example Requests

- "Add `StyleCop.Analyzers` to this solution."
- "Configure StyleCop without losing `.editorconfig` ownership."
