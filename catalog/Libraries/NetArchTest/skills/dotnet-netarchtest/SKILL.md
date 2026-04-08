---
name: dotnet-netarchtest
description: "Use the open-source free `NetArchTest.Rules` library for architecture rules in .NET unit tests. Use when a repo wants lightweight, fluent architecture assertions for namespaces, dependencies, or layering."
compatibility: "Requires a .NET test project; works with any unit-test framework."
---

# NetArchTest

## Trigger On

- the repo uses or wants `NetArchTest.Rules`
- architecture rules should be enforced in automated tests

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- very rich architecture modeling that needs a heavier DSL

## Inputs

- the nearest `AGENTS.md`
- architecture boundaries to enforce
- target assemblies

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Encode only durable architecture rules:
   - forbidden dependencies
   - namespace layering
   - type shape conventions
2. Keep rules readable and close to the boundary they protect.
3. Fail tests on architecture drift, not on temporary style noise.

## Bootstrap When Missing

If `NetArchTest.Rules` is not configured yet:

1. Detect existing setup:
   - `rg -n "NetArchTest\\.Rules" -g '*.csproj' .`
2. Add the package to the architecture test project:
   - `dotnet add TEST_PROJECT.csproj package NetArchTest.Rules`
3. Add at least one executable boundary rule test.
4. Wire architecture tests into the standard `test` command in `AGENTS.md` and CI.
5. Run `dotnet test TEST_PROJECT.csproj` and return `status: configured` or `status: improved`.
6. If richer modeling is required and `ArchUnitNET` is chosen as the standard, return `status: not_applicable`.


## Deliver

- architecture tests that are understandable and stable
- boundary checks wired into the normal test path used by agents and CI

## Validate

- the rules map to real boundaries the team cares about
- failures point to actionable dependency drift

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
- `references/examples.md`
- `references/netarchtest.md`

## Example Requests

- "Add architecture tests with NetArchTest."
- "Block UI from referencing data directly."
