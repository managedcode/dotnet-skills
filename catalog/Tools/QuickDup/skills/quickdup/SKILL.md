---
name: quickdup
description: "Use the open-source free `QuickDup` clone detector for .NET repositories. Use when a repo needs duplicate C# code discovery, structural clone detection, DRY refactoring candidates, or repeatable duplication scans in local workflows and CI."
compatibility: "Requires a repository with C# source files; respects the repo's `AGENTS.md` commands first."
---

# QuickDup for .NET

## Trigger On

- the repo wants `QuickDup`
- the team needs repeatable duplicate-code scans for C#
- the user asks about DRY cleanup, copy-paste detection, clone detection, or duplicate logic removal in a .NET repo

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- formatting-only work
- repos that intentionally use a different clone detector and do not want overlap
- generated-code churn where duplication findings would mostly be noise

## Inputs

- the nearest `AGENTS.md`
- target solution, project, or source subtree
- current duplication hotspots and generated-code boundaries

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Choose the scan target deliberately:
   - whole solution for broad discovery
   - bounded folders for targeted cleanup
2. Scan C# with `.cs` only unless the repo explicitly wants Razor, generated XML, or other extensions too.
3. Exclude generated and transient paths before trusting the results:
   - `bin/`
   - `obj/`
   - `Migrations/` when scaffold churn dominates
   - `*.g.cs`
   - `*.generated.cs`
   - `*.Designer.cs`
4. Review the top patterns before refactoring anything.
5. Classify duplication before changing code:
   - real structural duplication worth extraction
   - domain-shape duplication that documents intent
   - generated or acceptable duplication that should be ignored
6. Prefer small extractions, named helpers, shared value objects, or focused abstractions over flag-heavy merge methods.
7. Re-run `QuickDup` after the refactor and then run the repo's normal quality pass.

## Bootstrap When Missing

If `QuickDup` is not available yet:

1. Detect current state:
   - `command -v quickdup`
   - `go version`
   - `rg --files -g '.quickdup/ignore.json' -g '.quickdup/results.json'`
2. Choose the install path deliberately:
   - preferred when Go is available: `go install github.com/asynkron/Asynkron.QuickDup/cmd/quickdup@latest`
   - official macOS/Linux fallback: `curl -sSL https://raw.githubusercontent.com/asynkron/Asynkron.QuickDup/main/install.sh | bash`
   - official Windows fallback: `iwr -useb https://raw.githubusercontent.com/asynkron/Asynkron.QuickDup/main/install.ps1 | iex`
3. Verify the installed CLI resolves correctly:
   - `quickdup -h`
4. Record exact duplication commands in `AGENTS.md`, for example:
   - `quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs"`
   - `quickdup -path src -ext .cs -top 20`
   - `quickdup -path . -ext .cs -select 0..5`
5. If the repo wants stable suppressions, create `.quickdup/ignore.json` and review each ignored pattern intentionally.
6. Run one bounded scan and return `status: configured` or `status: improved`.
7. If the repo already standardizes on another clone detector and does not want `QuickDup`, return `status: not_applicable`.

## Deliver

- repeatable duplicate-code detection for C#
- explicit exclude and suppression strategy
- concrete refactoring candidates instead of vague maintainability advice

## Validate

- the scan target and excludes match the repo's real source boundaries
- generated-code noise is filtered before acting on findings
- duplication cleanup preserves behavior and is backed by relevant tests
- `QuickDup` output is used as input to review, not as an automatic rewrite authority

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
- `references/examples.md`
- `references/quickdup.md`

## Example Requests

- "Find duplicate C# logic in this solution."
- "Add QuickDup to this .NET repo."
- "Review the top QuickDup patterns before we refactor."
- "Set up a repeatable duplicate-code scan for CI or local cleanup."
