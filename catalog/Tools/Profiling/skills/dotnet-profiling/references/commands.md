# .NET Profiling CLI Commands Reference

This reference covers the official .NET diagnostics CLI tools for profiling and runtime investigation.

## Tool Installation

```bash
# Install all profiling tools globally
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-dump
dotnet tool install --global dotnet-gcdump

# Verify installations
dotnet-counters --version
dotnet-trace --version
dotnet-dump --version
dotnet-gcdump --version
```

---

## dotnet-counters

Real-time monitoring of .NET runtime metrics and custom counters.

### List Available Counters

```bash
# List all well-known counters
dotnet-counters list

# List counters for a specific process
dotnet-counters list --process-id <PID>
```

### Monitor Live Counters

```bash
# Monitor System.Runtime counters
dotnet-counters monitor --process-id <PID> --counters System.Runtime

# Monitor with custom refresh interval (in seconds)
dotnet-counters monitor --process-id <PID> --refresh-interval 2

# Monitor multiple counter providers
dotnet-counters monitor --process-id <PID> \
  --counters System.Runtime,Microsoft.AspNetCore.Hosting

# Monitor specific counters from a provider
dotnet-counters monitor --process-id <PID> \
  --counters "System.Runtime[cpu-usage,working-set,gc-heap-size]"

# Monitor by process name (attach to first match)
dotnet-counters monitor --name MyApp --counters System.Runtime
```

### Collect Counters to File

```bash
# Collect counters to CSV
dotnet-counters collect --process-id <PID> \
  --counters System.Runtime \
  --output counters.csv \
  --format csv

# Collect counters to JSON
dotnet-counters collect --process-id <PID> \
  --counters System.Runtime \
  --output counters.json \
  --format json

# Collect for a specific duration (in seconds)
dotnet-counters collect --process-id <PID> \
  --counters System.Runtime \
  --output counters.csv \
  --format csv \
  --duration 60
```

### Common Counter Providers

| Provider | Description |
|----------|-------------|
| `System.Runtime` | GC, thread pool, exception, and general runtime metrics |
| `Microsoft.AspNetCore.Hosting` | ASP.NET Core request metrics |
| `Microsoft.AspNetCore.Http.Connections` | SignalR connection metrics |
| `System.Net.Http` | HTTP client metrics |
| `System.Net.Sockets` | Socket-level metrics |
| `Microsoft.EntityFrameworkCore` | EF Core query and save metrics |

### Key System.Runtime Counters

| Counter | Description |
|---------|-------------|
| `cpu-usage` | CPU usage percentage |
| `working-set` | Working set memory in MB |
| `gc-heap-size` | GC heap size in MB |
| `gen-0-gc-count` | Generation 0 GC count |
| `gen-1-gc-count` | Generation 1 GC count |
| `gen-2-gc-count` | Generation 2 GC count |
| `threadpool-thread-count` | Thread pool thread count |
| `threadpool-queue-length` | Thread pool work item queue length |
| `exception-count` | Number of exceptions thrown |
| `alloc-rate` | Allocation rate in bytes per second |

---

## dotnet-trace

Collect detailed performance traces for CPU profiling, events, and diagnostics.

### List Running .NET Processes

```bash
dotnet-trace ps
```

### List Available Profiles

```bash
dotnet-trace list-profiles
```

### Collect Traces

```bash
# Collect with default profile (cpu-sampling)
dotnet-trace collect --process-id <PID>

# Collect with specific profile
dotnet-trace collect --process-id <PID> --profile cpu-sampling

# Collect with multiple profiles
dotnet-trace collect --process-id <PID> \
  --profile cpu-sampling \
  --profile gc-verbose

# Collect for a specific duration (in seconds)
dotnet-trace collect --process-id <PID> \
  --duration 00:00:30

# Collect with custom output path
dotnet-trace collect --process-id <PID> \
  --output mytrace.nettrace

# Collect with specific providers
dotnet-trace collect --process-id <PID> \
  --providers "Microsoft-DotNETCore-SampleProfiler,Microsoft-Windows-DotNETRuntime"

# Collect with high-frequency CPU sampling
dotnet-trace collect --process-id <PID> \
  --profile cpu-sampling \
  --clrevents gc,jit,exception,contention

# Attach to process by name
dotnet-trace collect --name MyApp
```

### Built-in Profiles

| Profile | Description |
|---------|-------------|
| `cpu-sampling` | CPU sampling for hotspot analysis |
| `gc-verbose` | Detailed GC events |
| `gc-collect` | GC collection events only |
| `none` | No predefined providers (use --providers) |

### Convert Trace Format

```bash
# Convert to SpeedScope format for web viewer
dotnet-trace convert mytrace.nettrace --format Speedscope

# Convert to Chromium format
dotnet-trace convert mytrace.nettrace --format Chromium

# Convert with custom output
dotnet-trace convert mytrace.nettrace \
  --format Speedscope \
  --output mytrace.speedscope.json
```

### Common Provider Keywords

