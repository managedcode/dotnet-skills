---
name: dotnet-resharper-clt
description: "Use the free official JetBrains ReSharper Command Line Tools for .NET repositories. Use when a repo wants powerful `jb inspectcode` inspections, `jb cleanupcode` cleanup profiles, solution-level `.DotSettings` enforcement, or a stronger CLI quality gate for C# than the default SDK analyzers alone."
compatibility: "Requires a .NET solution or project; works best when the repo keeps shared ReSharper settings in solution `.DotSettings` files and records exact commands in `AGENTS.md`."
---

# ReSharper Command Line Tools

## Trigger On

- the repo uses or wants ReSharper Command Line Tools
- the team wants `jb inspectcode` or `jb cleanupcode`
- the user asks for stronger C# inspections, cleanup profiles, or ReSharper-based CI gates

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- replacing tests with inspection output
- ad-hoc formatting-only work when the repo intentionally standardizes on another formatter
- repos that do not want JetBrains settings or CLT-based gates in their workflow

## Inputs

- the nearest `AGENTS.md`
- the target `.sln`, `.csproj`, or bounded file set
- repo-root `.editorconfig`
- solution shared settings such as `YourSolution.sln.DotSettings`

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Prefer solution-level runs when possible so ReSharper can resolve references and apply full inspections.
2. Build the solution before `jb cleanupcode` when working at solution scope.
3. Use `jb inspectcode` first to surface issues before editing anything broad.
4. Treat surfaced issues as mandatory fixes when this gate is enabled for the repo; do not just dump a report and stop.
5. Use `jb cleanupcode` with an explicit cleanup profile:
   - `Built-in: Full Cleanup`
   - `Built-in: Reformat Code`
   - `Built-in: Reformat & Apply Syntax Style`
   - or a checked-in custom profile
6. Keep durable ReSharper settings in the team-shared solution layer and commit the solution `.DotSettings` file when policy changes.
7. Re-run `jb inspectcode` after cleanup or fixes, then run the repo's normal quality pass and tests.

## Bootstrap When Missing

If ReSharper Command Line Tools are not available yet:

1. Detect current state:
   - `rg --files -g '.config/dotnet-tools.json' -g '*.sln.DotSettings'`
   - `dotnet tool list --local`
   - `dotnet tool list --global`
   - `command -v jb`
2. Choose the install path deliberately:
   - preferred repo-local install for reproducible CI:
     - `dotnet new tool-manifest` (if missing)
     - `dotnet tool install JetBrains.ReSharper.GlobalTools`
   - global fallback:
     - `dotnet tool install --global JetBrains.ReSharper.GlobalTools`
3. Verify the installed commands resolve correctly:
   - `jb inspectcode --help`
   - `jb cleanupcode --help`
4. Record exact commands in `AGENTS.md`, for example:
   - `dotnet build MySolution.sln -c Release`
   - `jb inspectcode MySolution.sln -o=artifacts/inspectcode.sarif`
   - `jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup"`
5. If the repo needs stable settings, save them into the solution team-shared layer and commit `YourSolution.sln.DotSettings`.
6. Run `jb inspectcode` once, fix or triage the surfaced issues, rerun it, and return `status: configured` or `status: improved`.
7. If the repo intentionally excludes ReSharper CLT from its toolchain, return `status: not_applicable`.

## Deliver

- explicit `jb inspectcode` and `jb cleanupcode` commands
- durable ReSharper settings in shared solution config
- a quality gate that surfaces issues which are then fixed, not ignored

## Validate

- the target solution or project builds before solution-wide cleanup
- `jb inspectcode` output is reviewed and acted on
- cleanup profiles and shared settings are explicit
- tests and the wider quality pass still run after ReSharper-driven fixes

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

- `references/resharper-clt.md`
- `references/commands.md`
- `references/config.md`

## Example Requests

- "Add ReSharper CLI inspections to this .NET repo."
- "Run InspectCode and fix what it finds."
- "Set up CleanupCode with a shared profile."
- "Use JetBrains ReSharper command line tools in CI."
