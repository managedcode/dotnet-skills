# Feature: FeatureName

> TEMPLATE ONLY — remove this note and replace all placeholder text before saving as a real feature doc under `docs/Features/`.

Links:  
Architecture: `docs/Architecture.md`  
Modules:  
ADRs: `docs/ADR/...`  

---

## Implementation plan (step-by-step)

> TEMPLATE ONLY — replace these checkboxes with real implementation steps for this feature and keep them updated while implementing.

- [ ] Analyze current behaviour (facts)
- [ ] Finalize spec (rules/flows/diagram/verification)
- [ ] Implement the feature (smallest safe increments)
- [ ] Add/update automated tests for each scenario (happy + negative + edge)
- [ ] Run verification commands (build/test/format/analyze/coverage) and record results
- [ ] Update docs (Feature/ADRs/Architecture overview) and close the checklist

---

## Purpose

Short description of the business problem and value.

---

## Stakeholders (who needs this to be clear)

| Role | What they need from this spec |
| --- | --- |
| Product / Owner | Scope, acceptance criteria, user-visible behaviour |
| Engineering | Modules, contracts, data, error handling, edge cases |
| DevOps / SRE | Config, rollout plan, monitoring/alerts, rollback |
| QA | Executable test flows (positive/negative/edge) + environment |

---

## Scope

### In scope

- Item

### Out of scope

- Item

---

## Business Rules

- Rule 1
- Rule 2
- Rule 3

---

## User Flows

### Primary flows

1. Flow name  
   - Actor: User / Service  
   - Trigger:  
   - Steps:  
   - Result:  

### Edge cases

- Edge case → Expected behaviour

---

## System Behaviour

- Entry points: API endpoints / UI / events / scheduled jobs  
- Reads from: DB / service / cache  
- Writes to: DB / service / queue  
- Side effects / emitted events:  
- Idempotency: Yes/No + conditions  
- Error handling: rules + user-facing messages  
- Security / permissions: AuthZ rules  
- Feature flags / toggles: names + defaults  
- Performance / SLAs:  
- Observability: logs/metrics/traces that must exist  

---

## Diagrams

At least one Mermaid diagram is mandatory in every real feature doc.

> TEMPLATE ONLY — Mermaid often breaks with fancy syntax. Keep it simple and make sure it renders in the repo.

```mermaid
```

---

## Verification

This section is mandatory: describe how to test (scenarios + commands).

### Test environment

- Environment / stack (local compose / staging / cloud env):  
- Data and reset strategy (seed data, fixtures, migration steps):  
- External dependencies (real / sandbox / test environment required):  

### Testing methodology

- Main flows that MUST be proven end-to-end:  
- Positive flows that MUST pass:  
- Negative flows that MUST fail safely and predictably:  
- Edge / boundary / unexpected flows that MUST be covered:  
- Test realism requirements (real dependencies, contracts, environments):  
- Coverage baseline requirement (must stay at least at the pre-change level or improve):  
- Pass criteria for considering the task done (all relevant tests green, new tests added, verification complete):  

### Test commands

- build: (paste from `AGENTS.md`)
- test: (paste from `AGENTS.md`)
- format: (paste from `AGENTS.md`)
- coverage: (paste from `AGENTS.md`; delete if none)

### Test flows

**Positive scenarios**

| ID | Description | Level (Unit / Int / API / UI) | Expected result | Data / Notes |
| --- | --- | --- | --- | --- |
| POS-001 | Happy path | Integration | Outcome observed via public interface | Data / fixtures |

**Negative scenarios**

| ID | Description | Level (Unit / Int / API / UI) | Expected result | Data / Notes |
| --- | --- | --- | --- | --- |
| NEG-001 | Validation failure | API | Error response / code | Invalid input example |

**Edge cases**

| ID | Description | Level (Unit / Int / API / UI) | Expected result | Data / Notes |
| --- | --- | --- | --- | --- |
| EDGE-001 | Boundary condition | Integration | Expected behaviour at boundary | Data / timing notes |

### Test mapping

- Integration tests:  
- API tests:  
- UI / E2E tests:  
- Unit tests:  
- Static analysis:  
- Coverage comparison against baseline:  

### Non-functional checks

Include this section only if it applies to this feature; otherwise remove it.

- Performance / load (tool, threshold, command):  
- Security / privacy (threats to verify):  
- Observability (log/metric assertions):  

---

## Definition of Done

- Behaviour matches rules and flows in this document.  
- Diagram section contains at least one Mermaid diagram that renders in the repo.  
- All test flows above are covered by automated tests (Integration / API / UI as applicable).  
- Testing methodology is written down and matches the implemented tests.  
- New or updated automated tests were added for the changed behaviour.  
- Positive, negative, and edge flows are all covered where applicable.  
- Static analysis passes with no new unresolved issues.  
- Test and build commands listed above run clean in local and CI environments, and all relevant tests are green.  
- Coverage is at least at the pre-change baseline or better.  
- Documentation updated: this feature doc, related ADRs, Testing / API / Architecture docs, `AGENTS.md` if rules or patterns changed.  
- Feature flags / migrations rolled out or cleaned up.

---

## References

- ADRs: `docs/ADR/...`  
- API: `docs/API/...`  
- Architecture: `docs/Architecture.md`  
- Testing: `docs/Testing/...`  
- Code: modules / namespaces  
