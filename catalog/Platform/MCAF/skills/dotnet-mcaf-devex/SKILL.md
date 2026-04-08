---
name: dotnet-mcaf-devex
description: "Apply MCAF developer-experience guidance for onboarding, F5 contract, cross-platform tasks, local inner loop, and reproducible setup. Use when the repo is hard to run, debug, test, or onboard into."
compatibility: "Requires repository access; may update docs, task runners, devcontainer guidance, or local setup conventions."
---

# MCAF: Developer Experience

## Trigger On

- the repo is hard to run, test, debug, or onboard into
- local setup differs too much across contributors
- the inner loop is slow or undocumented

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- production deployment or pipeline policy
- pure documentation cleanup with no developer workflow impact

## Inputs

- the current local setup and first-run path
- actual build, run, debug, and test commands
- pain points in onboarding or the inner loop

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Find the slowest or most fragile part of the inner loop:
   - clone and setup
   - build
   - run and debug
   - test
2. Standardize tasks before optimizing them.
3. Prefer one documented way to run the full solution locally.
4. Pull only the references that match the local-dev problem you are fixing.

## Deliver

- lower-friction local workflow
- better onboarding
- reproducible build, run, test, and debug paths

## Validate

- a newcomer can follow the docs without hidden setup knowledge
- the inner loop is explicit and reproducible
- cross-platform or containerized guidance is used only where it helps
- local development uses real services, containers, or sandbox environments instead of fakes or stubs

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

- read `references/developer-experience.md` first
- open `references/onboarding-guide-template.md` only when relevant

## Example Requests

- "Make this repo easier to onboard into."
- "Document a sane local run and debug loop."
- "Fix the dev setup drift across machines."
