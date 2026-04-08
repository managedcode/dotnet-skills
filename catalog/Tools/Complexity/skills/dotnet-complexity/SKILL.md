---
name: dotnet-complexity
description: "Use free built-in .NET maintainability analyzers and code metrics configuration to find overly complex methods and coupled code. Use when a repo needs cyclomatic complexity checks, maintainability thresholds, or complexity-driven refactoring gates."
compatibility: "Requires a .NET SDK-based repository; respects the repo's `AGENTS.md` commands first."
---

# .NET Complexity Review

## Trigger On

- the team wants to find overly complex methods
- cyclomatic complexity thresholds are needed in CI
- maintainability metrics or coupling thresholds need to be configured

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- formatting-only work
- generic analyzer setup with no complexity policy change

## Inputs

- the nearest `AGENTS.md`
- current analyzer settings
- current maintainability limits

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Start with the built-in maintainability analyzers before reaching for non-standard tooling.
2. Use these rules deliberately:
   - `CA1502` for excessive cyclomatic complexity in methods
   - `CA1505` for low maintainability index
   - `CA1506` for excessive class coupling
   - `CA1501` when inheritance depth is also part of the design problem
3. Keep rule severity in the root `.editorconfig`.
4. Keep metric thresholds in a checked-in `CodeMetricsConfig.txt` added as `AdditionalFiles`.
5. Pair analyzer findings with MCAF maintainability limits in `AGENTS.md`.

## Bootstrap When Missing

If complexity thresholds are not configured yet:

1. Detect current state:
   - `rg -n "CA1501|CA1502|CA1505|CA1506|CodeMetricsConfig" -g '.editorconfig' -g '*.csproj' -g 'Directory.Build.*' .`
   - `rg --files -g 'CodeMetricsConfig.txt'`
2. Add severity entries for `CA1502`, `CA1505`, `CA1506`, and `CA1501` in root `.editorconfig`.
3. Add checked-in `CodeMetricsConfig.txt` and include it as `AdditionalFiles` in project props.
4. Keep maintainability limits aligned with `AGENTS.md`.
5. Run `dotnet build SOLUTION_OR_PROJECT` and return `status: configured` or `status: improved`.
6. If policy relies only on `AGENTS.md` limits with no analyzer gate by design, return `status: not_applicable`.


## Deliver

- explicit complexity and maintainability policy
- checked-in metric thresholds
- CI commands that surface complex methods early

## Validate

- method-complexity checks are enabled where the repo wants them
- thresholds are versioned in repo, not held in IDE memory
- complexity findings map to real refactoring decisions

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

- `references/complexity.md`
- `references/metrics.md`
- `references/config.md`

## Example Requests

- "Which analyzer finds complex methods in .NET?"
- "Add a complexity gate for our C# code."
- "Configure cyclomatic complexity thresholds."
