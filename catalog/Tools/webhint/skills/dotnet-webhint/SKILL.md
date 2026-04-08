---
name: dotnet-webhint
description: "Use webhint in .NET repositories that ship browser-facing frontends. Use when a repo needs CLI audits for accessibility, performance, security headers, PWA signals, SEO, or runtime page quality against a served site or built frontend output."
compatibility: "Requires a .NET repository with a browser-facing site or frontend build output; works best against a reachable local or deployed URL and a checked-in `.hintrc`."
---

# webhint for Browser-Facing Frontends in .NET Repositories

## Trigger On

- the repo ships a browser-facing site and the user asks about accessibility, performance, SEO, security headers, or page quality
- the repo has `.hintrc`, `hint` scripts, or a served local frontend that should be audited
- the team needs more than syntax linting and wants runtime-oriented site checks

## Do Not Use For

- JavaScript or TypeScript semantic linting; route that to `dotnet-eslint` or `dotnet-biome`
- stylesheet-only linting; route that to `dotnet-stylelint`
- static HTML structure checks alone; route that to `dotnet-htmlhint`

## Inputs

- the nearest `AGENTS.md`
- `package.json`
- `.hintrc` if present
- the real audit target: local dev URL, preview URL, deployed URL, or built output

## Workflow

1. Choose the audit surface deliberately:
   - running local URL such as `https://localhost:3000`
   - preview or deployed URL
   - local connector against built output when no browser runtime is needed
2. Prefer repo-local installation and a checked-in `.hintrc`.
3. Start from a documented preset such as `web-recommended`, then customize only for real repo requirements.
4. Add repeatable scripts to `package.json`, for example:
   - `hint https://localhost:3000`
   - `hint https://example.test --config .hintrc`
5. Keep runtime prerequisites explicit:
   - supported Node.js version
   - browser availability when the connector needs Chromium-based automation
6. Treat findings as categorized work:
   - headers and transport
   - accessibility and HTML issues
   - performance
   - PWA and manifest signals
7. Re-run the audit after fixes on the same URL or build output so results are comparable.

## Bootstrap When Missing

1. Detect current state:
   - `rg --files -g 'package.json' -g '.hintrc*'`
   - `rg -n '"hint"' --glob 'package.json' .`
2. Prefer a repo-local install:
   - `npm install --save-dev hint`
3. Create or refine `.hintrc` with a known baseline such as `web-recommended`.
4. Add repeatable commands to `AGENTS.md` and `package.json`, then verify with:
   - `npx hint https://localhost:3000`
   - `npx hint -c ./.hintrc https://example.com`
5. Return `status: configured` if the repo now has a working site-audit gate, or `status: improved` if the baseline was tightened.
6. Return `status: not_applicable` when the repo does not expose a stable browser-facing surface that can be audited in the current task.

## Handle Failures

- Missing-browser errors usually mean the environment lacks Chrome, Chromium, or Edge for the selected connector.
- WSL is a poor default for browser-backed runs; prefer a native environment or switch to a `jsdom`-style connector when appropriate.
- `EACCES` or install-permission failures are usually fixed by installing `hint` as a repo devDependency instead of relying on a global tool.
- If the audit target is unstable, authenticated, or still booting, fix the serving workflow first; otherwise the noise is not actionable.

## Deliver

- a repeatable webhint audit command and config
- a stable target URL or build-output strategy
- categorized runtime-quality findings the team can act on

## Validate

- the audited target matches the site that actually ships
- browser or connector prerequisites are documented
- webhint is not being used as a substitute for ESLint or Stylelint
- reruns on the same target produce consistent comparisons

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

- "Run webhint against the local frontend before release."
- "Add accessibility and security-header audits for this site."
- "Why does webhint fail in CI but not locally?"
