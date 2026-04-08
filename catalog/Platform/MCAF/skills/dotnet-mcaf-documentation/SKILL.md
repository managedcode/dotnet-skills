---
name: dotnet-mcaf-documentation
description: "Apply MCAF documentation guidance for docs structure, navigation, source-of-truth placement, and writing quality. Use when a repo’s docs are missing, stale, duplicated, or hard to navigate, or when adding new durable engineering guidance."
compatibility: "Requires repository write access; updates docs and documentation structure."
---

# MCAF: Documentation

## Trigger On

- docs are missing, stale, duplicated, or hard to navigate
- a repo needs a cleaner source-of-truth structure
- durable engineering guidance needs to be added without bloating pages

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- writing a specific feature spec or ADR when that skill already fits
- dumping large template content into public pages

## Inputs

- current docs structure and entry pages
- the code, policy, or workflow that the docs should reflect
- duplicate or conflicting sources of truth

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Decide the canonical location for each fact before writing.
2. Prefer navigational docs that link to detail instead of copying detail.
3. Keep bootstrap pages small; move workflow scaffolds into skills.
4. Update stale docs in the same change as the code or policy they describe.

## Deliver

- cleaner docs structure
- better source-of-truth placement
- docs that reflect the code and workflow that actually exist

## Validate

- each durable fact has one clear home
- entry pages route the reader correctly
- pages do not bloat with template or reference dumps
- docs match the real repo, not the intended future repo

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

- read `references/documentation.md` for structure and documentation-quality guidance

## Example Requests

- "Clean up this docs mess."
- "Move process clutter out of public pages."
- "Make the repo docs navigable for an agent."