```bash
# Detailed GC events
--providers "Microsoft-Windows-DotNETRuntime:0x1:5"

# JIT compilation events
--providers "Microsoft-Windows-DotNETRuntime:0x10:5"

# Exception events
--providers "Microsoft-Windows-DotNETRuntime:0x8000:5"

# Contention events
--providers "Microsoft-Windows-DotNETRuntime:0x4000:5"

# Thread pool events
--providers "Microsoft-Windows-DotNETRuntime:0x10000:5"

# All runtime events (verbose)
--providers "Microsoft-Windows-DotNETRuntime:0xFFFFFFFFFFFFFFFF:5"
```

---

## dotnet-dump

Capture and analyze memory dumps for debugging crashes and memory issues.

### Collect Dumps

```bash
# Collect a full memory dump
dotnet-dump collect --process-id <PID>

# Collect with specific dump type
dotnet-dump collect --process-id <PID> --type Full
dotnet-dump collect --process-id <PID> --type Heap
dotnet-dump collect --process-id <PID> --type Mini

# Collect with custom output path
dotnet-dump collect --process-id <PID> \
  --output mydump.dmp

# Collect from process by name
dotnet-dump collect --name MyApp
```

### Dump Types

| Type | Description |
|------|-------------|
| `Full` | Complete process memory (largest) |
| `Heap` | GC heap and type information |
| `Mini` | Minimal dump with stack traces |

### Analyze Dumps

```bash
# Start interactive analysis
dotnet-dump analyze mydump.dmp
```

### Analysis Commands (Interactive)

```bash
# Show all managed threads
clrthreads

# Show call stacks for all threads
clrstack -all

# Show call stack for current thread
clrstack

# Show exceptions on all threads
pe -all

# Dump heap statistics
dumpheap -stat

# Dump heap by type
dumpheap -type System.String

# Dump specific object
dumpobj <address>

# Find GC roots for an object
gcroot <address>

# Dump method table
dumpmt <address>

# Dump module information
dumpmodule <address>

# Show GC heap information
gcheapstat

# Show finalizer queue
finalizequeue

# Show sync blocks (locks)
syncblk

# Exit analysis
exit
```

### Scripted Analysis

```bash
# Run commands from a script
dotnet-dump analyze mydump.dmp --command "clrthreads" --command "dumpheap -stat"

# Output to file
dotnet-dump analyze mydump.dmp --command "dumpheap -stat" > heap-stats.txt
```

---

## dotnet-gcdump

Capture GC heap snapshots for memory analysis without full dumps.

### Collect GC Dumps

```bash
# Collect GC dump
dotnet-gcdump collect --process-id <PID>

# Collect with custom output
dotnet-gcdump collect --process-id <PID> \
  --output myheap.gcdump

# Collect with timeout (in seconds)
dotnet-gcdump collect --process-id <PID> \
  --timeout 60

# Collect by process name
dotnet-gcdump collect --name MyApp
```

### Generate Heap Reports

```bash
# Generate report to console
dotnet-gcdump report myheap.gcdump

# Generate report with specific type filter
dotnet-gcdump report myheap.gcdump --type System.String
```

### Important Notes

- GC dumps cause a GC and briefly pause the process
- Smaller than full memory dumps
- Captures type information and reference graphs
- Open `.gcdump` files in Visual Studio or PerfView for analysis

---

## Process Discovery

### Find .NET Process IDs

```bash
# List all .NET processes with dotnet-trace
dotnet-trace ps

# List all .NET processes with dotnet-counters
dotnet-counters ps

# Platform-specific alternatives
# Linux/macOS
ps aux | grep dotnet

# Windows PowerShell
Get-Process | Where-Object { $_.ProcessName -like "*dotnet*" }
```

---

## Environment Variables for Diagnostics

```bash
# Enable diagnostic port (for sidecar collection)
export DOTNET_DiagnosticPorts=/tmp/diag.sock

# Enable startup diagnostics
export DOTNET_StartupHooks=/path/to/hook.dll

# Force GC server mode
export DOTNET_gcServer=1

# Enable GC stress testing
export DOTNET_GCStress=0x3

# Enable ETW events on Windows
export DOTNET_PerfMapEnabled=1

# Enable perf maps on Linux
export DOTNET_EnableEventLog=1
```

---

## CI/CD Integration

### Capture Startup Trace

```bash
# Start app with trace collection from launch
dotnet-trace collect \
  --output startup-trace.nettrace \
  -- dotnet run --project MyApp.csproj
```

### Automated Counter Collection

```bash
#!/bin/bash
# Collect counters during load test
APP_PID=$(dotnet-trace ps | grep MyApp | awk '{print $1}')

dotnet-counters collect \
  --process-id $APP_PID \
  --counters System.Runtime,Microsoft.AspNetCore.Hosting \
  --output load-test-counters.csv \
  --format csv \
  --duration 300
```

### Automated Dump on Crash

```bash
# Configure automatic dump collection
export DOTNET_DbgEnableMiniDump=1
export DOTNET_DbgMiniDumpType=4
export DOTNET_DbgMiniDumpName=/tmp/coredump.%p

# Run application
dotnet run
```
