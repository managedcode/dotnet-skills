---
name: dotnet-mcaf-agile-delivery
description: "Apply MCAF agile-delivery guidance for backlog quality, roles, ceremonies, and engineering feedback. Use when defining how the team plans, tracks work, and turns feedback into durable improvements."
compatibility: "Requires repository access only when the repo stores delivery docs or governance guidance."
---

# MCAF: Agile Delivery

## Trigger On

- the team needs backlog, ceremony, role, or feedback-loop rules
- delivery process is vague, too heavy, or living only in chat
- recurring team pain needs to become durable repo guidance

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- repo governance that belongs in `AGENTS.md`
- feature planning for one specific feature doc

## Inputs

- the current delivery pain point
- backlog, role, ceremony, and feedback mechanisms that already exist
- where the team stores durable agreements, if anywhere

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Keep delivery artefacts concrete:
   - backlog
   - roles
   - ceremonies
   - engineering feedback
2. Prefer lightweight agreements over process theatre.
3. When a pain point repeats, turn it into a rule, doc, or skill update.
4. Pull only the references that match the current process problem.

## Deliver

- concrete delivery guidance
- durable team agreements
- feedback loops that update docs, skills, and rules

## Validate

- the process guidance fixes a real delivery problem
- roles and rituals are explicit enough to use
- recurring pain is converted into a durable artifact, not more chat

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

- read `references/agile-delivery.md` first
- open `references/roles.md` only for a narrower topic

## Example Requests

- "Define a lighter delivery model for this team."
- "Turn repeated feedback pain into repo guidance."
- "Fix our backlog and ceremony chaos."
