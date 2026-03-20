# MCAF Adoption Alongside dotnet-skills

Use this reference when a repository wants to adopt the Managed Code Coding AI Framework and also uses the dotnet-skills catalog for `.NET` implementation work instead of keeping AI workflow rules scattered across chat threads, tribal knowledge, or ad hoc prompts.

## Canonical Sources

- Concepts: `https://mcaf.managed-code.com/`
- Tutorial: `https://mcaf.managed-code.com/tutorial`
- Skills catalog: `https://mcaf.managed-code.com/skills`
- Source repository: `https://github.com/managedcode/MCAF`

The concepts page defines the framework. The tutorial is the canonical bootstrap flow.

## What MCAF Adds To A Repository

From the official concepts and tutorial pages, the core MCAF shape is:

1. durable engineering context stays in the repository
2. agents work from `AGENTS.md`, repo docs, and installed skills
3. verification is enforced through tests and analyzers
4. guidance stays small, explicit, versioned, and repo-native

For repositories that also contain `.NET` code, the official tutorial makes one boundary explicit:

- install MCAF governance skills from the MCAF catalog
- install implementation-focused `dotnet-*` skills from `https://skills.managed-code.com/`

That means MCAF is the framework layer, while this repository remains the `.NET` execution layer.

The local `dotnet-skills` catalog now mirrors the net-new MCAF governance surfaces as installable `dotnet-mcaf-*` skills. Use the local mirrors first for the transferred surfaces, and fall back to the upstream MCAF catalog when a governance skill has not been mirrored here yet.

## Bootstrap Rules That Matter Alongside dotnet-skills

Use the tutorial as the single canonical install surface.

Required bootstrap artifacts:

- root `AGENTS.md` at the repo or solution root
- optional `CLAUDE.md` wrapper when the repo uses Claude Code
- local `dotnet-mcaf*` governance skills under the native agent skill directory
- `dotnet-*` implementation skills from this catalog when the repo contains `.NET` code

For multi-project solutions:

- keep one root `AGENTS.md`
- add one local `AGENTS.md` per project or module root when local rules differ
- local files may tighten root policy but must not silently weaken it

## Repo-Native Documentation Shape

The official MCAF concepts page recommends durable docs under:

- `docs/Architecture.md`
- `docs/Features/`
- `docs/ADR/`
- `docs/Testing/`
- `docs/Development/`
- `docs/Operations/`

For `.NET` teams, that means build and validation knowledge should not stay only in CI YAML or chat history. The repo should explicitly document:

- exact `dotnet build` commands
- exact `dotnet test` commands
- formatter and analyzer commands
- coverage commands and thresholds when used
- architecture and behavior that tests must prove

## Task Workflow Expectations

The official MCAF guidance expects non-trivial work to follow:

1. root-level `<slug>.brainstorm.md`
2. root-level `<slug>.plan.md`
3. implementation
4. validation against the repo-defined quality pass

Do not compress architecture choice, test strategy, and execution into one improvised edit cycle when the task is non-trivial.

## Verification Expectations

Official MCAF verification rules explicitly favor:

- TDD for new behavior and bug fixes
- user-visible or caller-visible scenario coverage
- integration and end-to-end checks when behavior crosses boundaries
- static analysis as part of done
- full relevant suites green before completion

For `.NET` repos this means “tests passed” is insufficient if the repo also requires:

- analyzers
- `dotnet format`
- coverage
- architecture tests
- security gates

## MCAF Skill Map That Usually Matters Alongside dotnet-skills

The local `managedcode/MCAF` catalog currently exposes 18 separate `mcaf-*` skills. In practice that means teams should route to a narrow governance skill, not cite "MCAF" generically.

Recommended split:

- use MCAF skills for governance, docs, planning, maintainability, testing policy, CI/CD policy, observability, security baseline, and delivery process
- use `dotnet-*` skills from this catalog for framework-specific implementation and validation

The most common `.NET` adoption map is in `references/skill-map.md`.

## Practical Routing For .NET Teams

When the ask is:

- "set up repo rules, AGENTS, docs, and delivery workflow" -> start with `dotnet-mcaf`
- "implement or fix actual ASP.NET/Orleans/EF/Agent Framework code" -> route from `dotnet-mcaf` into the narrowest `dotnet-*` skill
- "tighten verification and CI rules" -> combine `dotnet-mcaf` with the appropriate testing or quality skill
- "choose which local MCAF mirror should own governance work" -> route through the grouped map in `references/skill-map.md`
