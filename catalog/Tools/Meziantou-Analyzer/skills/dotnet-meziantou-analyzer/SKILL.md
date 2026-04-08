---
name: dotnet-meziantou-analyzer
description: "Use the open-source free `Meziantou.Analyzer` package for design, usage, security, performance, and style rules in .NET. Use when a repo wants broader analyzer coverage with a single NuGet package."
compatibility: "Requires a .NET SDK-based repository; respects the repo's `AGENTS.md` commands first."
---

# Meziantou.Analyzer

## Trigger On

- the repo uses or wants `Meziantou.Analyzer`
- the team wants one analyzer pack that covers design, usage, security, performance, and style

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- repos that already enforce an overlapping analyzer baseline and do not want extra diagnostics
- formatting-only work

## Inputs

- the nearest `AGENTS.md`
- current analyzer packages
- `.editorconfig`

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Add `Meziantou.Analyzer` when the repo wants broader rules than the SDK baseline.
2. Keep rule severity in the repo-root `.editorconfig`.
3. Review overlaps with SDK analyzers and Roslynator before mass-enabling everything as errors.

## Bootstrap When Missing

If `Meziantou.Analyzer` is not configured yet:

1. Detect current state:
   - `rg -n "Meziantou\\.Analyzer" -g '*.csproj' .`
2. Add the package to the intended scope (project-level or shared props strategy):
   - `dotnet add PROJECT.csproj package Meziantou.Analyzer`
3. Set severity in root `.editorconfig` for the enabled `MAxxxx` rules.
4. Keep overlap with SDK analyzers and Roslynator explicit to avoid duplicate noise.
5. Run `dotnet build SOLUTION_OR_PROJECT` and return `status: configured` or `status: improved`.
6. If the repo intentionally keeps a smaller analyzer surface, return `status: not_applicable`.


## Deliver

- explicit Meziantou package setup
- repo-owned severity and warning policy

## Validate

- the added rules are understood by the team
- CI runs stay actionable instead of noisy

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

- `references/meziantou-analyzer.md`
- `references/rules.md`
- `references/config.md`

## Example Requests

- "Add Meziantou analyzers to the repo."
- "Use Meziantou for extra quality and security checks."
