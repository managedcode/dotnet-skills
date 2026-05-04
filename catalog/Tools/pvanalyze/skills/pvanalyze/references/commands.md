# pvanalyze Commands

## Baseline

```bash
pvanalyze --help
pvanalyze info ./trace.nettrace
```

## GC

```bash
pvanalyze gcstats ./trace.nettrace
pvanalyze gcstats ./trace.nettrace --format json
pvanalyze gcstats ./trace.nettrace --timeline
pvanalyze gcstats ./trace.nettrace --longest 5
pvanalyze gcstats ./trace.nettrace --from 1000 --to 2000 --timeline
pvanalyze gcstats ./trace.nettrace --process MyApp --format json
```

Use for pause time, heap size, generation distribution, and per-GC timelines.

## JIT

```bash
pvanalyze jitstats ./trace.nettrace
pvanalyze jitstats ./trace.nettrace --process MyApp --format json
```

Use for method compilation count, JIT CPU time, IL size, and native size signals.

## CPU Stacks

```bash
pvanalyze cpustacks ./trace.nettrace --top 20
pvanalyze cpustacks ./trace.nettrace --group-by module --top 10
pvanalyze cpustacks ./trace.nettrace --group-by namespace --inclusive
pvanalyze cpustacks ./trace.nettrace --from 1000 --to 2000 --top 10
pvanalyze cpustacks ./trace.nettrace --format json
pvanalyze cpustacks ./trace.nettrace --format speedscope --output ./trace.speedscope.json
```

Use `--inclusive` when parent cost matters more than leaf-frame self cost.

## Call Tree

```bash
pvanalyze calltree ./trace.nettrace --depth 5
pvanalyze calltree ./trace.nettrace --hot-path
pvanalyze calltree ./trace.nettrace --caller-callee "Serialize"
pvanalyze calltree ./trace.nettrace --hot-path --format json
pvanalyze calltree ./trace.nettrace --min-percent 2 --depth 6
```

Use `--hot-path` for the dominant chain and `--caller-callee` for one method substring.

## Allocation

Collect allocation events first:

```bash
dotnet-trace collect --providers "Microsoft-Windows-DotNETRuntime:0x200001:5" -- dotnet run -c Release
```

Analyze:

```bash
pvanalyze alloc ./trace.nettrace --top 20
pvanalyze alloc ./trace.nettrace --group-by namespace --format json
pvanalyze alloc ./trace.nettrace --group-by module --from 1000 --to 2000
```

Use for top allocating types, LOH detection, and namespace or module-level allocation summaries.

## DATAS

Collect .NET 9+ DATAS signals:

```bash
DOTNET_GCDynamicAdaptationMode=1 dotnet-trace collect -p <PID> --providers "Microsoft-Windows-DotNETRuntime:0x4C14FCCBD:5"
```

Analyze:

```bash
pvanalyze datas ./trace.nettrace
pvanalyze datas ./trace.nettrace --changes-only
pvanalyze datas ./trace.nettrace --samples
pvanalyze datas ./trace.nettrace --gen2
pvanalyze datas ./trace.nettrace --changes-only --format json
```

Use for server GC heap count, gen0 budget, tuning decisions, and gen2 full-GC backstop events.

## Events

```bash
pvanalyze events ./trace.nettrace --list
pvanalyze events ./trace.nettrace --list --format json
pvanalyze events ./trace.nettrace --type GCStart
pvanalyze events ./trace.nettrace --provider DotNETRuntime --limit 50
pvanalyze events ./trace.nettrace --pid 1234
pvanalyze events ./trace.nettrace --tid 5678
pvanalyze events ./trace.nettrace --payload "ConnectionReset"
pvanalyze events ./trace.nettrace --from 500 --to 1000 --type GC
```

Use event listing before guessing exact event names.

## Exceptions

```bash
pvanalyze exceptions ./trace.nettrace
pvanalyze exceptions ./trace.nettrace --type NullReference
pvanalyze exceptions ./trace.nettrace --limit 20 --format json
pvanalyze exceptions ./trace.nettrace --from 1000 --to 2000
```

Use for thrown exception count and detail. This does not replace crash dump analysis.

## Timeline And Snapshot

```bash
pvanalyze timeline ./trace.nettrace
pvanalyze timeline ./trace.nettrace --lanes gc,cpu,exceptions,alloc,jit,events --buckets 80 --format json
pvanalyze snapshot ./trace.nettrace --at 1500 --window 100
pvanalyze snapshot ./trace.nettrace --at 1500 --window 250 --format json
```

Use these commands when an agent needs a compact cross-signal view for a time range or timestamp.

## Cache Cleanup

```bash
pvanalyze clean ./trace.nettrace
pvanalyze clean ./traces/
```

Use after analysis when `.pvanalyze.etlx` cache files should not stay beside trace artifacts.
