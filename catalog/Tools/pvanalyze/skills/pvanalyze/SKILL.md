---
name: pvanalyze
description: "Use `pvanalyze` to inspect existing .NET `.nettrace` files from the command line, including GC, JIT, CPU stacks, allocation, DATAS, events, exceptions, timeline, and call-tree analysis with JSON or SpeedScope. USE FOR: the user mentions pvanalyze, PerfView-style CLI trace analysis, or cross-platform .nettrace inspection; the task starts from an existing .nettrace file and needs. DO NOT USE FOR: unrelated stacks; generic tasks that do not need this specific guidance. INVOKES: inspect the repository context, edit targeted files, and run relevant build, test, lint, or validation commands when changes are made."
compatibility: "Requires .NET 8 or later to run `pvanalyze`; trace collection usually requires `dotnet-trace`. Some scenarios require traces captured with verbose runtime providers or .NET 9+ DATAS events."
---

# pvanalyze

## Trigger On

- the user mentions `pvanalyze`, PerfView-style CLI trace analysis, or cross-platform `.nettrace` inspection
- the task starts from an existing `.nettrace` file and needs readable terminal or JSON output
- an agent or CI workflow needs GC, JIT, CPU stack, allocation, event, exception, timeline, or call-tree summaries
- SpeedScope export is useful but the source artifact is a `.nettrace`

Use `dotnet-trace-collect` or `profiling` first when the task is mostly about collecting the trace. Use `pvanalyze` once a trace artifact exists or when the user wants the specific command surface.

## Workflow

1. Confirm the trace path and whether it was collected with the events needed for the question.
2. Run `pvanalyze info <trace.nettrace>` first to verify the file opens and contains the expected processes.
3. Pick one focused analysis command:
   - `gcstats` for GC count, heap size, pause, and GC timeline questions
   - `alloc` for allocation-by-type questions
   - `datas` for Dynamic Adaptation To Application Sizes heap-count tuning
   - `jitstats` for JIT compilation cost
   - `cpustacks` for top CPU methods, module grouping, namespace grouping, or SpeedScope export
   - `calltree` for hot paths and caller/callee exploration
   - `events` for provider, event type, payload, PID, or TID filtering
   - `exceptions` for thrown exception summaries and details
   - `timeline` or `snapshot` when an agent needs time-bucketed context
4. Prefer `--format json` when another tool or agent will consume the output.
5. Add `--from` and `--to` only after the baseline command confirms the interesting time window.
6. Use `pvanalyze clean <trace-or-directory>` when generated `.pvanalyze.etlx` cache files should be removed.

## Install

The upstream README documents two install paths:

```bash
# .NET 10+ one-shot execution path
dnx pvanalyze

# Global tool path
dotnet tool install --global pvanalyze
```

If the NuGet package is not resolvable in the current environment, build or pack from source:

```bash
git clone https://github.com/adityamandaleeka/pvanalyze
cd pvanalyze
dotnet build -c Release
dotnet run -c Release -- info ./trace.nettrace
```

The source project is configured as a .NET tool with `PackageId` `pvanalyze`, `ToolCommandName` `pvanalyze`, and `VersionPrefix` `0.1.0`.

Install trace collection support when you need to create the input artifact:

```bash
dotnet tool install --global dotnet-trace
dotnet-trace --version
pvanalyze --help
```

## Practical Usage

### Read trace metadata and GC signal

```bash
pvanalyze info ./trace.nettrace
pvanalyze gcstats ./trace.nettrace --format json
pvanalyze gcstats ./trace.nettrace --timeline --longest 5
pvanalyze gcstats ./trace.nettrace --from 1000 --to 2000 --timeline --format json
```

### Write CPU outputs for review

```bash
pvanalyze cpustacks ./trace.nettrace --top 20
pvanalyze cpustacks ./trace.nettrace --group-by module --top 10 --format json
pvanalyze cpustacks ./trace.nettrace --format speedscope --output ./trace.speedscope.json
```

### Drill into events, exceptions, and call trees

```bash
pvanalyze events ./trace.nettrace --list --format json
pvanalyze events ./trace.nettrace --provider DotNETRuntime --type GCStart --limit 50
pvanalyze exceptions ./trace.nettrace --type NullReference --format json
pvanalyze calltree ./trace.nettrace --hot-path --depth 5 --format json
pvanalyze calltree ./trace.nettrace --caller-callee "Serialize"
```

### Collect traces with required event detail

General trace:

```bash
dotnet-trace collect --process-id <PID> --output ./trace.nettrace
dotnet-trace collect -- dotnet run -c Release
```

Allocation analysis requires allocation events:

```bash
dotnet-trace collect --providers "Microsoft-Windows-DotNETRuntime:0x200001:5" -- dotnet run -c Release
pvanalyze alloc ./trace.nettrace --group-by type --top 20 --format json
```

DATAS analysis requires .NET 9+ DATAS events and verbose GC runtime events:

```bash
DOTNET_GCDynamicAdaptationMode=1 dotnet-trace collect -p <PID> --providers "Microsoft-Windows-DotNETRuntime:0x4C14FCCBD:5"
pvanalyze datas ./trace.nettrace --changes-only --format json
```

## Option Patterns

- Use `--process <name>` on `gcstats`, `jitstats`, `alloc`, or `datas` when a trace includes multiple .NET processes.
- Use `--from <ms>` and `--to <ms>` for GC, CPU, allocation, event, exception, timeline, and call-tree time windows.
- Use `--format json` for automation and `--format text` for human terminal review.
- Use `cpustacks --group-by method|module|namespace` and `--inclusive` to change CPU aggregation.
- Use `calltree --hot-path`, `--caller-callee <method>`, `--depth <n>`, and `--min-percent <n>` to keep stack output focused.
- Use `timeline --lanes gc,cpu,exceptions,alloc,jit,events --buckets <n>` for a compact multi-signal view.
- Use `snapshot --at <ms> --window <ms>` when the question is "what was happening around this timestamp?"

## Constraints

- `pvanalyze` analyzes trace artifacts; it does not replace `dotnet-trace` for collection.
- CPU stacks, allocations, DATAS, exceptions, and event filters only work when the trace contains the required events.
- The tool converts `.nettrace` to a sibling `.pvanalyze.etlx` cache for repeated reads; this is useful but can surprise clean working directories.
- SpeedScope output comes from `cpustacks`; use SpeedScope or another viewer for interactive flame graph inspection.
- The source currently targets `net8.0`; the README also documents `dnx pvanalyze` as a .NET 10+ path.
- If the global tool package is not available yet, use the source build path and record that in the investigation notes.

## Deliver

- the exact `dotnet-trace collect` command used or needed to capture the right events
- the focused `pvanalyze` command and output format
- the relevant process, time window, provider, event type, or method filter
- any generated files such as `.speedscope.json` or `.pvanalyze.etlx` cache paths that matter for follow-up

## Validate

- `pvanalyze --help` or `dotnet run -c Release -- --help` succeeds
- `dotnet-trace --version` succeeds when collection is part of the workflow
- `pvanalyze info <trace.nettrace>` reads the trace before deeper analysis begins
- JSON output parses when `--format json` is used
- `pvanalyze clean <trace-or-directory>` removes cache files when cache cleanup is required

## References

- [overview.md](references/overview.md) - source links, installation model, and tool positioning
- [commands.md](references/commands.md) - command matrix, collection recipes, and option selection
