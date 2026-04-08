---
name: dotnet-quality-ci
description: "Set up or refine open-source .NET code-quality gates for CI: formatting, `.editorconfig`, SDK analyzers, third-party analyzers, coverage, mutation testing, architecture tests, and security scanning. Use when a .NET repo needs an explicit quality stack in `AGENTS.md`, docs, or pipeline YAML."
compatibility: "Requires a .NET solution or project; may update `AGENTS.md`, CI workflows, a repo-root `.editorconfig`, `Directory.Build.props`, or analyzer package references."
---

# .NET Quality CI

## Trigger On

- adding or tightening .NET code-quality gates in CI
- choosing analyzers, coverage, mutation, or architecture-test tooling for a .NET repo
- standardizing `.editorconfig`, `dotnet format`, and warning policy

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- non-.NET repositories
- generic CI/CD guidance with no .NET quality stack decisions
- framework-specific test authoring with no quality-gate change

## Inputs

- the nearest `AGENTS.md`
- the current repo-root `.editorconfig` and MSBuild props
- the current CI workflow and package references
- the active test runner model: VSTest or Microsoft.Testing.Platform

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Start with the repo-native baseline:
   - repo-root `.editorconfig`
   - `dotnet format --verify-no-changes`
   - SDK analyzers with explicit `EnableNETAnalyzers`, `AnalysisLevel`, and warning policy
2. Add third-party analyzers only where they close a real gap:
   - `StyleCopAnalyzers`
   - `Roslynator`
   - `Meziantou.Analyzer`
   - framework analyzers such as xUnit, MSTest, or TUnit analyzers
3. Separate quality gates by purpose:
   - formatting and style
   - correctness and static analysis
   - coverage and reports
   - architecture rules
   - security scanning
   - mutation testing
4. For complexity, use a composite approach:
   - CA1502 thresholding
   - maintainability limits in `AGENTS.md`
   - architecture tests
   - coverage and mutation where risk justifies it
5. Make ownership explicit in `AGENTS.md` and CI:
   - which command formats
   - which command analyzes
   - which command measures coverage
   - which runner model the tests use
6. After any .NET code change, the repo's quality pass must be runnable by agents:
   - format
   - build
   - analyze
   - focused tests
   - broader tests
   - coverage and report generation when configured
   - extra configured gates only when the repo actually enabled them
7. Route tool-specific setup through dedicated skills where possible:
   - `dotnet-format`
   - `dotnet-code-analysis`
   - `dotnet-analyzer-config`
   - analyzer-pack skills such as `dotnet-stylecop-analyzers`, `dotnet-roslynator`, and `dotnet-meziantou-analyzer`
   - frontend asset quality skills in mixed `.NET` plus Node repos such as `dotnet-eslint`, `dotnet-stylelint`, `dotnet-htmlhint`, `dotnet-webhint`, `dotnet-biome`, `dotnet-sonarjs`, `dotnet-metalint`, and `dotnet-chous`
   - coverage/reporting skills such as `dotnet-coverlet` and `dotnet-reportgenerator`
   - architecture/security skills such as `dotnet-netarchtest`, `dotnet-archunitnet`, and `dotnet-codeql`
8. Avoid overlapping tools with conflicting ownership. If you add an opinionated formatter, define whether it replaces or complements `dotnet format`.

## Bootstrap When Missing

If a quality gate is requested but not configured, use this activation path:

1. Detect current state in `.csproj`, `Directory.Build.*`, `.editorconfig`, tool manifests, and CI workflow files.
2. Choose exactly one owner command per gate category (format, analyze, test, coverage, architecture, security, mutation).
3. Install the minimal required package or tool and commit checked-in config files.
4. Wire the gate into both `AGENTS.md` and CI with explicit commands.
5. Run a first verify pass, fix actionable failures, and rerun.
6. Return `status: configured` if newly enabled and passing, or `status: improved` if issues remain but baseline improved.
7. Return `status: not_applicable` only when the gate is explicitly out of scope for this repo.


## Deliver

- a documented .NET quality baseline
- CI commands that are explicit and reproducible
- analyzer and coverage choices that match the repo's runner model
- a documented post-change quality pass for agents and CI
- tool selection that stays open-source and free by default, with caveats called out explicitly

## Validate

- repo-root `.editorconfig` is the default source of truth for per-rule severity
- formatting, analyzer, and coverage commands are runner-compatible
- added tools cover distinct gaps instead of duplicating each other
- complexity and architecture policy are explicit, not implied
- .NET code changes are expected to pass more than tests alone when quality gates are configured
- any licensing or hosting caveat is documented before the tool becomes a default gate

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

- `references/editorconfig-and-ci.md`
- `references/quality-toolchain.md`
- `references/workflows.md`
- `references/checklist.md`

## Example Requests

- "Define the best OSS CI stack for this .NET repo."
- "Add .NET analyzers, coverage, and mutation testing guidance."
- "Make `.editorconfig` and CI agree in our .NET solution."
