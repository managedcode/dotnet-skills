---
name: dotnet-chous
description: "Use Chous in .NET repositories that ship sizeable frontend codebases and want file-structure linting, naming convention enforcement, and folder-layout policy as a CLI gate. Use when the problem is frontend architecture drift in the file tree rather than semantic code issues inside the files."
compatibility: "Requires a repository with a meaningful frontend file tree and Node-based tooling or `npx`; Chous complements code linters and does not replace ESLint, Stylelint, or runtime site audits."
---

# Chous for Frontend File-Structure Linting in .NET Repositories

## Trigger On

- the repo has a growing frontend tree and the user asks about naming conventions, folder structure, or file placement rules
- the repo wants to enforce layout policy for `ClientApp/`, `src/`, `apps/`, or `packages/`
- architectural drift in the frontend file tree is a larger problem than syntax errors

## Do Not Use For

- semantic code bugs, type errors, or framework API misuse
- CSS, HTML, or JS rule enforcement inside files
- very small repos where a structure linter would add more ceremony than value

## Inputs

- the nearest `AGENTS.md`
- `package.json`
- any existing `.chous` file
- the frontend tree that needs policy enforcement

## Workflow

1. Define the structure problem first:
   - naming convention drift
   - component placement
   - forbidden folders or files
   - monorepo frontend boundaries
2. Start from `chous init` or a known preset, then tighten only the rules the repo can explain.
3. Keep the checked-in `.chous` file readable enough that future contributors understand the policy.
4. Add repeatable commands such as:
   - `npx chous`
5. Exclude generated folders, build artifacts, and vendored assets so the signal stays architectural.
6. Use Chous as a supplement to semantic linters, not as their replacement.
7. Re-run after moves or refactors to confirm the structure policy still matches the intended design.

## Bootstrap When Missing

1. Detect current state:
   - `rg --files -g 'package.json' -g '.chous'`
   - `rg -n '"chous"' --glob 'package.json' .`
2. Start with the official no-install or global paths:
   - `npx chous`
   - `npm install -g chous`
3. Initialize config when the repo truly wants structure policy:
   - `npx chous init`
4. Add a repeatable command to `AGENTS.md` and `package.json`, then verify with:
   - `npx chous`
5. Return `status: configured` if the repo now has a checked-in structure-lint baseline, or `status: improved` if an existing baseline was tightened.
6. Return `status: not_applicable` when the repo is too small or too fluid to justify a structure-lint gate right now.

## Handle Failures

- If Chous flags large parts of the tree after the first rollout, the rule set is probably too strict for the repo's current maturity; start from the preset and tighten incrementally.
- Generated or vendored folders should be excluded instead of repeatedly ignored in reviews.
- If contributors cannot explain what a rule protects, simplify the `.chous` policy before enforcing it in CI.

## Deliver

- a checked-in frontend structure policy
- repeatable file-tree linting commands
- explicit exclusions for generated and vendored folders

## Validate

- the `.chous` rules reflect real architecture intent
- generated output is excluded
- Chous is used alongside, not instead of, semantic linters
- the policy remains understandable after the first rollout

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

- "Enforce frontend folder naming and placement rules."
- "Add file-structure linting to the web client."
- "Why is our frontend tree drifting even though code linting passes?"
