---
name: xunit
description: "Write, run, or repair .NET tests that use xUnit. Use when a repo uses `xunit`, `xunit.v3`, `[Fact]`, `[Theory]`, or `xunit.runner.visualstudio`, and you need the right CLI, package, and runner guidance for xUnit on VSTest or Microsoft.Testing.Platform."
compatibility: "Requires a .NET solution or project with xUnit packages; respects the repo's `AGENTS.md` commands first."
---

# xUnit.net

## Trigger On

- the repo uses xUnit v2 or xUnit v3
- you need to add, run, debug, or repair xUnit tests
- the team is unsure whether a project is using VSTest or Microsoft.Testing.Platform

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- TUnit projects
- MSTest projects
- generic test strategy with no xUnit-specific mechanics

## Inputs

- the nearest `AGENTS.md`
- the test project file and package references
- the active runner model for the test project

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Detect the active xUnit model before changing commands:
   - `xunit` usually means v2
   - `xunit.v3` means v3
   - `xunit.runner.visualstudio` plus `Microsoft.NET.Test.Sdk` usually means VSTest compatibility is enabled
   - `TestingPlatformDotnetTestSupport` or `UseMicrosoftTestingPlatformRunner` means Microsoft.Testing.Platform is in play
2. Read the repo's real `test` command from `AGENTS.md`. If the repo has no explicit command yet, start with `dotnet test PROJECT_OR_SOLUTION`.
3. Keep the runner model consistent:
   - xUnit v2 usually runs through VSTest
   - xUnit v3 can run as a standalone executable with `dotnet run`
   - xUnit v3 can also integrate with Microsoft.Testing.Platform
   - do not mix VSTest-only switches into Microsoft.Testing.Platform runs
4. Run the narrowest useful scope first:
   - one project
   - one class
   - one trait
   - one method
5. Prefer `[Theory]` for stable data-driven coverage and `[Fact]` for single-path invariant checks.
6. Keep `xunit.analyzers` enabled when present. Fix analyzer findings instead of muting them casually.

## Bootstrap When Missing

If xUnit is requested but not configured:

1. Detect current framework first:
   - `rg -n "xunit(\\.v3)?|xunit\\.runner\\.visualstudio|TestingPlatformDotnetTestSupport|UseMicrosoftTestingPlatformRunner|TUnit|MSTest" -g '*.csproj' .`
2. If the repo currently uses `TUnit` or `MSTest`, do not auto-migrate. Return `status: not_applicable` unless migration is explicitly requested.
3. For explicit xUnit adoption, add packages to the target test project:
   - `dotnet add TEST_PROJECT.csproj package xunit.v3`
   - optional VSTest bridge: `dotnet add TEST_PROJECT.csproj package xunit.runner.visualstudio`
4. Add repo test commands and runner notes to `AGENTS.md`.
5. Run `dotnet test TEST_PROJECT.csproj` or repo-defined xUnit command and return `status: configured` or `status: improved`.


## Deliver

- xUnit tests that match the repo's active xUnit version and runner
- commands that work in local and CI runs
- focused verification before broader suite execution

## Validate

- the chosen CLI matches the active runner model
- test filters or focused runs are valid for that runner
- tests use deterministic inputs and assertions
- xUnit-specific analyzers remain active unless the repo documents an exception

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

- `references/xunit.md`
- `references/patterns.md`
- `references/anti-patterns.md`

## Example Requests

- "Run this xUnit suite correctly."
- "Fix our xUnit v3 test command."
- "Add an xUnit regression test and keep CI compatible."
