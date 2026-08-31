---
name: mcaf
description: "Apply minimal MCAF repository governance when the user explicitly asks to adopt MCAF or change MCAF-specific rules. USE FOR: creating or tightening root and local AGENTS.md policy; placing durable engineering context; defining the repository's own verification contract. DO NOT USE FOR: ordinary .NET implementation, documentation edits, UI/UX, testing, CI, Git, NFR, developer-experience, or ML work unless the request is specifically about MCAF governance. INVOKES: inspect repository policy and documentation, make the smallest durable governance change, then validate the affected repository surfaces."
---

# MCAF Governance

Keep MCAF as one opt-in governance layer. Do not create parallel `mcaf-*` implementation or process skills.

## Workflow

1. Confirm that the request is explicitly about adopting MCAF or changing MCAF governance.
2. Read the root `AGENTS.md` and every affected local `AGENTS.md` completely before editing any of them.
3. Keep only durable repository rules that future contributors and agents must know.
4. Put each rule next to the area that owns it. Add a local `AGENTS.md` only when a subtree genuinely differs.
5. Record exact build, test, format, analysis, and operational commands when the repository depends on them.
6. Route implementation work to the normal catalog skill closest to the actual technology or behavior.
7. Validate the changed policy, documentation links, and repository commands.

## MCAF 1.3.68 Policy Contract

When a repository declares that it follows MCAF 1.3, preserve these upstream mandatory policies rather than treating them as optional templates:

- `MCAF-ARCH-001`: keep the complete solution in one repository and use one canonical vertical-slice identity across applicable code, tests, infrastructure, and feature documentation.
- `MCAF-GOV-001`: merge incoming MCAF guidance into existing root and local `AGENTS.md` files. Never overwrite, truncate, summarize away, weaken, or silently omit an existing repository rule.
- `MCAF-REQ-001`: for non-trivial features, keep stable requirements and acceptance criteria, map them to automated tests or explicit evidence exceptions, and link any required ADR implementation contract.
- `MCAF-AI-001`: use a capable planning/integration owner for non-trivial work and keep delegated scopes bounded, independently verified, and joined before completion when the execution environment supports delegation.

Do not inject these policies into a repository that has not chosen MCAF. When updating an existing MCAF repository, preserve any stricter local rule and report a genuine conflict instead of weakening it.

## Minimal Rules

- Keep one root `AGENTS.md`.
- In a multi-project MCAF solution, keep a local `AGENTS.md` in each project or module root with its entry points, boundaries, commands, applicable skills, and local risks.
- Keep one source of truth for each durable fact.
- Describe the current repository, not an intended future state.
- Make acceptance and verification expectations concrete and runnable.
- Add feature specifications, ADRs, review plans, or quality constraints only when the task actually needs those artifacts.
- Turn repeated team pain into a small durable rule; do not encode one-off preferences.

## Boundaries

- Do not use MCAF as a router for normal .NET work; use `dotnet` or the narrow framework skill.
- Do not impose generic agile ceremonies, branch naming, UI/UX choices, onboarding templates, NFR catalogs, or ML process.
- Do not duplicate testing, CI, documentation, security, observability, or architecture skills under an MCAF namespace.
- Do not invent planning artifacts beyond the repository's adopted MCAF version. Current MCAF 1.3 repositories require brainstorm, acceptance, and plan artifacts for non-trivial work, so preserve that contract when it is already adopted.
- Do not add process artifacts that are larger than the decision or behavior they clarify.

## Deliver

- the smallest repository-native governance change that resolves the explicit MCAF request
- direct routing to implementation-focused skills for everything outside governance

## Validate

- every new rule is durable, scoped, and owned by a repository surface
- no rule duplicates a normal implementation skill
- referenced commands and paths exist
- an MCAF 1.3 repository preserves `MCAF-ARCH-001`, `MCAF-GOV-001`, `MCAF-REQ-001`, and `MCAF-AI-001` or records an explicit versioned migration state
- `scripts/verify-mandatory-policies.sh` passes when the consuming repository has adopted the upstream verifier
- removed or renamed MCAF surfaces leave no dangling catalog, bundle, watch, or documentation references

## Reference

Read [references/adoption.md](references/adoption.md) only when bootstrapping MCAF in a repository.
