---
name: codeql
description: "Use the open-source CodeQL ecosystem for .NET security analysis. Use when a repo needs CodeQL query packs, CLI-based analysis on open source codebases, or GitHub Action setup with explicit licensing caveats for private repositories."
compatibility: "Requires a GitHub-based or CLI-based CodeQL workflow; respects the repo's `AGENTS.md` commands first."
---

# CodeQL for .NET

## Trigger On

- the repo uses or wants CodeQL for .NET security analysis
- GitHub code scanning is part of the CI plan

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- teams that need a tool with no private-repo licensing caveat

## Inputs

- the nearest `AGENTS.md`
- hosting model: open-source repo, private repo, or manual CLI workflow
- current GitHub Actions workflow

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Treat CodeQL as a security-analysis tool, not as a style checker.
2. Make the licensing and hosting model explicit before proposing it as the default gate.
3. Prefer manual build mode for compiled .NET projects when precision matters.

## Bootstrap When Missing

If `CodeQL` is not configured yet:

1. Detect current state:
   - `rg -n "codeql-action|security-events|CodeQL" .github/workflows`
   - `command -v codeql`
2. Prefer CI-first setup for repository scanning using `github/codeql-action/init` and `github/codeql-action/analyze`.
3. Configure explicit .NET build mode in workflow (`manual` when precision matters).
4. Add local CLI usage only when the task requires local query work.
5. Run the workflow or local analyze path and return `status: configured` or `status: improved`.
6. If licensing or hosting constraints reject CodeQL for this repo, return `status: not_applicable` with caveat documented.


## Deliver

- explicit CodeQL setup or an explicit rejection with caveat documented
- reproducible CI or local commands for running CodeQL in this repo

## Validate

- the chosen CodeQL path is allowed for the repo type
- build mode is documented and reproducible

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

- `references/codeql.md`
- `references/queries.md`
- `references/workflow.md`

## Example Requests

- "Set up CodeQL for this public .NET repo."
- "Explain the CodeQL caveat for private repos."
