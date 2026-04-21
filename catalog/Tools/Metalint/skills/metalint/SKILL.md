---
name: metalint
description: "Use Metalint in .NET repositories that ship Node-based frontend assets and want one CLI entrypoint over several underlying linters. Use when a repo wants to orchestrate ESLint, Stylelint, HTMLHint, and related frontend checks from a single checked-in `.metalint/` configuration."
compatibility: "Requires a .NET repository with a Node-based frontend and multiple underlying linters; Metalint is an orchestrator, not a replacement for the actual linter configs it delegates to."
---

# Metalint for Aggregated Frontend Linting in .NET Repositories

## Trigger On

- the repo wants one command to run several frontend linters together
- the user asks for a unified lint entrypoint over ESLint, Stylelint, HTMLHint, or similar tools
- the repo already has multiple linters and the problem is orchestration rather than choosing a single owner

## Do Not Use For

- simple repos where one tool such as Biome already covers the required surface
- teams that have not decided which underlying linters own JS, CSS, and HTML yet
- replacing the underlying linter configs with one vague wrapper config

## Inputs

- the nearest `AGENTS.md`
- `package.json`
- existing linter configs
- any `.metalint/` directory already checked in

## Workflow

1. Define underlying ownership first:
   - ESLint for JS or TS
   - Stylelint for CSS or SCSS
   - HTMLHint for static HTML
   - other delegated linters only when the repo really uses them
2. Use Metalint only after those owners are explicit.
3. Keep all wrapper configuration under `.metalint/` and keep the delegated configs reviewable.
4. Add package scripts such as:
   - `lint`: `metalint`
   - `lint:fix`: `metalint --fix`
5. Treat formatter overlap carefully. If delegated tools can all fix files, define which ones are allowed to mutate which globs.
6. Use Metalint in CI when the repo benefits from a single frontend lint step and formatter output such as GitHub annotations.
7. Re-run the underlying owners directly when debugging Metalint issues so failures stay attributable.

## Bootstrap When Missing

1. Detect current state:
   - `rg --files -g 'package.json' -g '.metalint/**' -g 'eslint.config.*' -g 'stylelint.config.*' -g '.htmlhintrc*'`
   - `rg -n '"metalint"|"eslint"|"stylelint"|"htmlhint"' --glob 'package.json' .`
2. Prefer a repo-local install:
   - `npm install --save-dev metalint`
3. Install the delegated linters the repo actually needs; Metalint does not replace them.
4. Create `.metalint/metalint.config.js` plus delegated config files under `.metalint/` only when the repo wants that consolidated layout.
5. Verify with:
   - `npx metalint`
   - `npx metalint --fix`
6. Return `status: configured` if the repo now has a working aggregated entrypoint, or `status: improved` if orchestration was tightened.
7. Return `status: not_applicable` when the repo intentionally stays with direct linter commands and does not want the wrapper layer.

## Handle Failures

- If Metalint says a delegated linter is missing, install that linter or remove it from the wrapper config; Metalint cannot invent the underlying tool.
- If fix mode causes conflicting rewrites, split ownership by glob instead of letting several tools mutate the same files blindly.
- If the wrapper becomes harder to understand than direct commands, the repo probably does not need Metalint.
- Debug noisy failures by running the delegated linter directly first, then come back to Metalint integration.

## Deliver

- one repeatable frontend lint entrypoint
- explicit delegated-tool ownership
- wrapper config that stays readable and attributable

## Validate

- each delegated linter is actually installed and configured
- fix ownership is explicit across file types
- CI output remains attributable to the right underlying tool
- Metalint reduced operational friction instead of hiding the real owners

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

- "Give this repo one frontend lint command."
- "Wrap ESLint, Stylelint, and HTMLHint under Metalint."
- "Use Metalint in GitHub Actions for frontend checks."
