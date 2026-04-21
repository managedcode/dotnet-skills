# Review Plan Format

Use two separate sections.

## 1. Human Review Flow

List the natural operational flow in the order a reviewer should follow it.

For each step include:

- what the user or system is trying to do
- what code layers are involved
- what invariants or side effects the reviewer should verify
- what existing architecture pattern, analogous entity, or known system rule should be compared here

## 2. Priority Files

List the files or modules a human should inspect first.

For each entry include:

- path
- why it is high risk
- what kinds of mistakes are likely in generated code there
- what existing entity, flow, or architecture rule the reviewer should mentally compare it against

## Optional Saved Artifact

When saving a plan, use:

- the exact docs path requested by the user when one is provided
- `docs/AREA/HUMAN_REVIEW_PLAN.md` when the reviewed area has its own docs folder
- otherwise the nearest reasonable `docs/` location for that bounded context
