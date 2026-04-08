---
name: dotnet-mcaf-source-control
description: "Apply MCAF source-control guidance for repository structure, branch naming, merge strategy, commit hygiene, and secrets-in-git discipline. Use when bootstrapping a repo, tightening PR flow, or documenting branch and release policy."
compatibility: "Requires repository access; may update contribution docs, AGENTS rules, or repository policy files."
---

# MCAF: Source Control

## Trigger On

- bootstrapping source-control policy
- tightening branch, merge, or PR rules
- documenting commit or release hygiene
- dealing with secrets-in-git or repository structure issues

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- CI/CD workflow design with no source-control policy change
- one-off git commands that do not alter repo policy

## Inputs

- current branching and merge flow
- release strategy and versioning expectations
- secret-handling and repository-structure constraints

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Agree on merge and release strategy before scaling implementation.
2. Keep branch and PR rules explicit in-repo.
3. Treat secrets in git history as a critical incident, not cleanup noise.
4. Use concrete policy language, not hand-waving.

## Deliver

- clear branch and merge strategy
- updated contribution or governance docs
- safer repository hygiene around commits, PRs, and secrets

## Validate

- naming and merge rules are explicit
- release/versioning implications are documented where needed
- secret hygiene is treated as policy, not tribal knowledge

## Ralph Loop

Use the Ralph Loop for every task, including docs, architecture, testing, and tooling work.

1. Brainstorm first (mandatory):
   - analyze current state
   - define the problem, target outcome, constraints, and risks
   - generate options and think through trade-offs before committing
   - capture the recommended direction and open questions
2. Plan second (mandatory):
   - write a detailed execution plan from the chosen direction
   - list final validation skills to run at the end, with order and reason
3. Execute one planned step and produce a concrete delta.
4. Review the result and capture findings with actionable next fixes.
5. Apply fixes in small batches and rerun the relevant checks or review steps.
6. Update the plan after each iteration.
7. Repeat until outcomes are acceptable or only explicit exceptions remain.
8. If a dependency is missing, bootstrap it or return `status: not_applicable` with explicit reason and fallback path.

### Required Result Format

- `status`: `complete` | `clean` | `improved` | `configured` | `not_applicable` | `blocked`
- `plan`: concise plan and current iteration step
- `actions_taken`: concrete changes made
- `validation_skills`: final skills run, or skipped with reasons
- `verification`: commands, checks, or review evidence summary
- `remaining`: top unresolved items or `none`

For setup-only requests with no execution, return `status: configured` and exact next commands.

## Load References

- read `references/source-control.md` first
- open `references/naming-branches.md` only when the task is specifically about branch naming

## Example Requests

- "Define branch naming and merge rules for this repo."
- "Document how releases and component versions should work."
- "Tighten our source-control policy after a secrets leak."
