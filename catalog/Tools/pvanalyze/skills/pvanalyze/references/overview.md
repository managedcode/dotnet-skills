# pvanalyze Overview

## Official Sources

- GitHub repository: <https://github.com/adityamandaleeka/pvanalyze>
- Upstream README: <https://github.com/adityamandaleeka/pvanalyze/blob/main/README.md>
- Tool project: <https://github.com/adityamandaleeka/pvanalyze/blob/main/pvanalyze.csproj>
- Command sources: <https://github.com/adityamandaleeka/pvanalyze/tree/main/Commands>

## What The Tool Is

`pvanalyze` is a cross-platform command-line analyzer for .NET `.nettrace` files. It is positioned by upstream as a PerfView companion for Mac, Linux, Windows, scripting, CI, and AI-agent workflows.

The source uses `Microsoft.Diagnostics.Tracing.TraceEvent` to read trace artifacts and `System.CommandLine` for the command surface. Repeated `.nettrace` reads are cached by converting to a sibling `.pvanalyze.etlx` file.

## Install Model

The upstream README documents:

```bash
dnx pvanalyze
dotnet tool install --global pvanalyze
```

The source project is configured with:

- `TargetFramework`: `net8.0`
- `PackAsTool`: `true`
- `ToolCommandName`: `pvanalyze`
- `PackageId`: `pvanalyze`
- `VersionPrefix`: `0.1.0`

If the NuGet package is not available in the current environment, clone the repository and run from source:

```bash
git clone https://github.com/adityamandaleeka/pvanalyze
cd pvanalyze
dotnet build -c Release
dotnet run -c Release -- info ./trace.nettrace
```

## When To Choose pvanalyze

Choose `pvanalyze` when:

- there is already a `.nettrace` file
- the answer should be terminal, JSON, or SpeedScope output
- the workflow needs to run on non-Windows platforms without the PerfView GUI
- an agent needs a compact command-line trace summary

Choose lower-level official diagnostics skills when:

- the task is process discovery, live counters, dump collection, or trace provider planning
- the team needs a documented Microsoft-only toolchain
- the trace has not been collected yet and event selection is the main decision

## Trace Collection Prerequisites

General trace collection:

```bash
dotnet tool install --global dotnet-trace
dotnet-trace collect --process-id <PID> --output trace.nettrace
dotnet-trace collect -- dotnet run -c Release
```

Allocation command support requires allocation events:

```bash
dotnet-trace collect --providers "Microsoft-Windows-DotNETRuntime:0x200001:5" -- dotnet run -c Release
```

DATAS command support requires .NET 9+ DATAS events:

```bash
DOTNET_GCDynamicAdaptationMode=1 dotnet-trace collect -p <PID> --providers "Microsoft-Windows-DotNETRuntime:0x4C14FCCBD:5"
```

## Operational Notes

- `pvanalyze clean` removes `.pvanalyze.etlx`, `.pvanalyze.etlx.lock`, and temporary cache files.
- Use JSON output whenever output is handed to another automation step.
- Use `cpustacks --format speedscope --output <file>` when a flame graph viewer is needed.
- Keep the original `.nettrace` because replay cannot reconstruct events that were not collected.
