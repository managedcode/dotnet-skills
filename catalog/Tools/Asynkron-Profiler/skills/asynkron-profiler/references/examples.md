# Asynkron.Profiler Examples

## CPU

Use `--cpu` for sampled hotspots and top-function tables.

```bash
dotnet build -c Release examples/cpu/CpuDemo.csproj
asynkron-profiler --cpu -- ./examples/cpu/bin/Release/net10.0/CpuDemo
```

What to expect:

- a total-time call tree
- top functions by sampled time
- optional self-time view when `--calltree-self` is enabled

## Memory

Use `--memory` for allocation-heavy paths and per-type call trees.

```bash
dotnet build -c Release examples/memory/MemoryDemo.csproj
asynkron-profiler --memory -- ./examples/memory/bin/Release/net10.0/MemoryDemo
```

What to expect:

- allocation totals by type
- a sampled allocation call tree
- better signal when the workload is large enough to emit GC allocation ticks

## Exceptions

Use `--exception` when thrown exceptions are part of the performance or correctness issue.

```bash
dotnet build -c Release examples/exception/ExceptionDemo.csproj
asynkron-profiler --exception --exception-type "InvalidOperation" -- ./examples/exception/bin/Release/net10.0/ExceptionDemo
```

What to expect:

- thrown counts
- throw-site call tree
- narrower output when `--exception-type` is supplied

## Contention

Use `--contention` for lock-heavy or thread-blocking scenarios.

```bash
dotnet build -c Release examples/contention/ContentionDemo.csproj
asynkron-profiler --contention -- ./examples/contention/bin/Release/net10.0/ContentionDemo
```

What to expect:

- wait-time call tree
- top contended methods
- clearer results when the workload creates repeatable contention

## Heap

Use `--heap` when retained-memory shape matters more than allocation rate.

```bash
dotnet build -c Release examples/heap/HeapDemo.csproj
asynkron-profiler --heap -- ./examples/heap/bin/Release/net8.0/HeapDemo
```

What to expect:

- top retained types
- heap byte totals and object counts
- a snapshot view rather than an allocation timeline

## Troubleshooting

### Missing prerequisites

If the command fails because a prerequisite tool is missing:

```bash
dotnet tool install -g asynkron-profiler --prerelease
dotnet tool install -g dotnet-trace
dotnet tool install -g dotnet-gcdump
```

### Diagnostics IPC failures

If you see diagnostics session or IPC creation failures:

- confirm the target process allows diagnostics
- avoid `DOTNET_EnableDiagnostics=0`
- avoid `COMPlus_EnableDiagnostics=0`
- run the profiler as the same user that launches the target process

### Empty or weak data

If the output is empty or not useful:

- rerun against built `Release` output instead of `dotnet run`
- increase the workload or iteration count
- use the mode that matches the signal:
  - CPU for hotspots
  - memory for allocations
  - contention for lock waits
  - heap for retained memory
- verify the input artifact matches the replay mode
