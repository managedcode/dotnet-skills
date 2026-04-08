---
name: dotnet-mcaf-ui-ux
description: "Apply MCAF UI/UX guidance for design systems, accessibility, front-end technology selection, and design-to-development collaboration. Use when bootstrapping a UI project, choosing front-end stack, or tightening design and accessibility practices."
compatibility: "Requires repository access when UI docs, component guidance, or design system rules live in the repo."
---

# MCAF: UI/UX

## Trigger On

- bootstrapping a UI project
- choosing front-end stack or design-system direction
- tightening accessibility or design-to-development collaboration

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- pure backend or infrastructure work
- vague visual taste debates with no delivery consequence

## Inputs

- user outcomes, device targets, and accessibility needs
- design-system constraints and design handoff inputs
- current front-end stack and delivery constraints

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Start from user outcomes and accessibility needs, not framework preference.
2. Tie design choices to implementation and delivery constraints.
3. Pull only the references that match the active UI decision.

## Deliver

- better UI/UX delivery guidance
- clearer front-end technology and accessibility decisions
- design-system-aware development notes

## Validate

- accessibility is treated as a first-class requirement
- technology choices serve the product and team constraints
- design handoff guidance is actionable for engineers

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

- read `references/ui-ux.md` first
- open `references/recommended-technologies.md` when the active question is front-end stack or tech selection

## Example Requests

- "Choose the right frontend direction for this product."
- "Tighten accessibility and design handoff rules."
- "Document UI engineering guidance for this repo."
