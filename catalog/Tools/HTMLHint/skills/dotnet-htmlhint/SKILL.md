---
name: dotnet-htmlhint
description: "Use HTMLHint in .NET repositories that ship static HTML output or standalone frontend templates. Use when a repo needs a focused CLI lint gate for DOM structure, invalid attributes, and basic HTML correctness checks on static pages."
compatibility: "Requires a .NET repository with static HTML assets or generated frontend output; use on rendered output rather than raw Razor or Blazor source files."
---

# HTMLHint for Static HTML in .NET Repositories

## Trigger On

- the repo has static HTML files, generated frontend output, or standalone templates under `wwwroot/`, `dist/`, or other web folders
- the user asks for HTML structure checks, invalid attribute detection, or basic DOM-quality linting
- the repo wants a narrow HTML gate separate from JS, CSS, and full-site runtime audits

## Do Not Use For

- raw `.cshtml` or `.razor` source with server-side directives; lint the rendered or published output instead
- JavaScript or TypeScript linting; route that to `dotnet-eslint`
- runtime performance, accessibility, SEO, or headers; route that to `dotnet-webhint`

## Inputs

- the nearest `AGENTS.md`
- `package.json`
- `.htmlhintrc` or equivalent config if present
- the real static HTML target: source templates, built output, or deployed URL

## Workflow

1. Choose the right target first:
   - static HTML source files
   - generated build output such as `dist/`
   - a reachable URL when the page is already served
2. Prefer repo-local installation and checked-in config for repeatable runs.
3. Keep HTMLHint focused on static HTML correctness and lightweight policy.
4. Add narrow scripts to `package.json`, for example:
   - `htmlhint "dist/**/*.html"`
   - `htmlhint "wwwroot/**/*.html"`
5. If the repo has templating syntax that confuses the parser, lint the rendered output instead of forcing source templates through the tool.
6. Use rule overrides deliberately for real project conventions; do not disable broad classes of checks just to make a noisy first pass green.
7. Rerun the publish or frontend build flow if fixes touched generated or packaged HTML sources.

## Bootstrap When Missing

1. Detect current state:
   - `rg --files -g 'package.json' -g '.htmlhintrc*' -g '*.html'`
   - `rg -n '"htmlhint"' --glob 'package.json' .`
2. Prefer a repo-local install:
   - `npm install --save-dev htmlhint`
3. Add or refine `.htmlhintrc` only after confirming the actual target files.
4. Add repeatable commands to `AGENTS.md` and `package.json`, then verify with:
   - `npx htmlhint "dist/**/*.html"`
   - `npx htmlhint https://example.com`
5. Return `status: configured` if HTMLHint now owns a clear static-HTML gate, or `status: improved` if the existing setup was tightened.
6. Return `status: not_applicable` when the repo's HTML is primarily server-rendered templates that should be validated after rendering instead.

## Handle Failures

- Parser noise on Razor, Blazor, or other server-side template syntax is a target-selection problem; lint built output instead of source templates.
- URL-based checks can fail on auth, SPA routing, or environment drift; verify the served target is reachable and stable before trusting the result.
- Large volumes of trivial attribute warnings usually mean the config was copied from another stack and needs to be adapted to the repo's real HTML conventions.

## Deliver

- a repeatable static HTML lint gate
- clear targeting rules for source HTML versus rendered output
- checked-in config that matches the repo's actual page structure

## Validate

- the lint target contains real static HTML, not unsupported template syntax
- commands are reproducible from repo-local dependencies
- HTMLHint ownership is kept separate from broader site-audit tooling
- fixes were verified on the built or served output that actually ships

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

- "Add HTMLHint for the built static site in this repo."
- "Lint the generated HTML before deployment."
- "Why is HTMLHint failing on Razor pages?"
