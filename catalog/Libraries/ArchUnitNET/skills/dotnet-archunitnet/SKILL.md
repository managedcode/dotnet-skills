---
name: dotnet-archunitnet
description: "Use the open-source free `ArchUnitNET` library for architecture rules in .NET tests. Use when a repo needs richer architecture assertions than lightweight fluent rule libraries usually provide."
compatibility: "Requires a .NET test project; supports dedicated integrations for xUnit, xUnit v3, MSTest, TUnit, and others where available."
---

# ArchUnitNET for .NET

## Trigger On

- the repo uses or wants `ArchUnitNET`
- architecture testing needs richer modeling than simple dependency checks

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- the lightest possible architecture rule checks

## Inputs

- the nearest `AGENTS.md`
- target assemblies
- architecture boundaries and naming conventions

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Load the architecture once per test assembly where possible.
2. Encode a small number of durable, high-value architecture rules first.
3. Use the test-framework-specific integration package that matches the repo.

## Bootstrap When Missing

If `ArchUnitNET` is not configured yet:

1. Detect existing setup:
   - `rg -n "TngTech\\.ArchUnitNET" -g '*.csproj' .`
2. Add packages to the architecture test project:
   - `dotnet add TEST_PROJECT.csproj package TngTech.ArchUnitNET`
   - add one framework bridge package: `TngTech.ArchUnitNET.xUnit`, `TngTech.ArchUnitNET.xUnitV3`, `TngTech.ArchUnitNET.MSTestV2`, or `TngTech.ArchUnitNET.TUnit`
3. Add at least one durable boundary rule test.
4. Wire architecture tests into the standard `test` command in `AGENTS.md` and CI.
5. Run `dotnet test TEST_PROJECT.csproj` and return `status: configured` or `status: improved`.
6. If `NetArchTest` already covers the same boundary policy and no gap exists, return `status: not_applicable`.


## Deliver

- architecture tests with richer domain and type modeling
- architecture-rule commands wired into repo test flow and CI expectations

## Validate

- architecture load cost is reasonable for the suite
- rules are stable and tied to real boundaries

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

- `references/archunitnet.md`
- `references/patterns.md`
- `references/examples.md`

## Example Requests

- "Use ArchUnitNET for layered architecture tests."
- "Set up ArchUnitNET with xUnit or MSTest."
