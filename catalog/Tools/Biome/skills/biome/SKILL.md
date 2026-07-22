---
name: biome
description: "Use Biome in .NET repositories that ship Node-based frontend assets and want a fast combined formatter-linter-import organizer for JavaScript, TypeScript, CSS, JSON, GraphQL, or HTML. USE FOR: biome.json or @biomejs/biome setup; fast frontend formatting and linting; replacing overlapping frontend style tools deliberately. DO NOT USE FOR: ESLint-only plugin coverage; runtime site audits such as headers, accessibility, or browser behavior. INVOKES: inspect the repository context, edit targeted files, and run relevant build, test, lint, or validation commands when changes are made."
compatibility: "Requires a .NET repository with frontend assets managed through Node or a standalone Biome binary workflow; keep ownership explicit versus ESLint, Stylelint, and webhint."
---

# Biome for Frontend Assets in .NET Repositories

## Trigger On

- the repo has `biome.json`, `@biomejs/biome`, or the user asks for a faster all-in-one frontend formatter-linter stack
- the repo wants one tool for formatting, linting, and import organization across JS, TS, CSS, JSON, GraphQL, or HTML
- the team is comparing Biome against ESLint plus Prettier or wants to simplify the current stack

## Do Not Use For

- repos that rely on ESLint plugins or framework-specific rules Biome does not cover yet
- runtime site audits such as headers, accessibility, and SEO; route that to `webhint`
- cases where a dedicated CSS or HTML tool is still the deliberate owner and no migration is requested

## Inputs

- the nearest `AGENTS.md`
- `package.json`
- `biome.json` or `biome.jsonc`
- current ownership across ESLint, Prettier, Stylelint, and import ordering

## Workflow

1. Decide ownership first:
   - Biome as the main formatter and linter
   - Biome only for formatting
   - Biome in coexistence with ESLint for plugin gaps
2. Prefer a repo-local pinned install so CI and developer machines use the same version.
3. Generate `biome.json` only after confirming what the repo wants Biome to own.
4. Add repeatable scripts to `package.json`, for example:
   - `biome check .`
   - `biome check . --write`
5. Keep file ownership explicit:
   - Biome can own formatting, linting, and import sorting
   - webhint still owns site-runtime audits
   - ESLint may stay for plugin-heavy cases the repo intentionally keeps
6. Start migrations with `check` and bounded folders before flipping the whole repo to `--write`.
7. Re-run the frontend build and tests after broad formatting or lint-fix passes.

## Current Upstream Notes

- Biome CLI `2.5.5` fixes incomplete type-inference false positives and deadlocks, Astro/Svelte shorthand ARIA-role detection, unresolved package subpath imports, Vue argument-less `v-bind`, HTML embedded style/script removal, CSS comment/casing formatting, and shared-daemon disconnect behavior. It also adds `noNegationInEqualityCheck` and CSS `@custom-media` support.
- Re-run the repo's existing `biome check` command after upgrading, especially when type-aware rules or `--write` are enabled. Remove suppressions only when the affected Astro/Svelte/Vue, HTML, CSS, resolver, or Promise-inference cases are covered by repository fixtures.
- The watched Biome release is `@biomejs/js-api@6.0.0`, not a core CLI-only release. It adds `spanInBytesToSpanInCodeUnits` for converting Biome UTF-8 byte spans to JavaScript UTF-16 code unit spans.
- Use that helper when custom JavaScript tooling slices source text from Biome diagnostics, especially with non-ASCII content. Normal `.NET` repos that only run the Biome CLI usually need no config change for this release.
- Recent Biome changes also continue expanding CSS/SCSS, HTML accessibility, import sorting, watch mode, and upgrade-command surfaces; verify actual CLI ownership before replacing ESLint or Stylelint.

## Bootstrap When Missing

1. Detect current state:
   - `rg --files -g 'package.json' -g 'biome.json*'`
   - `rg -n '"@biomejs/biome"|"eslint"|"prettier"|"stylelint"' --glob 'package.json' .`
2. Prefer a repo-local pinned install:
   - `npm i -D -E @biomejs/biome`
3. Create config deliberately:
   - `npx @biomejs/biome init`
4. Add repeatable commands to `AGENTS.md` and `package.json`, then verify with:
   - `npx @biomejs/biome check .`
   - `npx @biomejs/biome check . --write`
5. Return `status: configured` if Biome is now wired with explicit ownership, or `status: improved` if the existing setup was tightened.
6. Return `status: not_applicable` when the repo intentionally stays on ESLint-centered ownership and no migration or comparison was requested.

## Handle Failures

- Missing-rule parity with specialized ESLint plugins is an ownership problem; keep ESLint for those files until the gap is intentionally closed.
- Overly broad `--write` runs can cause large churn; start with bounded folders or changed files first.
- Generated assets or vendored code should be excluded in `biome.json` before trusting the signal.
- If developers complain that Biome and ESLint disagree, define file ownership instead of running both broadly on the same surface by accident.

## Deliver

- explicit Biome ownership and version pinning
- checked-in config and repeatable `check` commands
- a migration or coexistence plan versus ESLint and other frontend tools

## Validate

- the chosen ownership model is documented
- CI and local runs use the same Biome version
- the target globs exclude generated and vendored assets
- downstream build or test flows still pass after `--write` runs

## Ralph Loop

1. Plan: analyze current state, target outcome, constraints, and risks.
2. Execute one step and produce a concrete delta.
3. Review the result and capture findings.
4. Apply fixes in small batches and rerun checks.
5. Update the plan after each iteration.
6. Repeat until outcomes are acceptable.
7. If a dependency is missing, bootstrap it or return `status: not_applicable` with a reason.

### Required Result Format

- `status`: `complete` | `clean` | `improved` | `configured` | `not_applicable` | `blocked`
- `plan`: concise plan and current step
- `actions_taken`: concrete changes made
- `verification`: commands, checks, or review evidence
- `remaining`: unresolved items or `none`

## Example Requests

- "Replace Prettier and basic linting with Biome in this repo."
- "Add Biome to the frontend under ClientApp."
- "Explain whether we should keep ESLint after adding Biome."
