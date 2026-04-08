---
name: dotnet-eslint
description: "Use ESLint in .NET repositories that ship JavaScript, TypeScript, React, or other Node-based frontend assets. Use when a repo needs a configurable CLI lint gate for frontend correctness, import hygiene, unsafe patterns, or framework-specific rules."
compatibility: "Requires a .NET repository with Node-based frontend assets such as `package.json`, `ClientApp/`, `src/`, or separate web frontend folders."
---

# ESLint for Frontend Assets in .NET Repositories

## Trigger On

- the repo has `package.json`, `eslint.config.*`, `.eslintrc*`, `tsconfig.json`, or JS/TS/React frontend files
- the user asks for JavaScript or TypeScript linting, React rule enforcement, import hygiene, or unsafe frontend patterns
- CI should fail on frontend lint findings instead of relying on editor-only feedback

## Do Not Use For

- CSS ownership by itself; route that to `dotnet-stylelint`
- HTML-only checks on static output; route that to `dotnet-htmlhint`
- repos that intentionally standardized on Biome as the only JS or TS formatter-linter, unless migration or comparison is explicitly requested

## Inputs

- the nearest `AGENTS.md`
- `package.json`
- `eslint.config.*` or `.eslintrc*`
- `tsconfig.json` when TypeScript or type-aware rules are involved

## Workflow

1. Detect ownership first:
   - existing ESLint config
   - package manager and workspace layout
   - framework packages such as React, Next.js, Vue, or plain TypeScript
2. Keep ESLint as the semantic lint owner for JS and TS files when the repo needs rich plugin ecosystems or framework-specific rules.
3. Prefer checked-in local devDependencies over global installs so CI and local runs match.
4. Add narrow scripts to `package.json`, for example:
   - `lint`: `eslint .`
   - `lint:fix`: `eslint . --fix`
5. Scope auto-fix runs before broad rewrites:
   - fix a bounded folder or glob first
   - rerun the linter
   - rerun the frontend build or tests if the repo has them
6. If the repo wants type-aware rules, verify the target files are covered by the intended `tsconfig.json` before enabling heavier rules.
7. Do not hide noise by mass-disabling rules. Fix code, narrow scope, or phase severity deliberately.

## Bootstrap When Missing

1. Detect current state:
   - `rg --files -g 'package.json' -g 'eslint.config.*' -g '.eslintrc*' -g 'tsconfig.json'`
   - `rg -n '"eslint"|"@typescript-eslint"|"eslint-plugin-" --glob 'package.json' .`
2. Prefer a repo-local install:
   - `npm install --save-dev eslint`
3. Add framework-specific plugins only when the repo actually uses the framework.
4. Create or refine `eslint.config.js` or `eslint.config.mjs` instead of leaving setup implicit.
5. Add repeatable commands to `AGENTS.md` and `package.json`, then verify with:
   - `npx eslint .`
   - `npx eslint . --fix`
6. Return `status: configured` if the repo now has a working lint gate, or `status: improved` if existing setup was tightened.
7. Return `status: not_applicable` only when another documented tool already owns JS or TS linting and migration is not requested.

## Handle Failures

- Parsing errors on TS or JSX usually mean the config or parser stack does not match the file set; verify framework plugins and `tsconfig.json` coverage first.
- `Definition for rule ... was not found` usually means a missing plugin package or a config targeting a different ESLint major version.
- `File ignored because of a matching ignore pattern` usually means the glob is too broad or ignores were left stale after a folder move.
- Huge warning floods should be handled in phases with bounded globs, not by downgrading everything to `off`.

## Deliver

- explicit ESLint ownership for JS or TS frontend assets
- checked-in commands for `lint` and `lint:fix`
- config decisions that match the repo's real frontend stack

## Validate

- ESLint is running from repo-local dependencies
- rule ownership is not ambiguous versus Biome or other linters
- the chosen globs match the real frontend folders
- fixes were verified with the repo's normal frontend build or test pass when available

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

- "Add ESLint to the React frontend in this ASP.NET Core repo."
- "Make JS and TS lint failures block CI."
- "Fix the current ESLint warnings without hiding them."
