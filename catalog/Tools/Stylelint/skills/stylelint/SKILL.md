---
name: stylelint
description: "Use Stylelint in .NET repositories that ship CSS, SCSS, or other stylesheet assets alongside web frontends. Use when a repo needs a dedicated CLI lint gate for selectors, properties, duplicate styles, naming conventions, or design-system rule enforcement."
compatibility: "Requires a .NET repository with stylesheet assets such as `wwwroot/`, `ClientApp/`, `src/`, or other frontend folders managed with Node tooling."
---

# Stylelint for Stylesheets in .NET Repositories

## Trigger On

- the repo has `stylelint.config.*`, `.stylelintrc*`, or CSS and SCSS assets under frontend folders
- the user asks for CSS linting, duplicate style cleanup, naming convention enforcement, or design-system guardrails
- the repo needs a stylesheet gate beyond formatting alone

## Do Not Use For

- JavaScript or TypeScript ownership; route that to `eslint` or `biome`
- runtime accessibility, performance, SEO, or header checks; route that to `webhint`
- repos that intentionally use only Biome for CSS linting and do not want a separate stylesheet linter

## Inputs

- the nearest `AGENTS.md`
- `package.json`
- `stylelint.config.*` or `.stylelintrc*`
- the stylesheet file types in scope: CSS, SCSS, Less, embedded styles, or generated output

## Workflow

1. Confirm what Stylelint should own:
   - plain CSS only
   - CSS plus SCSS
   - embedded styles in HTML, Markdown, or framework files
2. Prefer repo-local installation and checked-in config.
3. Start from a known shared config such as `stylelint-config-standard`, then add syntax-specific packages only when the repo truly needs them.
4. Add repeatable scripts to `package.json`, for example:
   - `stylelint "**/*.{css,scss}"`
   - `stylelint "**/*.{css,scss}" --fix`
5. Keep ignore patterns explicit so build output, vendored assets, and generated CSS do not pollute the signal.
6. Treat autofix as controlled cleanup:
   - run on a bounded scope first
   - inspect the diff
   - rerun the frontend build if the repo compiles styles
7. Use Stylelint for semantic CSS and selector policy, not as a replacement for site-level audits.

## Bootstrap When Missing

1. Detect current state:
   - `rg --files -g 'package.json' -g 'stylelint.config.*' -g '.stylelintrc*'`
   - `rg -n '"stylelint"|"stylelint-config-" --glob 'package.json' .`
2. Prefer a repo-local install:
   - `npm install --save-dev stylelint stylelint-config-standard`
3. Add syntax packages only when the repo needs them for SCSS or embedded styles.
4. Create or refine `stylelint.config.js`, `stylelint.config.mjs`, or the existing config format.
5. Add repeatable commands to `AGENTS.md` and `package.json`, then verify with:
   - `npx stylelint "**/*.{css,scss}"`
   - `npx stylelint "**/*.{css,scss}" --fix`
6. Return `status: configured` if Stylelint is now wired and repeatable, or `status: improved` if the existing baseline was tightened.
7. Return `status: not_applicable` only when another documented tool already owns stylesheet linting and migration is not requested.

## Handle Failures

- `Unknown rule` usually means the config expects a plugin or a different Stylelint major version.
- `Unknown word` on SCSS, Vue, or mixed-content files usually means the repo needs the matching custom syntax package instead of plain CSS parsing.
- Massive autofix churn usually means generated assets or third-party CSS slipped into the lint target.
- Design-system rule noise should be handled by tuning the checked-in config, not by skipping the linter entirely.

## Current 17.8 Guidance

- Stylelint `17.8.0` adds `languageOptions.directionality`, `property-layout-mappings`, `relative-selector-nesting-notation`, and `selector-no-deprecated`. Re-run the repo baseline after upgrading so any new selector or layout findings are reviewed instead of preserved by default.
- Use `languageOptions.directionality` explicitly when the repo styles bidirectional UI or logical properties. That keeps direction-sensitive rules aligned with the intended writing direction rather than inferred behavior.
- The earlier `*syntax` deprecation under `declaration-property-value-no-unknown` still matters on the 17.x line. If the repo still relies on those options, move the compatibility into `customSyntax` or parser selection instead of extending deprecated rule config.
- Keep repo ignores focused on generated or vendored assets. Re-check any broad ignore globs after upgrading so they are not masking selector or layout regressions that the new rules now catch.

## Official Sources

- [Stylelint 17.8.0 release notes](https://github.com/stylelint/stylelint/releases/tag/17.8.0)
- `references/release-notes.md`

## Deliver

- explicit stylesheet lint ownership
- checked-in config and repeatable commands
- clear scope boundaries for CSS, SCSS, and generated assets

## Validate

- the lint target matches the repo's real stylesheet sources
- ignores exclude generated or vendored assets
- Stylelint ownership does not conflict with Biome without an explicit plan
- fixes were verified against the repo's stylesheet build flow when one exists

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

- "Add Stylelint for the SCSS in this ASP.NET Core app."
- "Block duplicate selectors and invalid CSS in CI."
- "Fix the current Stylelint violations without touching generated CSS."

## References

- [release-notes.md](references/release-notes.md) - Current 17.8.0 release changes that matter for repo config, rule tuning, and selector/layout validation
