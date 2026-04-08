# AGENTS.md

Project: dotnet-skills
Stack: .NET 10, GitHub Actions, Python automation, NuGet, GitHub Releases

Follows [MCAF](https://mcaf.managed-code.com/)

---

## Purpose

This file defines how AI agents work in this repository.

- Root `AGENTS.md` holds the global workflow, repository structure, release and automation policy, and skill-catalog maintenance rules.
- This repository currently uses only the root `AGENTS.md`; add a nearer local `AGENTS.md` only when a subtree needs stricter or more specialized rules.
- The repository has three equally important responsibilities:
  1. Maintain a high-quality scanned catalog under `catalog/<type>/<package>/` for modern and legacy `.NET`.
  2. Maintain repo-owned orchestration agents in top-level `agents/` and package-owned catalog agents in `catalog/<type>/<package>/agents/`.
  3. Maintain automation that watches official upstream releases and documentation so the catalog can be refreshed when the ecosystem changes.

If this repository contains executable code, it must exist only to distribute or install the skill catalog itself, for example as a publishable `dotnet tool`.
Do not turn this repository into a general application or unrelated tooling codebase.

All repository-facing documentation and skill content should be written in English.
Follow official or documented agent standards where they exist; do not present a repo-local adapter as if it were a universal standard.

## Solution Topology

- Solution root: `.`
- Areas with specialized responsibilities:
  - `agents/`: top-level orchestration agents that sit above the skill catalog, one folder per agent
  - `catalog/`: canonical scanned catalog tree, including package manifests plus nested skills and package-owned agents
  - `external-sources/`: vendir transport config, pinned lockfile, checked-in upstream snapshots, and import overrides for external repositories
  - `cli/ManagedCode.DotnetAgents/`: publishable `dotnet-agents` installer tool for repo-owned orchestration agents
  - `cli/ManagedCode.Agents/`: publishable `agents` installer tool for the same repo-owned orchestration agents
  - `cli/ManagedCode.DotnetSkills/`: publishable `dotnet-skills` installer tool
  - `scripts/`: catalog generation and upstream-watch automation
  - `.github/workflows/`: CI, release, and scheduled automation
- Local `AGENTS.md` files currently present: none

## Rule Precedence

1. Read the root `AGENTS.md` first.
2. If a local `AGENTS.md` is later added for a subtree, read the nearest one before editing that area.
3. Apply the stricter rule when both files speak to the same topic.
4. Local `AGENTS.md` files may refine or tighten root rules, but they must not silently weaken them.
5. If a subtree needs a durable exception, document it explicitly in the nearest local `AGENTS.md` or another canonical repo document.

## Path And Linking Rules

- Never commit personal or machine-specific absolute filesystem paths such as `/Users/...`, `/home/...`, or `C:\Users\...` in repository docs, generated site files, manifests, examples, or contributor guidance.
- In repository-facing Markdown, prefer repo-relative links such as `README.md`, `catalog/`, or `.github/workflows/publish-catalog.yml` instead of workstation-local absolute paths.
- For path examples, use portable placeholders such as `~/...`, `/path/to/...`, `<repo-root>/...`, or product-native paths that are not tied to one contributor machine.
- Before committing docs or generated artifacts, scan the diff for leaked local paths and remove them.

## Conversations (Self-Learning)

Learn the user's stable habits, preferences, and corrections. Record durable rules here instead of relying on chat history.

Before doing non-trivial work, evaluate the latest user message.
If it contains a durable rule, correction, preference, or workflow change, update `AGENTS.md` first.
If it is only task-local scope, do not turn it into a lasting rule.

Update this file when the user gives:

- a repeated correction
- a permanent requirement
- a lasting preference
- a workflow change
- a high-signal frustration that indicates a rule was missed
- a rule request for library skill quality, such as “when adding a library skill, include installation and practical usage patterns in the skill body”

Treat explicit frustration, swearing, sarcasm, repeated rejection, or "don't do this again" as strong signals that a durable rule should likely be captured here.

- For interactive CLI UX changes in the publishable tools, run a dedicated Claude-led design pass before finalizing the console presentation.
- Do not ship flat menus where every entry has the same visual weight. Interactive menus must expose clear hierarchy, differentiated sections, and obvious primary flows.
- When the CLI has package-aware workflows, surface NuGet/package entry points clearly in the interactive shell instead of burying them behind generic labels.
- In public UX, docs, CLI commands, and generated site content, always use `bundles` for grouped multi-skill installs (e.g. `dotnet skills install bundle ai`). The word `packages` means NuGet library packages — concrete individual libraries such as AutoMapper, MediatR, Sep, etc. — and the skills that correspond to them. Never conflate these two concepts.
- Skill- or agent-specific manifest metadata belongs in the nearest sibling `manifest.json` next to that `SKILL.md` or `AGENT.md`, not in package-level keyed maps and not in `SKILL.md` frontmatter. For skills, keep `version`, `category`, `packages`, and `package_prefix` in that sibling manifest. Keep package `manifest.json` for package-level metadata such as title, icon, and upstream links.
- Package `manifest.json` should also hold upstream source metadata for the package surface: repository URL plus docs and NuGet links when known. Catalog generators should read those links from manifests and propagate them into exported catalog data and the public site instead of hardcoding or inferring them ad hoc.
- Do not hardcode catalog category lists or catalog type-directory lists as root constants in Python or C#. Derive them from the scanned catalog or a generated artifact sourced from the catalog manifests, so adding a new manifest category or catalog type does not require manual constant edits in runtime code.
- External repositories that contribute skills or agents must live under `external-sources/`: keep vendir transport config in `external-sources/vendir.yml`, the lockfile in `external-sources/vendir.lock.yml`, and checked-in upstream snapshots under `external-sources/upstreams/`. Do not copy external repositories into `catalog/` by hand.
- Normalize vendir-managed upstream content into `catalog/` through `scripts/import_external_catalog_sources.py` plus checked-in overrides under `external-sources/imports/`.
- Import configs under `external-sources/imports/` are overrides-only. Auto-discover upstream plugins from vendored `plugin.json` files instead of maintaining a second manual plugin registry in local config.
- Imported official upstream skills or agents may keep their upstream canonical ids instead of being renamed to fit the local `dotnet-*` convention.
- If an imported official upstream skill or agent is a true duplicate of a repo-authored local entry, prefer the official upstream source and remove the local duplicate instead of keeping two copies.
- Do not inject HTML provenance comments such as `Imported from ... via vendir` into generated `SKILL.md` or `AGENT.md` files. Keep imported content clean; provenance belongs in package metadata, external-source config, or importer logic, not inside the skill or agent body.
- When importing upstream skills or agents, copy upstream `SKILL.md`, `AGENT.md`, and `references/` content verbatim. Do not rewrite their frontmatter, add local headings, inject `compatibility`, synthesize `skills:` lists, or otherwise mutate the markdown body. If local catalog metadata is still needed, keep it in sibling `manifest.json`, not inside the imported markdown.

### Issue Workflow

- When repository work is driven by GitHub issues, complete the implementation end-to-end: inspect the issue, make the repo changes, validate them, commit, push, and close the resolved issues.
- When committing work that resolves repository issues, include the issue-closing references in the commit body, for example `Closes #48`.

Do not record:

- one-off instructions for the current task
- temporary exceptions
- requirements that are already captured elsewhere without change

## Library Skill Standard

- When a user asks to add a skill for a library, update the skill with source-driven usage guidance, not a placeholder.
- The skill must include, at minimum:
  - install path for the library (NuGet/PackageReference/cmd examples where relevant),
  - at least two practical usage snippets (read + write),
  - option/setting patterns that affect behavior,
  - tradeoffs and constraints (for example ref-struct and async limits),
  - validation checks the user can run locally.
- Prefer a practical “how to use this from today” structure: install, read, write, validate.

### Provided Source Links

- When a user provides one or more source URLs for a skill, agent, or durable documentation task, inspect those URLs directly before editing the repository.
- Prefer Playwright for user-provided website and documentation links so the page structure, navigation, and primary content are reviewed from the live source.
- For GitHub repository links, inspect the repository page and the relevant primary materials such as `README`, docs, samples, releases, or package references before turning the source into a skill.
- For Microsoft Learn or other official documentation sites, review the relevant navigation and primary guidance pages from the provided documentation set before writing or materially revising a skill.
- Do not rely on memory or third-party summaries when the user has already provided authoritative source links.
- When a skill is built from a large official documentation set, include a references file that maps the documentation tree with direct links to the relevant pages, including quickstarts, samples, and example-oriented pages instead of only a small curated subset.

## Global Skills

List only the skills this repository actually uses for its own maintenance workflows.

- `skill-creator` - when creating or restructuring skills in `catalog/`
- `mcaf-solution-governance` - when changing `AGENTS.md`, repository governance, or maintenance policy
- `mcaf-documentation` - when changing durable repo docs such as `README.md`, `CONTRIBUTING.md`, or policy docs
- `mcaf-ci-cd` - when changing GitHub Actions, release flow, or automation policy
- `mcaf-dotnet` - when changing the publishable `.NET` tool
- `mcaf-testing` - when adding or updating automated verification for the tool or automation

If work touches `.NET` code in this repository:

- `mcaf-dotnet` is the entry skill and routes to more specialized `.NET` guidance.
- Keep the executable surface limited to the catalog installer tool; repo automation does not need to be moved into `.NET`.
- Recheck `build`, `pack`, smoke-test, and publish workflows when tool behavior changes.
- Do not rely on smoke tests alone for tool changes; keep a real automated `.NET` test project with focused unit or integration coverage for installer behavior, path resolution, command semantics, and recommendation logic when those areas change.

## Canonical Layout

The canonical scanned catalog tree is [`catalog/`](catalog).
The canonical top-level orchestration-agent tree is [`agents/`](agents).

Expected layout:

```text
agents/
├── README.md
└── <agent-slug>/
    ├── AGENT.md
    ├── scripts/        # optional
    ├── references/     # optional
    └── assets/         # optional

catalog/
└── <Type>/
    └── <Package>/
        ├── manifest.json
        ├── icon.svg           # optional
        ├── skills/
        │   └── <skill-slug>/
        │       ├── SKILL.md
        │       ├── manifest.json
        │       ├── scripts/     # optional
        │       ├── references/  # optional
        │       └── assets/      # optional
        └── agents/
            └── <agent-slug>/
                ├── AGENT.md
                ├── manifest.json # optional
                ├── scripts/      # optional
                ├── references/   # optional
                └── assets/       # optional
```

Other important repository files:

- [`CLAUDE.md`](CLAUDE.md): Claude adapter that points Claude to the root repository instructions.
- [`GEMINI.md`](GEMINI.md): Gemini adapter that imports the root repository instructions.
- [`.github/copilot-instructions.md`](.github/copilot-instructions.md): Copilot adapter that points GitHub Copilot to the repository-wide rules.
- [`README.md`](README.md): public catalog and repository overview.
- [`CONTRIBUTING.md`](CONTRIBUTING.md): contributor workflow for skills, versions, descriptions, and watch entries.
- [`agents/README.md`](agents/README.md): index of repo-owned orchestration agents and layout conventions.
- [`external-sources/`](external-sources/): dedicated area for vendir transport config, vendored upstream snapshots, and import overrides.
- [`external-sources/imports/*.json`](external-sources/imports/): overrides-only import policy files for vendir-managed repositories.
- [`catalog/*/*/manifest.json`](catalog/): package manifests that hold package metadata and upstream links for the scanned catalog tree.
- [`catalog/*/*/skills/*/manifest.json`](catalog/): sibling skill manifests that hold skill-specific metadata such as `version`, `category`, `packages`, and `package_prefix`.
- [`catalog/*/*/agents/*/manifest.json`](catalog/): sibling agent manifests for agent-specific metadata when needed.
- [`external-sources/upstreams/`](external-sources/upstreams/): vendir-managed snapshots of external source repositories used by import scripts.
- [`external-sources/vendir.yml`](external-sources/vendir.yml): declarative source-sync config for vendir-managed repositories.
- [`external-sources/vendir.lock.yml`](external-sources/vendir.lock.yml): resolved vendir lock file with pinned upstream SHAs.
- [`.github/workflows/catalog-check.yml`](.github/workflows/catalog-check.yml): pull-request validation workflow for generated catalog outputs and tool smoke checks.
- [`.github/workflows/publish-catalog.yml`](.github/workflows/publish-catalog.yml): unified 04:00 UTC release workflow for `catalog-v*` assets, NuGet tool publish, and GitHub Pages deployment.
- [`.github/upstream-watch.json`](.github/upstream-watch.json): base upstream watch metadata file for labels and shared defaults.
- [`.github/upstream-watch*.json`](.github/): optional upstream watch config shards that hold the human-maintained `github_releases` and `documentation` lists.
- [`.github/upstream-watch-state.json`](.github/upstream-watch-state.json): machine-maintained baseline state.
- [`.github/workflows/upstream-watch.yml`](.github/workflows/upstream-watch.yml): scheduled workflow.
- [`cli/ManagedCode.DotnetSkills/ManagedCode.DotnetSkills.csproj`](cli/ManagedCode.DotnetSkills/ManagedCode.DotnetSkills.csproj): publishable `.NET` tool that installs the catalog through `dotnet skills ...`.
- [`cli/ManagedCode.DotnetAgents/ManagedCode.DotnetAgents.csproj`](cli/ManagedCode.DotnetAgents/ManagedCode.DotnetAgents.csproj): publishable `.NET` tool that installs orchestration agents through `dotnet agents ...`.
- [`cli/ManagedCode.Agents/ManagedCode.Agents.csproj`](cli/ManagedCode.Agents/ManagedCode.Agents.csproj): publishable `.NET` tool that installs orchestration agents through `agents ...`.
- [`dotnet-skills.slnx`](dotnet-skills.slnx): canonical solution entry point for repository-level `dotnet build` and `dotnet pack` commands.
- [`scripts/generate_catalog.py`](scripts/generate_catalog.py): catalog scanner, README generator, and validation entry point.
- [`scripts/generate_catalog_definitions.py`](scripts/generate_catalog_definitions.py): build-time generator that derives catalog categories and type directories from scanned manifests and emits `.g.cs` definitions for the CLI runtime.
- [`scripts/generate_agent_catalog.py`](scripts/generate_agent_catalog.py): agent-catalog validation and optional export helper.
- [`scripts/smoke_test_tool.sh`](scripts/smoke_test_tool.sh): CI smoke test for the installable tool package.
- [`scripts/upstream_watch.py`](scripts/upstream_watch.py): watch runner.
- [`cli/Catalog.Generated.targets`](cli/Catalog.Generated.targets): shared MSBuild import that runs the Python catalog-definitions generator before C# compilation.
- [`github-pages/index.html`](github-pages/index.html): template for the public skills directory website.
- [`scripts/generate_pages.py`](scripts/generate_pages.py): generates the GitHub Pages site with embedded skills and agents data.

## Skill Naming Rules

Use clean `.NET` skill names:

- Good: `dotnet-aspnet-core`
- Good: `dotnet-aspire`
- Good: `dotnet-entity-framework-core`
- Good: `dotnet-microsoft-agent-framework`

Rules:

- Use the `dotnet-*` prefix for repo-authored catalog skills in this repository.
- Vendir-imported upstream skills may preserve their upstream canonical ids.
- Keep one clear responsibility per skill.
- Prefer framework or capability names that match official Microsoft naming.
- Do not invent vanity prefixes.
- Do not create duplicate skills that differ only by wording.
- When a skill in this repository references an external framework that is not itself a `.NET` framework, keep the external framework's canonical name in titles and prose. The `dotnet-*` prefix is this catalog's namespace, not a claim that every referenced framework is part of `.NET`. For example, MCAF should be described as `MCAF`, not as a `.NET` framework.

## When Adding or Updating a Skill

Before adding a new skill:

1. Check whether the capability already exists in [`catalog/`](catalog).
2. Confirm the framework or feature is important enough to justify a dedicated skill.
3. Prefer official Microsoft or first-party documentation to shape the content.
4. Check whether the capability is already covered indirectly by a broader skill such as `dotnet`, `dotnet-architecture`, or `dotnet-aspire`.
5. For `.NET`-scoped skills, prefer `.NET` and C# API references, samples, and watch coverage. Do not add Python-only API references or Python-only upstream watches unless the user explicitly asks for cross-language coverage.

When creating a new skill:

1. Choose the destination package under `catalog/<type>/<package>/`, or create a new package there with a package `manifest.json`.
2. Create `catalog/<type>/<package>/skills/<skill-slug>/`.
3. Add `SKILL.md` plus sibling `manifest.json`.
4. Add `references/` for the heavy material: official docs snapshots, API maps, long examples, migration notes, provider matrices, and other supporting documentation that would bloat `SKILL.md`.
5. Do not create `agents/` under a skill folder. If specialist routing is needed, add a package-owned agent under `catalog/<type>/<package>/agents/` or a top-level orchestrator under `agents/`.
6. Update any related [`README.md`](README.md) notes and regenerate the catalog outputs.
7. If the skill tracks a major framework or Microsoft surface, update the relevant upstream watch shard under [`.github/upstream-watch*.json`](.github/).

## When Adding or Updating an Agent

Agents are a parallel orchestration layer above the skill catalog.

Use these placement rules:

1. Put broad, reusable routing agents in [`agents/`](agents).
2. Put package-owned agents only in `catalog/<type>/<package>/agents/<agent-slug>/AGENT.md`.
3. Do not create `agents/` folders under `skills/<skill-slug>/`. Skill-scoped agents are not part of this repository layout.
4. Keep agents focused on triage, routing, orchestration, and bounded role behavior; keep detailed implementation guidance in `SKILL.md`.
5. Make the linked skill set explicit, so reviewers can see what the agent is expected to orchestrate.
6. Update [`README.md`](README.md) and [`CONTRIBUTING.md`](CONTRIBUTING.md) when the public agent catalog shape changes.

When creating a new agent:

1. Put it in `agents/<agent-slug>/AGENT.md` or `catalog/<type>/<package>/agents/<agent-slug>/AGENT.md`, depending on whether it is a top-level index entry or package-owned catalog content.
2. Keep each agent in its own folder; flat loose agent files in the repo are not the canonical source layout.
3. Add `AGENT.md` with a clear role and routing scope.
4. Prefer concise, role-based agent slugs. Avoid awkward names that simply repeat the full parent skill slug with a generic suffix like `-specialist` when a shorter slug such as `agent-framework-router` or `aspire-orchestrator` would be clearer.
5. Reference the relevant `dotnet-*` skills it is expected to orchestrate.
6. Keep validation explicit: what a good completion looks like, what the agent should hand off, and what it should refuse.
7. Keep `AGENT.md` short and routing-focused. Put bulk framework notes, decision tables, protocol details, and other deep material in sibling `references/` files or in the paired skill instead of turning `AGENT.md` into a second skill-sized document.

## `SKILL.md` Requirements

Every repo-authored skill must include YAML frontmatter:

- `name`
- `description`
- `compatibility`

Every skill must also include a sibling `manifest.json` that defines:

- `version`
- `category`
- optional `packages`
- optional `package_prefix`

Recommended structure:

1. Title
2. `Trigger On`
3. `Workflow`
4. `Deliver`
5. `Validate`

Content rules:

- Keep it practical and agent-oriented.
- Prefer concrete decision logic over generic prose.
- Keep the skill narrow enough that routing decisions stay clear.
- Avoid bloated theory sections.
- Avoid user-facing marketing language.
- Avoid obsolete guidance copied from old blog posts or samples.
- `description` must be an exact, reusable one-line description of what the skill is for, because the README catalog copies it directly.
- `version` must live in the sibling `manifest.json`, use semantic versioning, and be bumped when the skill guidance materially changes.
- `category` must live in the sibling `manifest.json` and match the supported README catalog categories.
- Treat repo-authored `SKILL.md` as the control plane for the skill: trigger conditions, selection logic, workflow, deliverables, and validation. Move large documentation bodies, reference tables, long examples, and mirrored upstream material into `references/`.
- Imported upstream skills may keep their upstream frontmatter shape as-is. When the local catalog needs extra fields such as `compatibility`, store them in the sibling `manifest.json` instead of rewriting the upstream markdown.
- Optimize for token economy. Prefer a short `Load References` section with topic-focused files over one large `SKILL.md` or one giant omnibus reference file.
- When a skill explains non-trivial implementation details, integration flow, component boundaries, or decision logic, add at least one Mermaid diagram instead of leaving the explanation text-only.
- When mirroring or bundling official documentation into a skill's `references/`, also extract the main operational guidance into `SKILL.md` or curated reference summaries. Do not leave the skill usable only as a raw documentation dump.
- When mirroring official docs into a skill snapshot, keep only high-signal, skill-useful markdown. Do not vendor project files, snippets trees, media folders, images, TOC scaffolding, DocFX support files, or Python-only pages unless they are directly necessary for the skill.
- If a mirrored Learn page still contains raw `:::code`, `:::image`, or similar source-asset directives after those assets were excluded, strip those directives from the local snapshot instead of keeping broken references.
- Do not leave orphaned reference files in a skill. Every meaningful file under `references/` must be reachable through an explicit reference path from `SKILL.md` or from an index file that `SKILL.md` points to directly.
- When `SKILL.md` points to files under `references/`, use concise explicit path mentions such as `references/patterns.md` or a Markdown link when human clickability materially helps. Do not rewrite internal references into verbose link syntax just for style.
- Split `references/` by topic, workflow branch, provider, or subsystem so agents can load only the relevant slice. Avoid dumping a whole framework into one mega-file when smaller references would keep context usage lower.
- Curated `references/*.md` files must carry real extracted knowledge. Do not create shallow placeholder references that only restate topic names, point back to the docs mirror, or summarize a whole framework in a few thin bullets. If a reference file exists, it should materially help solve the task without forcing the reader back into the raw docs immediately.
- The top-level `dotnet-ai` orchestration agent should treat Microsoft Agent Framework and Microsoft.Extensions.AI as a combined primary surface when tasks span agent orchestration and `IChatClient`-based provider composition.

## Diagramming Rules

Use Mermaid diagrams to explain non-trivial implementation details across repository-facing content.

Rules:

- Add Mermaid diagrams to skills, contributor docs, plans, and other durable technical notes when they describe workflows, architecture, integration steps, installer behavior, release flow, or branching decision logic.
- Do not rely on text-only explanations when a Mermaid diagram would make the implementation or flow materially clearer.
- Keep diagrams concrete and implementation-oriented; prefer real repo terms, commands, artifacts, and paths over generic boxes.
- Update the Mermaid diagram when the surrounding implementation guidance changes, so the diagram and prose stay in sync.

## README Maintenance Rules

[`README.md`](README.md) is the public index for the catalog.

Whenever you add, rename, split, merge, or remove a skill:

1. For repo-authored entries, update `SKILL.md` frontmatter only for `name`, `description`, or `compatibility`, and update the sibling `manifest.json` for `version`, `category`, `packages`, or `package_prefix`. For imported upstream entries, keep `SKILL.md` verbatim and put any local-only metadata in the sibling `manifest.json`.
2. Update the skill count if it is listed.
3. Update automation notes if watch coverage changes.
4. Let the release workflows generate fresh catalog outputs in CI; run `python3 scripts/generate_catalog.py` locally only when you need a preview.

The source of truth is the scanned `catalog/<type>/<package>/` tree: package `manifest.json`, nested `skills/*/SKILL.md`, and nested `agents/*/AGENT.md`. Do not introduce or rely on checked-in aggregate catalog JSON files as the source of truth.
Do not hand-edit the generated catalog section between `<!-- BEGIN GENERATED CATALOG -->` and `<!-- END GENERATED CATALOG -->`.

Generated catalog outputs:

- [`README.md`](README.md) catalog section

Canonical generation point:

- [`.github/workflows/publish-catalog.yml`](.github/workflows/publish-catalog.yml) for remote catalog releases, transient exported manifests when needed for release assets, the bundled fallback catalog inside the published `.nupkg`, and GitHub Pages deployment

## Dotnet Tool Rules

The only allowed repository-owned executable projects are the publishable catalog installer tools:

- `dotnet-skills`
  - package id: `dotnet-skills`
  - command name: `dotnet-skills`
  - usage shape: `dotnet skills ...`
- `dotnet-agents`
  - package id: `dotnet-agents`
  - command name: `dotnet-agents`
  - usage shape: `dotnet agents ...`
- `agents`
  - package id: `agents`
  - command name: `agents`
  - usage shape: `agents ...`

Rules:

- Keep the tools focused on installing and managing the skill catalog or the repo-owned orchestration agents.
- Do not expand it into a general repo-maintenance application.
- Repo maintenance automation may stay in GitHub Actions scripts and does not need to be moved into the tool.
- Use a clean canonical tool name; avoid redundant public package names with a trailing `.Tool` when the command shape already makes the tool purpose obvious.
- Prefer the public NuGet package IDs `dotnet-skills`, `dotnet-agents`, and `agents` so installation stays `dotnet tool install --global dotnet-skills`, `dotnet tool install --global dotnet-agents`, or `dotnet tool install --global agents`.
- Keep only the manual base version in the project file; CI must derive the publish version automatically by appending the GitHub run number as the numeric patch segment.
- Do not require or document local `dotnet tool install --add-source ...` smoke tests for contributors; validate installability in CI instead and keep user-facing docs focused on the public NuGet install flow.
- Keep canonical skill IDs namespaced as `dotnet-*` in the repository, but let the CLI accept short aliases such as `aspire` or `orleans` in commands.
- Keep `dotnet-skills` as the skill-first CLI. Publish the dedicated agent-only CLI in both supported surfaces: `dotnet-agents` for `dotnet agents ...` and `agents` for `agents ...`. Keep them behaviorally aligned; do not collapse the skill-first and agent-first surfaces into one ambiguous default tool.
- Canonical repo-owned agents live in folder-per-agent layouts with `AGENT.md`; runtime-specific `.agent.md` or native Claude files are adapters, not the source of truth.
- The installer must account for Codex, Claude, Copilot, Gemini, and Junie target layouts instead of assuming only one global skills directory.
- The bare `dotnet skills` entrypoint should behave like a polished interactive console application for browsing the catalog, inspecting details, and installing or removing content without remembering subcommands. Explicit command arguments must still bypass the interactive app and execute directly.
- When vendor-specific install behavior diverges, model it with separate per-platform strategy classes instead of growing one shared resolver or installer full of platform switches.
- Do not duplicate home-directory or environment-root resolution helpers across resolvers. Keep shared path-context logic in one place and let per-platform strategies consume it.
- `SKILL.md` is the canonical skill contract; vendor-specific files are adapters.
- For Copilot, use the official skill and agent locations: project `.github/skills` and `.github/agents`, user `~/.copilot/skills` and `~/.copilot/agents`.
- For Claude Code, use the official native paths: project `.claude/skills` and `.claude/agents`, user `~/.claude/skills` and `~/.claude/agents`.
- For Codex, use the native per-platform buckets that `dotnet-skills` manages: project `.codex/skills` and `.codex/agents`, user `$CODEX_HOME/skills` and `$CODEX_HOME/agents` (default `~/.codex/skills` and `~/.codex/agents`). Keep `.agents/skills` only as the default fallback when no native client root exists.
- For Gemini CLI, use the native paths: project `.gemini/skills` and `.gemini/agents`, user `~/.gemini/skills` and `~/.gemini/agents`.
- For Junie, use the native paths: project `.junie/skills` and `.junie/agents`, user `~/.junie/skills` and `~/.junie/agents`.
- When `--agent` is omitted for skill installation, detect existing native client roots in this order: `.codex`, `.claude`, `.github`, `.gemini`, `.junie`. Install into every detected native client target. Use `.agents/skills` only when none of those native roots exist yet.
- Do not add `.agents/skills` alongside native client targets during auto-detect. `.agents/skills` is fallback-only, not an extra fan-out destination when a native CLI root already exists.
- For repo-owned orchestration agents, auto-detect only vendor-native agent locations: `.codex/agents`, `.claude/agents`, `.github/agents`, `.gemini/agents`, and `.junie/agents`.
- Do not treat shared `.agents` directories as a portable agent target and do not map `.agents` to Codex.
- If `dotnet agents install` or `agents install` runs in auto mode and no native agent directory exists yet, fail with a clear message that asks for an explicit `--agent` or `--target`.
- If `dotnet agents ... --target <path>` or `agents ... --target <path>` is used, require an explicit `--agent`. Agent payload formats differ by platform, so auto mode must not guess a file format for a custom target.
- Use the same NuGet publish pattern as other ManagedCode repositories: publish from `publish-catalog.yml` with `dotnet nuget push` and the `NUGET_API_KEY` secret inside the shell step.
- Do not reference `secrets.*` in GitHub Actions `if:` expressions for NuGet publish branching; keep secret-dependent logic inside the shell step instead.
- Publish workflows should derive the package version from the checked-in base version plus the CI run number instead of relying on a manually typed patch version.
- Keep exactly two primary workflows for release mechanics: `catalog-check.yml` for pull-request validation and `publish-catalog.yml` for the unified nightly release.
- The unified release workflow must run at `04:00` UTC, publish the NuGet tool, create/update the `catalog-v*` GitHub release, and deploy GitHub Pages in the same pipeline.
- The unified release workflow should publish when `main` has new commits since the last `catalog-v*` release; manual dispatch may exist only as a fallback or backfill path, not as the primary workflow.
- The scheduled `04:00` UTC release path must skip publishing when `main` has no unreleased commits since the latest non-draft `catalog-v*` release. Do not create duplicate nightly releases for an already released commit.
- Remote skill content and the NuGet tool should be released from the same scheduled workflow, with `catalog-v*` release assets staying `dotnet-skills-manifest.json` and `dotnet-skills-catalog.zip`.
- Automatic catalog versions should use the numeric calendar-plus-daily-index format `<year>.<month>.<day>.<daily-build-index>`, where the first release for a UTC day is `.0`, the second is `.1`, and so on. Do not add letter prefixes such as `r` or `ci` in release tags or titles.
- The NuGet tool publish workflow must ignore `catalog-v*` releases so catalog content publishes never trigger package pushes by accident.
- `catalog-v*` releases must publish intentional release notes, not a one-line automation placeholder. Release notes should summarize the change window, list merged PRs or commits, call out contributors, and explicitly identify first-time contributors when any appear in that release window.
- The tool should use the newest non-draft `catalog-v*` GitHub release by default and fall back to bundled content only when the remote catalog is unavailable.
- The bare `dotnet skills` usage view is still a normal startup path and must surface the same automatic self-update notice as other startup commands, unless update checks are explicitly suppressed.
- Local `dotnet build` and `dotnet pack` for the tool may generate a temporary manifest in `obj/` from the scanned `catalog/<type>/<package>/` tree; release CI remains the canonical place that generates checked catalog outputs and release assets.

## GitHub Pages Rules

The repository publishes a public skills directory website to GitHub Pages.

Rules:

- The website source lives in `github-pages/index.html` as a template with a `SKILLS_DATA_PLACEHOLDER` marker.
- `scripts/generate_pages.py` scans the `catalog/` tree directly and injects the public catalog data into the template.
- The generated site is output to `artifacts/github-pages/` which is gitignored.
- GitHub Pages deployment runs inside `publish-catalog.yml` as part of the unified nightly release.
- The public site must show the current published `catalog-v*` release version as a visible page element, not only in metadata.
- GitHub Pages generation and deployment must happen only after the current `catalog-v*` release has been created in `publish-catalog.yml`, so the rendered site can use the actual release version from that run.
- The website displays the full skill catalog with search, category filters, installation commands, and a visible orchestration-agents section sourced from the repo catalog.
- Keep the website focused on skill discovery and installation; do not expand it into unrelated documentation.
- The website must show the `dotnet skills install <skill>` command pattern for each skill.
- The website must show a dedicated orchestration-agent install command for repo-owned agents, and it should surface both `agents install <agent>` and `dotnet agents install <agent>` when space allows.
- Dark terminal-like aesthetic with monospace fonts is the intended design language.
- When the site refers to Claude Code, GitHub Copilot, Gemini, and Codex, present them as supported platforms or assistants that consume the catalog, not as repository-owned "AI agents".
- Supported-platform sections on the site should use clearly differentiated brand-like tiles or logos instead of generic repeated cards.
- Supported-platform path examples must stay readable at a glance: avoid aggressive word-breaking, tiny dual-column chips, or layouts that split short filesystem paths into visual fragments.
- The public supported-platforms section should prefer a compact comparison matrix plus lightweight platform identity tiles over tall repeated marketing cards with duplicated copy.
- Footer copyright years on the public site must be generated from the build year during page generation; do not hardcode stale years in the HTML template.
- The public landing page should use tighter spacing rhythm than the current default: avoid oversized shell padding, overly tall card interiors, or loose gaps between onboarding steps and sidebar blocks.
- The public site design must feel refined, polished, and deliberate rather than merely functional. Favor cleaner hierarchy, calmer spacing, more exact typography, and more intentional surfaces instead of coarse generic cards or loose layout blocks.
- Every public-site layout change must be verified against real generated catalog data on desktop and mobile widths, including long skill titles and dense grids. Do not ship clipped badges, overlapping cards, broken gutters, or card content that visually escapes its own column.
- The public site must be fully adaptive across mobile, tablet, laptop, and wide desktop breakpoints. Treat responsive behavior as a first-class requirement: navigation, hero layouts, filters, cards, tables, sidebars, and modal content must remain readable and usable without relying on one preferred viewport size.
- Keep a visible user-facing link to the main ManagedCode website on the public site; do not leave it only in metadata or structured data.
- Avoid cramped, tiny, or overweight public-site UI. On desktop especially, do not compress the catalog into overly narrow columns, overly small cards, or heavy bold typography that makes the layout feel dense and cheap. Favor more breathing room, calmer font weights, and card widths that let long .NET titles read naturally.
- Do not force orchestration-agent cards to share the same dense composition as skill cards. Agent cards need a calmer, page-specific layout: fewer linked-skill pills, clearer hierarchy, and wider columns so they do not read like tall cramped catalog scraps.

## Source-of-Truth Policy

For .NET framework and platform guidance:

- Prefer official Microsoft Learn documentation.
- Prefer official GitHub repositories and release pages for release monitoring.
- Prefer first-party Microsoft product docs over third-party summaries.

For GitHub automation:

- Prefer `gh` CLI for GitHub API work.
- Do not replace `gh api` with raw direct GitHub HTTP calls unless there is a concrete reason.
- It is acceptable to use `curl` for non-GitHub documentation endpoints.

## Upstream Watch Automation

The upstream automation exists so the skill catalog stays current without requiring manual ecosystem monitoring.

Human-maintained upstream watch configuration lives in a small base file plus optional shard files in the same `.github/` folder:

- [`.github/upstream-watch.json`](.github/upstream-watch.json) for shared metadata such as `watch_issue_label` and `labels`
- [`.github/upstream-watch*.json`](.github/) for shard files such as `upstream-watch.ai.json`, `upstream-watch.data.json`, or `upstream-watch-agent-framework.json`

Keep the layout obvious.

Every shard may contain the same two human-maintained lists:

- `github_releases`
- `documentation`

Each entry should stay minimal:

- `source`
- `skills`: affected `dotnet-*` skills

Use `source` for both:

- a GitHub repository URL or `owner/repo` when you want a `github_release` watch
- a documentation URL when you want an `http_document` watch

Optional fields are allowed only when needed:

- `id`
- `name`
- `notes`
- `match_tag_regex`
- `exclude_tag_regex`
- `include_prereleases`

`scripts/upstream_watch.py` loads the base file plus every matching `upstream-watch*.json` shard except `upstream-watch-state.json`, then derives `kind`, source coordinates, and default metadata at runtime.

Sharding rules:

- Prefer a small number of semantic shards such as `ai`, `data`, `platform`, `managedcode`, or `agent-framework`
- Keep shard names semantic and review-friendly
- Do not create numbered fragments such as `10/20/30`
- Do not introduce `.d` directory indirection for this config
- Keep `watch_issue_label` and `labels` in the base `upstream-watch.json` file unless there is a strong reason not to

Supported kinds:

- `github_release`
- `http_document`

When adding a GitHub release watch:

- Prefer the repository that actually signals the .NET-facing release stream.
- If the repository publishes multiple language or package streams, add `match_tag_regex`.
- Use `match_tag_regex` for mixed repos such as Semantic Kernel or Agent Framework, where the latest release may otherwise point to Python or another stream.
- Project-specific watches must point to project-specific skills. Do not map a library watch for a concrete repository to generic umbrella skills such as `dotnet`, `dotnet-architecture`, or `dotnet-orleans` as a substitute for a missing dedicated skill.

When adding a documentation watch:

- Watch stable, meaningful overview pages, not random transient pages.
- Prefer official Microsoft Learn URLs that define platform or framework guidance.
- Keep issue fan-out reviewable. Upstream-watch automation must track one open maintenance issue per library or skill group, not one permanently open issue per individual documentation page when those pages roll up to the same library refresh.
- When another upstream change arrives for a library or skill group that already has an open upstream-watch issue, carry the pending watch context forward into the replacement issue so the new issue starts with the full current upstream state.
- Upstream-watch issue discovery must paginate across the full matching issue set before deciding whether an issue already exists. Do not assume the first page of GitHub issues is sufficient for deduplication or repair.
- Upstream-watch automation must be issue-driven and must not create `catalog-v*` releases or any other user-facing release noise just because machine-maintained watch state changed.
- Do not commit routine upstream-watch state refreshes to `main`. Persist automation state in a non-release-triggering channel so scheduled watch runs can open or rotate issues without manufacturing empty catalog releases.
- When a new upstream event arrives for a library or skill group that already has an open upstream-watch issue, create a fresh issue for the new event and close the older open issue as superseded by the newer one.

## State File Rules

[`.github/upstream-watch-state.json`](.github/upstream-watch-state.json) is machine-maintained state.

Rules:

- Treat the checked-in `.github/upstream-watch-state.json` file as a bootstrap baseline and local fallback, not as a daily-updated commit log on `main`.
- Scheduled automation may restore and save runtime watch state outside tracked files, but it must not push routine state refresh commits to the default branch.
- Do not hand-edit it unless there is a repository emergency.
- To validate watch config structure without contacting upstream sources, run:

```bash
python3 scripts/upstream_watch.py --validate-config
```

- After changing watch definitions, refresh baseline with:

```bash
python3 scripts/upstream_watch.py --sync-state-only
```

- For a non-mutating check, use:

```bash
python3 scripts/upstream_watch.py --dry-run
```

## Validation Checklist

After changing this repository, run the checks that match the work:

For skill and docs changes:

- Verify the new skill folder exists under `skills/`.
- Verify `SKILL.md` exists.
- Verify README links and catalog entries are correct.
- `python3 -m py_compile scripts/generate_catalog.py`
- `python3 scripts/generate_catalog.py --validate-only`
- run `python3 scripts/generate_catalog.py` locally only when you explicitly need a preview of the generated README and manifest

For agent and docs changes:

- Verify the new agent folder exists in `agents/<agent>/` or `catalog/<type>/<package>/agents/<agent>/`.
- Verify `AGENT.md` exists inside that folder.
- Verify the placement matches the intended scope: broad agents top-level, package-owned agents under the package root.
- Verify README and contributing docs explain the new agent surface accurately.

For dotnet tool changes:

- `dotnet build dotnet-skills.slnx`
- `dotnet test dotnet-skills.slnx`
- `dotnet pack dotnet-skills.slnx -c Release`
- validate installability through CI workflow smoke tests, not a documented local `dotnet tool install --add-source ...` loop

For catalog release changes:

- Verify [`.github/workflows/publish-catalog.yml`](.github/workflows/publish-catalog.yml) still publishes `catalog-v*` releases.
- Verify the release assets remain `dotnet-skills-manifest.json` and `dotnet-skills-catalog.zip`.
- Verify the same workflow still publishes the NuGet tool and deploys GitHub Pages.

For GitHub Pages changes:

- `python3 scripts/generate_agent_catalog.py`
- `python3 -m py_compile scripts/generate_pages.py`
- `python3 scripts/generate_pages.py`
- Verify `artifacts/github-pages/index.html` was generated with embedded skills data
- Verify [`.github/workflows/publish-catalog.yml`](.github/workflows/publish-catalog.yml) still deploys GitHub Pages in the nightly release

For automation changes:

- `python3 -m py_compile scripts/upstream_watch.py`
- `python3 scripts/upstream_watch.py --validate-config`
- `python3 scripts/upstream_watch.py --dry-run`
- `python3 scripts/upstream_watch.py --sync-state-only` when the watch config changes
- Verify [`.github/workflows/upstream-watch.yml`](.github/workflows/upstream-watch.yml) still points to the right script and token env

For JSON changes:

- Load the file with Python `json.loads` or equivalent

## Repository Logic

The intended maintenance logic is:

1. Keep the catalog broad enough to cover the real .NET ecosystem.
2. Keep each skill narrow enough that routing is still obvious.
3. Keep content tied to official sources.
4. Use upstream automation to surface change, not to auto-rewrite skills.
5. Open issues when upstream changes happen, then update the affected skills deliberately.

This repository should behave like a maintainable documentation-and-automation system, not like a dump of one-off prompt files.

## Preferences

### Likes

- Public NuGet distribution and CI-verified installability for the tool instead of contributor-local `--add-source` install loops.
- Canonical `dotnet-*` skill IDs in the repository, with short aliases in CLI commands.
- Agent-aware install flows that understand Codex, Claude, Copilot, Gemini, and Junie instead of assuming one shared folder layout.
- Official agent standards and native agent layouts instead of repo-local pseudo-standards.
- One obvious upstream watch config surface: a small base file plus optional shard files with the same two obvious lists: `github_releases` and `documentation`.
- Minimal watch entries: `source` plus related skills, with optional overrides only when really needed.
- English-only durable docs and skill content.
- Catalog manifest generation in CI release workflows instead of relying on contributor-local regeneration.
- Compact, readable CLI output that favors grouped summaries and short status views over giant wrapped tables.
- Top-level orchestration agents for broad `.NET` routing, with package-owned agents living only under `catalog/<type>/<package>/agents/` when they belong to one package surface.
- Folder-per-agent source layout, so every agent can carry its own references, assets, scripts, and future adapter metadata.
- The public landing page Quick Start section must look polished and intentionally composed; it should be one of the strongest visual sections on the site, not a loose grid of equally weighted cards.
- Public site copy should frame Claude Code, GitHub Copilot, Gemini, and Codex as supported platforms with recognizable brand-style presentation, not as "AI agents".
- Public landing page spacing should feel deliberate and compact; excessive whitespace between cards, sections, and step content is a regression.
- Skill catalog cards on the public site must keep category badges and install commands on stable separate rows; badges must never collide with or visually break the command line.
- Skill catalog cards must avoid oversized glassmorphism, heavy blur, inflated pill buttons, and equal-height empty space. Prefer sharper surfaces, tighter padding, calmer shadows, and content-driven card height.
- On dense directory pages such as `/skills/`, catalog cards must use a strict shared composition so the grid reads as deliberate: matching heights per row, stable title/summary/meta/action bands, and line clamping where needed instead of ragged card growth.
- Directory cards that represent navigable resources such as categories should make the whole card feel interactive. Do not leave large dead zones where only a small nested button or heading opens the destination.
- The public skills site should visually align with the main ManagedCode website rather than inventing a separate noisy catalog aesthetic. When redesigning the site, inspect live ManagedCode pages first and reuse their calmer premium typography, spacing, accent restraint, and overall tone.
- Public README hero copy must avoid exact skill counts in the top badge and intro line; keep precise counts only in the generated catalog section where they can stay authoritative.
- The README header generator must normalize duplicate generated lines after merges; one canonical top Skills badge and one canonical intro line only.
- For internal `SKILL.md`, `AGENT.md`, and `references/` content, optimize first for model loading and token economy. Human clickability or decorative Markdown formatting is secondary unless it materially improves maintenance.

### Dislikes

- Monolithic watch configuration files that become unreviewable as custom libraries grow.
- Numbered upstream-watch fragments such as `10/20/30` and `.d` directory indirection for a config that should stay simple.
- User-facing command examples that require the `dotnet-` prefix when the CLI can resolve a short alias.
- Local contributor workflows built around `dotnet tool install --add-source artifacts/nuget`.
- Treating a checked-in aggregate catalog JSON file as the source of truth instead of scanning `catalog/<type>/<package>/manifest.json` together with nested `skills/*/SKILL.md` and `agents/*/AGENT.md`.
- Nesting agents under `skills/<skill>/agents/`; agents belong at the top level or under `catalog/<type>/<package>/agents/`, never inside a skill folder.
- Flat loose `.agent.md` files as the canonical repo source format for agents.
- Default CLI views that dump the entire catalog as a wide multi-line table with heavily wrapped descriptions.
- Weak or awkward Quick Start layout on the public landing page, especially when the onboarding steps look visually scattered or poorly prioritized.
- Misleading public site wording that calls the supported platforms "AI agents" instead of showing them as platforms that use the skill catalog.
- Bloated spacing on the public landing page, especially in Quick Start shells, step stacks, and sidebar cards.
- Broken skill-card footers where category badges and install commands overlap, wrap awkwardly, or compete for the same horizontal space.
- Exact skill counts in the public README hero badge or intro copy, where they go stale and create pointless merge churn.
- README generators that update only the first header occurrence and leave duplicate Skills badges or duplicate intro lines behind after merges.
- Rewriting concise internal reference paths into verbose Markdown-link syntax when that does not help the model.

## Anti-Patterns

Do not do these:

- Create duplicate skill trees.
- Add frameworks without updating the generated catalog inputs and regenerating README.
- Add watch entries without mapping them to affected skills.
- Hand-edit the state file instead of syncing it.
- Hand-edit the generated README catalog section.
- Use noisy GitHub release watches without filtering mixed release streams.
- Base skill logic on unofficial or stale sources when official docs exist.
