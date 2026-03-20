# Risk Signals

Generated code deserves earlier human review when it sits at one of these seams.

## High-Risk Areas

- orchestration files that connect many use cases
- state transitions and lifecycle completion logic
- validation, permissions, and conditional branching
- persistence, migrations, and data mapping
- external integrations such as email, storage, queues, or APIs
- cross-aggregate invariants
- retry, idempotency, and background job flows
- places where generated code should mirror an existing entity or workflow but may have drifted
- seams where the new feature reuses assumptions from older domains with slightly different rules

## Lower-Value Early Review Targets

These often matter less in the first review pass:

- simple DTOs and view models
- repetitive test data builders
- mechanical mapping or formatting helpers
- passive constants with no branching logic

## Practical Rule

Start where a wrong decision would corrupt state, bypass a rule, or trigger the wrong side effect.
Also start where the generated code is most likely to disagree with the architecture knowledge already in the human reviewer's head.
