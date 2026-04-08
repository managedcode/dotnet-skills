# Official .NET Profiling Tools

## What This Skill Uses

This skill standardizes on official CLI-based .NET diagnostics tools:

- `dotnet-counters` for live metrics and exported counters
- `dotnet-trace` for trace capture and hotspot investigation
- `dotnet-gcdump` for managed heap snapshots

It intentionally avoids `dnx`-only flows so the commands remain explicit and durable in repo docs.

## Installation Paths

Preferred install path for frequent local diagnostics:

```bash
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-gcdump
```

Verify the tools:

```bash
dotnet-counters --version
dotnet-trace --version
dotnet-gcdump --version
```

If global tools are not suitable, use the official Microsoft Learn direct-download links for each tool:

- `dotnet-counters`
- `dotnet-trace`
- `dotnet-gcdump`

## Release-First Rule

Profile realistic builds and realistic scenarios:

```bash
dotnet build -c Release
dotnet run -c Release --project ./src/MyApp/MyApp.csproj
```

Avoid profiling Debug builds unless the debugging overhead itself is the question.

## First-Line Live Triage with dotnet-counters

Use `dotnet-counters` first when you need fast feedback about:

- CPU usage
- GC activity
- allocation growth
- exception rate
- working set
- thread pool pressure

List candidate processes:

```bash
dotnet-counters ps
```

Monitor runtime counters:

```bash
dotnet-counters monitor --process-id PID --counters System.Runtime
```

Export counters for later comparison:

```bash
dotnet-counters collect --process-id PID --counters System.Runtime --format json -o counters.json
```

For startup diagnostics:

```bash
dotnet-counters monitor --counters System.Runtime -- dotnet exec ./bin/Release/net10.0/MyApp.dll
```

## CPU and Runtime Tracing with dotnet-trace

Use `dotnet-trace` when counters show that deeper investigation is needed.

List candidate processes:

```bash
dotnet-trace ps
```

Capture a focused general-purpose trace:

```bash
dotnet-trace collect --process-id PID --profile dotnet-common,dotnet-sampled-thread-time -o trace.nettrace
```

Capture GC-heavy detail:

```bash
dotnet-trace collect --process-id PID --profile gc-verbose -o gc.nettrace
```

Trace startup directly:

```bash
dotnet-trace collect -- dotnet exec ./bin/Release/net10.0/MyApp.dll
```

Get a top-method summary from a captured trace:

```bash
dotnet-trace report trace.nettrace topN --number 20
```

Convert for external viewers when needed:

```bash
dotnet-trace convert trace.nettrace --format Speedscope
```

## Exceptions, Contention, and JIT Clues

`dotnet-trace` is the main CLI path when you need:

- exception-heavy traces
- contention signals
- JIT and runtime event visibility

Keep the run focused and compare the same scenario before and after each fix.

## Heap Investigation with dotnet-gcdump

Use `dotnet-gcdump` when you need managed heap composition rather than CPU stacks.

List candidate processes:

```bash
dotnet-gcdump ps
```

Capture a heap snapshot:

```bash
dotnet-gcdump collect --process-id PID --output heap.gcdump
```

Get a heap summary report:

```bash
dotnet-gcdump report heap.gcdump
```

Important warning:

- `dotnet-gcdump collect` triggers a full Gen 2 GC
- this can pause the target process for a long time on large heaps
- do not use it casually on latency-sensitive production paths

## Practical Investigation Order

1. Reproduce the issue in a realistic `Release` scenario.
2. Start with `dotnet-counters monitor`.
3. If the symptom is CPU or startup related, capture `dotnet-trace`.
4. If the symptom is memory-shape related, capture `dotnet-gcdump`.
5. Apply one fix at a time.
6. Rerun the same command set and compare.

## Useful Guardrails

- use the same user as the target process, or root where required
- on Linux and macOS, the tool and target process may need the same `TMPDIR`
- prefer `dotnet exec` or direct app launch over `dotnet run` for startup tracing, because `dotnet run` may spawn extra child processes
- keep artifacts named and stored predictably so comparisons are easy
