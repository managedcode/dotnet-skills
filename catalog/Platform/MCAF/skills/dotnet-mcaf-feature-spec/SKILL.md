---
name: dotnet-mcaf-feature-spec
description: "Apply MCAF feature-spec guidance to create or update a feature spec under `docs/Features/` with business rules, user flows, system behaviour, verification, and Definition of Done. Use when the user asks for a feature spec, executable requirements, acceptance criteria, behaviour documentation, or a pre-implementation plan for non-trivial behaviour changes."
compatibility: "Requires repository write access; produces Markdown docs with Mermaid diagrams and executable verification steps."
---

# MCAF: Feature Spec

## Trigger On

- add or change non-trivial behaviour
- behaviour is under-specified and engineers are guessing
- tests need a stable behavioural source of truth

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- architecture decisions that need alternatives and trade-offs
- tiny typo or cosmetic-only changes with no behavioural impact

## Inputs

- `docs/Architecture.md`
- the nearest `AGENTS.md`
- current user flows, business rules, and acceptance expectations

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Define scope first: in scope, out of scope, boundaries touched.
2. If the feature doc is missing, scaffold from `references/feature-template.md`.
3. Keep the spec executable:
   - numbered rules
   - main flow
   - edge and failure flows
   - system behaviour
   - verification steps
   - Definition of Done
4. Make the spec concrete enough that tests can be written without guessing.
5. If the feature creates a new dependency, boundary, or major policy shift, update an ADR too.

## Deliver

- `docs/Features/feature-name.md`
- a feature spec that engineers and agents can implement directly

## Validate

- rules are testable, not aspirational
- edge cases are captured where they matter
- verification steps match the intended behaviour
- the doc can drive implementation without hidden tribal knowledge

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

- use `references/feature-template.md` only for scaffolding

## Example Requests

- "Write a feature spec for the new checkout retry flow."
- "Document the behaviour before coding this API change."
- "Turn this loose requirement into an executable feature doc."
