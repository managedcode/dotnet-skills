---
name: dotnet-analyzer-config
description: "Use a repo-root `.editorconfig` to configure free .NET analyzer and style rules. Use when a .NET repo needs rule severity, code-style options, section layout, or analyzer ownership made explicit. Nested `.editorconfig` files are allowed when they serve a clear subtree-specific purpose."
compatibility: "Requires a .NET SDK-based repository; respects the repo's `AGENTS.md` commands first."
---

# .NET Analyzer Configuration

## Trigger On

- the repo needs a root `.editorconfig`
- analyzer severity and style ownership are unclear
- the team wants one source of truth for rule configuration

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- choosing analyzers with no config change
- formatting-only execution with no config ownership question

## Inputs

- the nearest `AGENTS.md`
- current `.editorconfig`
- any `Directory.Build.props` overrides

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Prefer one repo-root `.editorconfig` with `root = true`.
2. Add nested `.editorconfig` files when a subtree has a clear scoped purpose, such as stricter rules, different generated-code handling, or a different policy for tests or legacy code.
3. Keep severity in `.editorconfig`, not scattered through IDE settings.
4. Write the file as real EditorConfig, not as a made-up `.NET` variant:
   - lowercase filename `.editorconfig`
   - `root = true` in the preamble
   - no inline comments
   - forward slashes in globs
5. Keep bulk switches such as `EnableNETAnalyzers` in MSBuild files, not in `.editorconfig`.
6. Treat `.globalconfig` as an exceptional case, not the normal repo setup.

## Bootstrap When Missing

If analyzer configuration is requested but not structured yet:

1. Detect current state:
   - `rg --files -g '.editorconfig' -g '.globalconfig'`
   - `rg -n "EnableNETAnalyzers|AnalysisLevel|AnalysisMode" -g '*.csproj' -g 'Directory.Build.*' .`
2. Create or normalize one repo-root `.editorconfig` with `root = true`.
3. Move rule severity into `.editorconfig` and keep bulk analyzer switches in project or MSBuild config.
4. Add nested `.editorconfig` files only when a subtree really needs different scoped policy.
5. Run `dotnet build SOLUTION_OR_PROJECT` and return `status: configured` or `status: improved`.
6. If the repo intentionally uses another documented analyzer-config ownership model, return `status: not_applicable`.

## Deliver

- one explicit analyzer configuration ownership model
- a root `.editorconfig` layout that agents can extend safely

## Validate

- rule severity is reproducible in local and CI builds
- IDE-only settings do not silently override repo policy
- the default path is a root `.editorconfig`, not a surprise alternative

## Ralph Loop

Use the Ralph Loop for every task, including docs, architecture, testing, and tooling work.

1. Plan first (mandatory):
   - analyze current state
   - define target outcome, constraints, and risks
   - write a detailed execution plan
   - list final validation skills to run at the end, with order and reason
2. Execute one planned step and produce a concrete delta.
3. Review the result and capture findings with actionable next fixes.
4. Apply fixes in small batches and rerun the relevant checks or review steps.
5. Update the plan after each iteration.
6. Repeat until outcomes are acceptable or only explicit exceptions remain.
7. If a dependency is missing, bootstrap it or return `status: not_applicable` with explicit reason and fallback path.

### Required Result Format

- `status`: `complete` | `clean` | `improved` | `configured` | `not_applicable` | `blocked`
- `plan`: concise plan and current iteration step
- `actions_taken`: concrete changes made
- `validation_skills`: final skills run, or skipped with reasons
- `verification`: commands, checks, or review evidence summary
- `remaining`: top unresolved items or `none`

For setup-only requests with no execution, return `status: configured` and exact next commands.

## Load References

- `references/analyzer-config.md`
- `references/template.md`
- `references/rules.md`

## Example Requests

- "Make `.editorconfig` the source of truth."
- "Write a proper root `.editorconfig` for this repo."
- "Fix conflicting analyzer severities in this .NET repo."
