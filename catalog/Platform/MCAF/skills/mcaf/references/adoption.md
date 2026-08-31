# MCAF 1.3 Adoption

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

1. Add one root `AGENTS.md` from the current upstream template, then replace every placeholder with repository-specific facts.
2. Document the complete solution boundary, canonical vertical-slice convention, and exact build, test, format, analysis, complexity, and coverage commands the repository actually uses.
3. In a multi-project solution, add a local `AGENTS.md` to each project or module root with its purpose, entry points, boundaries, commands, skills, and risks.
4. Add `docs/Architecture.md`, `docs/Features/`, `docs/ADR/`, `docs/Testing/`, `docs/Development/`, and `docs/Operations/` only as required by the adopted MCAF policy and real solution scope.
5. Install the narrow implementation skills required by the actual stack; MCAF governance does not replace them.
6. If the repository includes the upstream `scripts/verify-mandatory-policies.sh`, run it after adoption or policy updates.
7. For non-trivial work, keep the MCAF 1.3 brainstorm, acceptance, and implementation-plan artifacts and map acceptance criteria to automated tests or explicit evidence exceptions.

## Mandatory MCAF 1.3 Policies

- `MCAF-ARCH-001`: one solution repository and one canonical vertical-slice name across applicable backend, frontend, contracts, tests, infrastructure, and feature documentation.
- `MCAF-GOV-001`: read every existing root/local `AGENTS.md` completely and merge changes without replacing, weakening, or dropping existing rules.
- `MCAF-REQ-001`: non-trivial features have stable requirements and acceptance criteria, test/evidence mapping, and required ADR implementation contracts.
- `MCAF-AI-001`: non-trivial planning, decomposition, integration, and final review stay owned by a capable lead; delegated work is bounded and independently verified when delegation is available.

Adopt these only when the repository explicitly chooses MCAF, but once adopted do not downgrade them to optional guidance.

## Validation

- rules describe current behavior
- commands run in the current checkout
- each durable fact has one owner
- optional artifacts exist only when they reduce real ambiguity
- implementation work routes to normal catalog skills
- mandatory policy IDs are present and internally consistent
- updates preserve every pre-existing root and local rule unless the owner explicitly changes that exact rule
