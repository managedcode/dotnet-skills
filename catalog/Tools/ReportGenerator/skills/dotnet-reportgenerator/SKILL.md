---
name: dotnet-reportgenerator
description: "Use the open-source free `ReportGenerator` tool for turning .NET coverage outputs into HTML, Markdown, Cobertura, badges, and merged reports. Use when raw coverage files are not readable enough for CI or human review."
compatibility: "Requires coverage artifacts such as Cobertura, OpenCover, or lcov; respects the repo's `AGENTS.md` commands first."
---

# ReportGenerator for .NET

## Trigger On

- the repo uses or wants `ReportGenerator`
- CI needs human-readable coverage reports
- multiple coverage files must be merged

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- raw coverage collection with no reporting need

## Inputs

- the nearest `AGENTS.md`
- existing coverage artifacts
- desired output formats

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Keep collection and rendering separate: Coverlet collects, ReportGenerator renders.
2. Prefer the local or manifest-based .NET tool for reproducible CI runs.
3. Choose output formats deliberately:
   - `HtmlSummary`
   - `Cobertura`
   - `MarkdownSummaryGithub`
   - badges
4. Merge multiple reports only when the repo really needs a consolidated view.

## Bootstrap When Missing

If `ReportGenerator` is not configured yet:

1. Detect current state:
   - `rg --files -g '.config/dotnet-tools.json'`
   - `dotnet tool list --local`
   - `dotnet tool list --global`
   - `command -v reportgenerator`
2. Prefer local tool installation for reproducible CI:
   - `dotnet new tool-manifest` (if missing)
   - `dotnet tool install dotnet-reportgenerator-globaltool`
3. Add one explicit render command to `AGENTS.md` and CI, for example:
   - `dotnet tool run reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"artifacts/coverage" -reporttypes:"HtmlSummary;Cobertura"`
4. Run the report command once and return `status: configured` or `status: improved`.
5. If raw coverage outputs are already sufficient and no rendered artifacts are needed, return `status: not_applicable`.

## Deliver

- readable coverage artifacts for humans and CI systems
- explicit report-generation commands

## Validate

- report inputs match the generated coverage format
- generated reports land in a stable artifact path

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

- `references/reportgenerator.md`
- `references/commands.md`
- `references/formats.md`

## Example Requests

- "Render coverage as HTML in CI."
- "Merge multiple Coverlet reports."
