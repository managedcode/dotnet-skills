---
name: dotnet-stryker
description: "Use the open-source free `Stryker.NET` mutation testing tool for .NET. Use when a repo needs to measure whether tests actually catch faults, especially in critical libraries or domains."
compatibility: "Requires a .NET test project or solution; respects the repo's `AGENTS.md` commands first."
---

# Stryker.NET

## Trigger On

- the repo uses or wants `Stryker.NET`
- mutation testing is needed for high-risk code

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- every PR path by default in a large repo
- simple coverage collection

## Inputs

- the nearest `AGENTS.md`
- target projects and critical paths
- time budget for mutation runs

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Run mutation testing on critical projects, not blindly on the whole mono-repo.
2. Keep it out of the fastest PR path unless the repo explicitly accepts the runtime cost.
3. Stabilize tests first; mutation testing amplifies flaky or slow suites.

## Bootstrap When Missing

If `Stryker.NET` is not configured yet:

1. Detect current state:
   - `rg --files -g '.config/dotnet-tools.json'`
   - `dotnet tool list --local`
   - `dotnet tool list --global`
2. Prefer local tool installation for reproducible CI:
   - `dotnet new tool-manifest` (if missing)
   - `dotnet tool install dotnet-stryker`
3. Choose a focused target scope and mutation budget before enabling CI.
4. Add a dedicated mutation command in `AGENTS.md` and CI (not in the fastest PR path by default).
5. Run `dotnet stryker` on the target project and return `status: configured` or `status: improved`.
6. If mutation testing is explicitly out of scope, return `status: not_applicable`.


## Deliver

- explicit mutation-test scope
- reproducible Stryker commands

## Validate

- the selected scope is affordable in CI
- mutation score is interpreted with test quality, not as a vanity number

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

- `references/stryker.md`
- `references/commands.md`
- `references/patterns.md`

## Example Requests

- "Add Stryker for this library."
- "Use mutation testing on our critical domain layer."
