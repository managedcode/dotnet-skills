---
name: dotnet-profiling
version: "1.0.0"
category: "Metrics"
description: "Use the free official .NET diagnostics CLI tools for profiling and runtime investigation in .NET repositories. Use when a repo needs CPU tracing, live counters, GC and allocation investigation, exception or contention tracing, heap snapshots, or startup diagnostics without GUI-only tooling."
compatibility: "Requires a .NET app or process to inspect; respects the repo's `AGENTS.md` commands first."
---

# .NET Profiling

## Trigger On

- the repo needs performance or runtime profiling for a .NET application
- the user asks about slow code, high CPU, GC pressure, allocation growth, exception storms, lock contention, or startup diagnostics
- the team wants official CLI-based diagnostics without depending on `dnx`

## Value

- produce a concrete project delta: code, docs, config, tests, CI, or review artifact
- reduce ambiguity through explicit planning, verification, and final validation skills
- leave reusable project context so future tasks are faster and safer

## Do Not Use For

- replacing realistic performance tests or load tests with ad-hoc tracing alone
- production heap collection when the pause risk has not been accepted
- GUI-only workflows that the repo cannot automate or document

## Inputs

- the nearest `AGENTS.md`
- target application, process, or startup path
- the symptom being investigated: CPU, memory, GC, contention, exceptions, or startup

## Quick Start

1. Read the nearest `AGENTS.md` and confirm scope and constraints.
2. Run this skill's `Workflow` through the `Ralph Loop` until outcomes are acceptable.
3. Return the `Required Result Format` with concrete artifacts and verification evidence.

## Workflow

1. Build and run a realistic target first:
   - prefer `Release`
   - prefer realistic config, inputs, and data volume
2. Start with the lightest useful tool:
   - `dotnet-counters` for live health signals
   - `dotnet-trace` for CPU, exception, contention, GC, and startup traces
   - `dotnet-gcdump` for managed heap inspection when memory shape matters
3. Prefer installed CLI tools over `dnx` one-shot execution so the repo commands stay stable and reproducible.
4. Capture one focused profile at a time instead of mixing every signal into one run.
5. For CPU and general runtime hotspots, start with `dotnet-trace collect`.
6. For live triage, start with `dotnet-counters monitor` on `System.Runtime`.
7. For heap analysis, use `dotnet-gcdump` carefully and document the pause risk.
8. After each change, rerun the same measurement path and compare before versus after.

## Bootstrap When Missing

If official .NET profiling tools are not available yet:

1. Detect current state:
   - `dotnet --info`
   - `dotnet tool list --global`
   - `command -v dotnet-counters`
   - `command -v dotnet-trace`
   - `command -v dotnet-gcdump`
2. Choose the install path deliberately:
   - preferred machine-level install:
     - `dotnet tool install --global dotnet-counters`
     - `dotnet tool install --global dotnet-trace`
     - `dotnet tool install --global dotnet-gcdump`
   - direct-download fallback when global tools are not suitable:
     - use the official Microsoft Learn download links for `dotnet-counters`, `dotnet-trace`, and `dotnet-gcdump`
3. Verify the installed tools resolve correctly:
   - `dotnet-counters --version`
   - `dotnet-trace --version`
   - `dotnet-gcdump --version`
4. Record exact profiling commands in `AGENTS.md`, for example:
   - `dotnet-counters monitor --process-id PID --counters System.Runtime`
   - `dotnet-trace collect --process-id PID --profile dotnet-common,dotnet-sampled-thread-time -o trace.nettrace`
   - `dotnet-gcdump collect --process-id PID --output heap.gcdump`
5. Run one bounded command and return `status: configured` or `status: improved`.
6. If the repo intentionally standardizes on another profiling stack and does not want these tools, return `status: not_applicable`.

## Deliver

- explicit official .NET profiling commands
- a clear profiling path for CPU, counters, and heap inspection
- reproducible diagnostics commands that humans and agents can rerun

## Validate

- the chosen tool matches the actual symptom
- commands target a realistic process and configuration
- before/after comparisons use the same scenario
- heap collection warnings are explicit when `dotnet-gcdump` is used

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

- `references/commands.md`
- `references/patterns.md`
- `references/profiling.md`

## Example Requests

- "Profile this .NET app for CPU hotspots."
- "Investigate GC pressure in this service."
- "Capture counters and a trace from startup."
- "Set up official .NET profiling tools for local investigations."
