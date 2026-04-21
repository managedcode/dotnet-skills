# Asynkron.Profiler Overview

## Official Sources

- GitHub repository:
  - <https://github.com/asynkron/Asynkron.Profiler>
- Upstream README:
  - <https://github.com/asynkron/Asynkron.Profiler/blob/main/README.md>
- Tool project:
  - <https://github.com/asynkron/Asynkron.Profiler/blob/main/src/ProfileTool/ProfileTool.csproj>
- Releases:
  - <https://github.com/asynkron/Asynkron.Profiler/releases>

## What The Tool Is

`Asynkron.Profiler` is a dotnet global tool that wraps `dotnet-trace` and `dotnet-gcdump` with a CLI focused on readable profiling output for humans, scripts, and agents.

Its core value is not raw trace collection alone. It gives you:

- a single command surface for CPU, memory, exception, contention, and heap scenarios
- plain-text summaries suitable for terminals, CI logs, or agent workflows
- replay support for existing `.nettrace`, `.speedscope.json`, `.etlx`, and `.gcdump` artifacts

## Install Paths

Install the profiler itself:

```bash
dotnet tool install -g asynkron-profiler --prerelease
```

Install required prerequisites:

```bash
dotnet tool install -g dotnet-trace
dotnet tool install -g dotnet-gcdump
```

Verify:

```bash
asynkron-profiler --help
dotnet-trace --version
dotnet-gcdump --version
```

Upstream currently documents `.NET SDK 10.x` as the expected baseline.

## When To Use This Skill

Use `asynkron-profiler` when:

- the repo wants readable profiling output without opening a GUI profiler
- you need one command that can both collect and render performance data
- CI or agent automation should keep profiling artifacts and summaries in a stable folder
- you already have a trace file and want a focused report from it

Use `profiling` instead when:

- you need the official .NET diagnostics CLIs directly
- you need attach-by-PID, counters, or lower-level trace authoring
- the repo intentionally does not want an extra profiler frontend

## Input And Output Model

### New capture flow

1. Build or choose the target command.
2. Run `asynkron-profiler` in one mode.
3. The tool invokes `dotnet-trace` or `dotnet-gcdump` as needed.
4. Results are written to `profile-output/`.

### Replay flow

1. Point `--input` at an existing artifact.
2. Optionally force the mode.
3. Render the structured report without rerunning the application.

## Supported Input Types

- CPU:
  - `.speedscope.json`
  - `.nettrace`
- Memory:
  - `.nettrace`
  - `.etlx`
- Exceptions:
  - `.nettrace`
  - `.etlx`
- Contention:
  - `.nettrace`
  - `.etlx`
- Heap:
  - `.gcdump`
  - `dotnet-gcdump report` text output

## Practical Defaults

- prefer built `Release` output over `dotnet run`
- start with one mode and only add filters after the baseline run
- keep `profile-output/` under the working directory so traces and reports stay together
- use project or solution paths only when you intentionally want the tool to build and run on your behalf

## Constraints And Tradeoffs

- if `dotnet-trace` or `dotnet-gcdump` is missing from `PATH`, the profiler cannot collect data
- disabled diagnostics IPC on the target process will break trace collection
- `--heap` gives retained-memory shape, not live allocation timelines
- `dotnet run` is convenient but usually less accurate than pointing at the compiled output
- replay mode is ideal for narrowing or sharing a trace, but it cannot recover events that were never captured in the original artifact
