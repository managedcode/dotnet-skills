---
name: dotnet-mcaf-ml-ai-delivery
description: "Apply MCAF ML/AI delivery guidance for data exploration, feasibility, experimentation, testing, responsible AI, and operating ML systems. Use when the repo includes model training, inference, data science workflows, or ML-specific delivery planning."
compatibility: "Requires repository access when ML/AI docs, experiments, or delivery guidance live in the repo."
---

# MCAF: ML/AI Delivery

## Trigger On

- the repo contains model training, inference, experimentation, or data-science workflow
- ML work needs explicit process, testing, or responsible-AI guidance
- delivery discussion is mixing product, data, and model concerns

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- generic software delivery with no ML or data-science component
- loading all ML references when only one stage is active

## Inputs

- the current ML stage: framing, data exploration, experimentation, training, inference, or operations
- product assumptions, data assumptions, and model assumptions
- current verification and responsible-AI expectations

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Separate product assumptions, data assumptions, and model assumptions.
2. Keep experimentation traceable and testable.
3. Treat responsible AI, data quality, and ML-specific verification as first-class requirements.
4. Load only the references that match the current ML stage.

## Deliver

- clearer ML/AI delivery guidance
- better links between data, experimentation, verification, and responsible AI
- docs that match how the ML system is built and validated

## Validate

- the active ML stage is explicit
- experimentation and evaluation are traceable
- responsible-AI and data-quality requirements are not bolted on at the end

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

- read `references/ml-ai-projects.md` first
- open `references/data-exploration.md`, `references/feasibility-studies.md`, `references/ml-fundamentals-checklist.md`, `references/model-experimentation.md`, `references/testing-data-science-and-mlops-code.md`, `references/responsible-ai.md`, or `references/ml-model-checklist.md` only when that stage is active

## Example Requests

- "Define the delivery workflow for this ML feature."
- "We need responsible-AI and testing guidance for this model."
- "Separate product, data, and model decisions in our docs."
