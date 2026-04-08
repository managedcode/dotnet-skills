---
name: dotnet-mcaf-nfr
description: "Apply MCAF non-functional-requirements guidance to capture or refine explicit quality attributes such as accessibility, reliability, scalability, maintainability, performance, and compliance. Use when a feature or architecture change needs explicit quality attributes and trade-offs."
compatibility: "Requires repository access when NFRs are documented in feature docs, ADRs, or architecture docs."
---

# MCAF: Non-Functional Requirements

## Trigger On

- a feature or architecture change needs explicit quality attributes
- a team is using vague words like "fast", "reliable", or "secure" without measurable meaning
- docs, ADRs, and tests are out of sync on quality expectations

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- generic architecture or feature writing with no quality-attribute decision
- loading all NFR references at once

## Inputs

- the changed feature, boundary, or rollout path
- the quality attributes that materially affect it
- current docs, ADRs, tests, and ops expectations

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Decide which quality attributes materially affect the change.
2. Turn vague goals into explicit requirements, constraints, or testable expectations.
3. Link NFRs to feature docs, ADRs, and verification when they affect design or rollout.
4. Use only the specific reference files that match the active quality attribute.

## Deliver

- explicit NFRs for the changed area
- docs or ADRs that describe measurable quality attributes
- better alignment between architecture, testing, and operations

## Validate

- each chosen NFR is measurable or at least falsifiable
- the selected attributes are the ones that actually drive design trade-offs
- verification and operational expectations are linked where needed

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

- pick only the exact file for the active NFR: accessibility, reliability, performance, scalability, compliance, maintainability, and so on

## Example Requests

- "Make the non-functional requirements explicit for this feature."
- "Turn vague reliability goals into real constraints."
- "Document performance and compliance expectations for this service."
