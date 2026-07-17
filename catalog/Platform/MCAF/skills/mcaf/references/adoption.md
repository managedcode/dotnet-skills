# Minimal MCAF Adoption

Use this reference only when a repository explicitly chooses MCAF.

## Canonical Sources

- Concepts: https://mcaf.managed-code.com/
- Tutorial: https://mcaf.managed-code.com/tutorial
- Source: https://github.com/managedcode/MCAF

## Boundary

MCAF owns repository governance and durable context. It does not replace implementation-focused skills from this catalog.

- MCAF: `AGENTS.md`, rule ownership, durable context, repository verification contract.
- Catalog skills: .NET frameworks, testing, CI, architecture, security, observability, UI, data, and tooling.

## Bootstrap

1. Add one root `AGENTS.md`.
2. Document the real repository topology and exact commands contributors must run.
3. Add local `AGENTS.md` files only where local behavior genuinely differs.
4. Link durable architecture, feature, decision, testing, development, and operations docs that already have a clear owner.
5. Install the narrow implementation skills required by the actual stack.

## Optional Rules

Add these only when the repository needs them:

- Capture non-trivial behavior in a feature document when tests and implementation need a shared contract.
- Record an ADR when a decision has meaningful alternatives or long-lived trade-offs.
- Express relevant quality constraints as measurable or falsifiable outcomes; do not load a universal NFR checklist.
- Review large generated changes by user flow and risk boundary instead of raw file order.
- Document exact local setup and inner-loop commands when onboarding cannot be inferred from the repository.
- Convert recurring delivery problems into one durable rule instead of adding ceremonies by default.
- Define source-control policy only when the repository differs from the hosting platform's normal workflow.
- For ML systems, record the specific data, evaluation, and responsible-use constraints that affect the current feature.

## Validation

- rules describe current behavior
- commands run in the current checkout
- each durable fact has one owner
- optional artifacts exist only when they reduce real ambiguity
- implementation work routes to normal catalog skills
