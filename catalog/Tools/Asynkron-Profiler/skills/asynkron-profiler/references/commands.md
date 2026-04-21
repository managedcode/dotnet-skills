# Asynkron.Profiler Commands

## Baseline Verification

```bash
asynkron-profiler --help
dotnet-trace --version
dotnet-gcdump --version
```

## Capture New Profiles

Build first:

```bash
dotnet build -c Release
```

CPU profile the compiled app:

```bash
asynkron-profiler --cpu -- ./bin/Release/<tfm>/MyApp
```

Profile a framework-dependent app:

```bash
asynkron-profiler --cpu -- dotnet ./bin/Release/<tfm>/MyApp.dll
```

Profile a project or solution directly:

```bash
asynkron-profiler --cpu -- ./MyApp.csproj
asynkron-profiler --cpu -- ./MySolution.sln
```

Only use `dotnet run` when you accept build and host noise:

```bash
asynkron-profiler --cpu -- dotnet run -c Release ./MyApp.csproj
```

## Mode Commands

CPU hotspots:

```bash
asynkron-profiler --cpu -- ./bin/Release/<tfm>/MyApp
```

Allocation profiling:

```bash
asynkron-profiler --memory -- ./bin/Release/<tfm>/MyApp
```

Thrown exceptions:

```bash
asynkron-profiler --exception -- ./bin/Release/<tfm>/MyApp
```

Lock contention:

```bash
asynkron-profiler --contention -- ./bin/Release/<tfm>/MyApp
```

Heap snapshot:

```bash
asynkron-profiler --heap -- ./bin/Release/<tfm>/MyApp
```

## Replay Existing Artifacts

Auto-select from file extension:

```bash
asynkron-profiler --input /path/to/trace.nettrace
```

Force CPU rendering for a Speedscope file:

```bash
asynkron-profiler --input /path/to/trace.speedscope.json --cpu
```

Render memory from `.etlx`:

```bash
asynkron-profiler --input /path/to/trace.etlx --memory
```

Render contention from `.etlx`:

```bash
asynkron-profiler --input /path/to/trace.etlx --contention
```

Render exceptions from `.etlx`:

```bash
asynkron-profiler --input /path/to/trace.etlx --exception
```

Render a heap dump:

```bash
asynkron-profiler --input /path/to/heap.gcdump --heap
```

## Tuning Output

Anchor the tree to one subsystem:

```bash
asynkron-profiler --memory --root "MyNamespace" -- ./bin/Release/<tfm>/MyApp
```

Reduce call tree noise:

```bash
asynkron-profiler --cpu --calltree-depth 8 --calltree-width 6 -- ./bin/Release/<tfm>/MyApp
```

Inspect self time:

```bash
asynkron-profiler --cpu --calltree-self -- ./bin/Release/<tfm>/MyApp
```

Focus exception analysis:

```bash
asynkron-profiler --exception --exception-type "InvalidOperationException" -- ./bin/Release/<tfm>/MyApp
```

Filter function tables:

```bash
asynkron-profiler --contention --filter "MyApp.Services" -- ./bin/Release/<tfm>/MyApp
```

Target a specific framework from a project:

```bash
asynkron-profiler --cpu --tfm net10.0 -- ./MyApp.csproj
```

## Manual Official-Tool Collection Plus Replay

Collect first with `dotnet-trace`, then render:

```bash
dotnet-trace collect --output ./profile-output/app.nettrace -- dotnet run MyProject.sln
asynkron-profiler --input ./profile-output/app.nettrace --cpu
```

This pattern is useful when:

- the trace is collected in CI or another machine
- you want to archive raw traces separately from the rendered report
- a later task needs multiple render passes over the same artifact
