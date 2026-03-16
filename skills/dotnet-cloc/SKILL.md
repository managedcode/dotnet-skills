---
name: dotnet-cloc
version: "1.0.0"
category: "Metrics"
description: "Use the open-source free `cloc` tool for line-count, language-mix, and diff statistics in .NET repositories. Use when a repo needs C# and solution footprint metrics, branch-to-branch LOC comparison, or repeatable code-size reporting in local workflows and CI."
compatibility: "Requires a repository with .NET source files or a Git checkout; respects the repo's `AGENTS.md` commands first."
---

# CLOC for .NET Repositories

## Trigger On

- the repo wants `cloc`
- the team needs repeatable LOC, language, or branch diff statistics for a .NET repo
- the user asks about C# codebase size, solution composition, or code-count deltas between refs

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- judging developer productivity from raw LOC
- replacing behavioral verification, architecture review, or complexity analysis
- counting generated or vendored files without an explicit reason

## Inputs

- the nearest `AGENTS.md`
- target repository, solution, project, or subtree
- the question being answered: footprint, composition, diff, or trend

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Choose the counting mode deliberately:
   - `--vcs=git` for repo-respecting counts
   - path-based counting for bounded folders
   - `--git --diff BASE HEAD` for change deltas
2. Prefer `.NET`-relevant views first:
   - C# footprint
   - test versus production footprint
   - solution language mix such as C#, Razor, XML, JSON, YAML, and MSBuild files
3. Exclude noise before trusting the numbers:
   - `bin`
   - `obj`
   - `.git`
   - vendored or generated folders when they are not part of the decision
4. Use machine-readable output when the numbers feed docs, CI, or follow-up automation:
   - `--json`
   - `--csv`
   - `--yaml`
   - `--md`
5. Treat `cloc` as a sizing and comparison tool, not as evidence that the design is good.
6. When using diff mode, compare named refs that match the review question:
   - `origin/main..HEAD`
   - release branch versus main
   - before and after a refactor
7. After any code cleanup based on `cloc` findings, run the repo's normal quality pass.

## Bootstrap When Missing

If `cloc` is not available yet:

1. Detect current state:
   - `command -v cloc`
   - `cloc --version`
   - `perl --version`
2. Choose the install path deliberately:
   - macOS with Homebrew: `brew install cloc`
   - Debian or Ubuntu: `sudo apt install cloc`
   - Red Hat or older Fedora family: `sudo yum install cloc`
   - Fedora or newer Red Hat family: `sudo dnf install cloc`
   - npm fallback: `npm install -g cloc`
   - Windows with Chocolatey: `choco install cloc`
   - Windows with Scoop: `scoop install cloc`
   - Docker fallback: `docker run --rm -v $PWD:/tmp aldanial/cloc .`
3. If package-manager builds are not acceptable, install from the latest upstream release or source and verify with `cloc --version`.
4. Record exact counting commands in `AGENTS.md`, for example:
   - `cloc --vcs=git --include-lang="C#,MSBuild,JSON,XML,YAML"`
   - `cloc --by-file --vcs=git --include-lang="C#"`
   - `cloc --git --diff origin/main HEAD --include-lang="C#"`
5. Run one bounded command and return `status: configured` or `status: improved`.
6. If the repo intentionally uses another code-count tool and does not want `cloc`, return `status: not_applicable`.

## Deliver

- repeatable LOC and language-mix reporting for .NET repos
- explicit include and exclude rules
- branch-diff or bounded-scope commands that answer a concrete engineering question

## Validate

- counts match the intended source boundary instead of including build output noise
- command choice matches the reporting question
- any automation or docs that consume the numbers can rerun the same command
- `cloc` is used as context, not as a substitute for tests or design review

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

Open only what you need:

- [commands.md](references/commands.md)
- [examples.md](references/examples.md)
- [cloc.md](references/cloc.md)

## Example Requests

- "Add cloc reporting to this .NET repo."
- "Compare code size between main and this branch."
- "Count C# versus test footprint in this solution."
- "Give me a machine-readable line-count report for CI."
