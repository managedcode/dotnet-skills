# .NET Profiling Patterns Reference

This reference covers common profiling patterns for CPU, memory, GC, and performance analysis in .NET applications.

---

## CPU Profiling Patterns

### Pattern: CPU Hotspot Analysis

**When to use**: Application is slow or using excessive CPU.

**Approach**:
1. Ensure the application is running under realistic load
2. Collect a CPU sampling trace
3. Analyze the flame graph for time-consuming methods
4. Focus on your code, not framework internals

```bash
# Collect CPU sampling trace
dotnet-trace collect --process-id <PID> --profile cpu-sampling --duration 00:00:30

# Convert for visualization
dotnet-trace convert trace.nettrace --format Speedscope
```

**Analysis checklist**:
- [ ] Identify top 5 methods by inclusive time
- [ ] Look for unexpected methods in hot paths
- [ ] Check for string operations in loops
- [ ] Check for repeated allocations causing GC
- [ ] Check for synchronous I/O blocking threads

### Pattern: Method-Level Timing

**When to use**: Need precise timing for specific operations.

**Approach**:
1. Add `System.Diagnostics.Activity` or `Stopwatch` instrumentation
2. Use EventCounters for custom metrics
3. Collect with dotnet-counters

```csharp
// Custom EventCounter for method timing
public class OperationMetrics : EventSource
{
    public static readonly OperationMetrics Instance = new();

    private readonly IncrementingEventCounter _operationCount;
    private readonly EventCounter _operationDuration;

    public OperationMetrics()
    {
        _operationCount = new IncrementingEventCounter("operation-count", this);
        _operationDuration = new EventCounter("operation-duration-ms", this);
    }

    public void RecordOperation(double durationMs)
    {
        _operationCount.Increment();
        _operationDuration.WriteMetric(durationMs);
    }
}
```

```bash
# Monitor custom counters
dotnet-counters monitor --process-id <PID> \
  --counters "MyApp.OperationMetrics[operation-count,operation-duration-ms]"
```

### Pattern: Thread Pool Starvation Detection

**When to use**: Application becomes unresponsive under load.

**Symptoms**:
- High thread pool queue length
- Increasing response times
- Thread count growing continuously

```bash
# Monitor thread pool health
dotnet-counters monitor --process-id <PID> \
  --counters "System.Runtime[threadpool-thread-count,threadpool-queue-length,threadpool-completed-items-count]"
```

**Thresholds to watch**:
- `threadpool-queue-length` > 0 sustained indicates starvation
- `threadpool-thread-count` growing continuously indicates blocking calls
- High `threadpool-completed-items-count` with high queue length indicates throughput issues

**Common causes**:
- Sync-over-async (calling `.Result` or `.Wait()`)
- Blocking I/O on thread pool threads
- Long-running synchronous work on thread pool
- Too many concurrent operations

---

## Memory Profiling Patterns

### Pattern: Memory Leak Detection

**When to use**: Memory grows continuously over time.

**Approach**:
1. Establish baseline memory usage
2. Run under load for extended period
3. Take periodic GC dumps
4. Compare heap snapshots

```bash
# Baseline snapshot
dotnet-gcdump collect --process-id <PID> --output baseline.gcdump

# After load test
dotnet-gcdump collect --process-id <PID> --output afterload.gcdump

# Generate reports
dotnet-gcdump report baseline.gcdump > baseline-report.txt
dotnet-gcdump report afterload.gcdump > afterload-report.txt
```

**Analysis steps**:
1. Compare type counts between snapshots
2. Look for types with growing instance counts
3. Identify retained object graphs
4. Check for event handler subscriptions
5. Check for static collections

### Pattern: Large Object Heap (LOH) Analysis

**When to use**: GC pauses are long or memory fragmentation suspected.

**Symptoms**:
- Gen 2 GC frequency increasing
- Long GC pause times
- High memory usage despite low live object count

```bash
# Collect GC verbose trace
dotnet-trace collect --process-id <PID> --profile gc-verbose --duration 00:01:00
```

**Common LOH issues**:
- Arrays > 85,000 bytes allocated frequently
- String concatenation creating large strings
- Large buffers not pooled
- Byte arrays from serialization

**Mitigations**:
```csharp
// Use ArrayPool for large buffers
var buffer = ArrayPool<byte>.Shared.Rent(100000);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// Use string pooling
var pooledString = string.Intern(frequentlyUsedString);

// Use Span<T> to avoid allocations
ReadOnlySpan<byte> span = stackalloc byte[256];
```

### Pattern: Allocation Rate Analysis

**When to use**: GC running too frequently.

```bash
# Monitor allocation rate
dotnet-counters monitor --process-id <PID> \
  --counters "System.Runtime[alloc-rate,gc-heap-size,gen-0-gc-count,gen-1-gc-count,gen-2-gc-count]"
```

**Thresholds to watch**:
- `alloc-rate` > 100 MB/s may cause GC pressure
- Frequent Gen 0 collections (> 10/sec) indicate high allocation
- Gen 2 collections > 1/min may indicate LOH issues or leaks

**Allocation hotspot trace**:
```bash
# Collect allocation events
dotnet-trace collect --process-id <PID> \
  --providers "Microsoft-Windows-DotNETRuntime:0x80000:4"
```

---

## GC Analysis Patterns

### Pattern: GC Pause Analysis

**When to use**: Application has latency spikes correlated with GC.

```bash
# Collect detailed GC events
dotnet-trace collect --process-id <PID> \
  --profile gc-verbose \
  --duration 00:02:00
```

**Key metrics to extract**:
- GC pause duration per generation
- GC frequency per generation
- Promoted bytes per collection
- Fragmentation percentage

### Pattern: GC Mode Selection

**Server GC vs Workstation GC**:

| Characteristic | Server GC | Workstation GC |
|---------------|-----------|----------------|
| Threads | One per CPU core | Single thread |
| Heap | Segmented per core | Single heap |
| Latency | Higher pause times | Lower pause times |
| Throughput | Higher throughput | Lower throughput |
| Memory | Higher memory usage | Lower memory usage |
| Best for | Server workloads | Desktop apps |

```xml
<!-- Enable server GC in .csproj -->
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
</PropertyGroup>
```

```json
// Or in runtimeconfig.json
{
  "runtimeOptions": {
    "configProperties": {
      "System.GC.Server": true
    }
  }
}
```

### Pattern: Concurrent vs Background GC Analysis

```bash
# Monitor GC behavior
dotnet-counters monitor --process-id <PID> \
  --counters "System.Runtime[gc-heap-size,gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,time-in-gc]"
```

**time-in-gc interpretation**:
- < 5%: Healthy
- 5-10%: Monitor closely
- > 10%: GC tuning needed

### Pattern: Pinned Object Analysis

**When to use**: Suspected fragmentation from pinned objects.

```bash
# Analyze pinned objects in dump
dotnet-dump analyze mydump.dmp
> gcheapstat
> dumpheap -stat
```

**Common pinning issues**:
- Interop callbacks holding managed references
- Async I/O buffers
- Native code integration

---

## Contention Profiling Patterns

### Pattern: Lock Contention Analysis

**When to use**: Application throughput limited by synchronization.

```bash
# Collect contention events
dotnet-trace collect --process-id <PID> \
  --providers "Microsoft-Windows-DotNETRuntime:0x4000:4"
```

**Analysis steps**:
1. Identify frequently contended locks
2. Analyze lock hold times
3. Look for lock ordering issues
4. Check for unnecessary locking

### Pattern: Thread Synchronization Overhead

```bash
# Monitor thread-related counters
dotnet-counters monitor --process-id <PID> \
  --counters "System.Runtime[monitor-lock-contention-count,threadpool-thread-count]"
```

**High contention indicators**:
- `monitor-lock-contention-count` growing rapidly
- Thread count higher than expected
- CPU usage lower than expected under load

**Mitigations**:
```csharp
// Use lock-free collections
var dict = new ConcurrentDictionary<string, int>();

// Use reader-writer locks for read-heavy workloads
var rwLock = new ReaderWriterLockSlim();

// Use Interlocked for simple operations
Interlocked.Increment(ref counter);

// Use async semaphores
var semaphore = new SemaphoreSlim(maxConcurrency);
await semaphore.WaitAsync();
```

---

## Exception Profiling Patterns

### Pattern: Exception Rate Analysis

**When to use**: Performance impacted by exception handling.

```bash
# Monitor exception count
dotnet-counters monitor --process-id <PID> \
  --counters "System.Runtime[exception-count]"

# Collect exception events with stack traces
dotnet-trace collect --process-id <PID> \
  --providers "Microsoft-Windows-DotNETRuntime:0x8000:4"
```

**Thresholds**:
- < 10 exceptions/sec: Normal
- 10-100 exceptions/sec: Review exception usage
- > 100 exceptions/sec: Performance impact likely

**Common exception anti-patterns**:
```csharp
// Anti-pattern: Using exceptions for flow control
try
{
    value = dict[key];
}
catch (KeyNotFoundException)
{
    value = default;
}

// Better: Use TryGetValue
if (!dict.TryGetValue(key, out value))
{
    value = default;
}
```

### Pattern: First-Chance Exception Analysis

```bash
# Collect first-chance exceptions
dotnet-trace collect --process-id <PID> \
  --providers "Microsoft-Windows-DotNETRuntime:0x8000:5"
```

Analyze the trace to find:
- Exception types thrown frequently
- Exception origins (your code vs framework)
- Exception handling patterns

---

## Startup Profiling Patterns

### Pattern: Cold Start Analysis

**When to use**: Application startup is slow.

```bash
# Trace from application launch
dotnet-trace collect \
  --output startup-trace.nettrace \
  -- dotnet run --project MyApp.csproj

# Or with specific configuration
dotnet-trace collect \
  --output startup-trace.nettrace \
  -- dotnet run -c Release --project MyApp.csproj
```

**Startup phases to analyze**:
1. CLR initialization
2. Assembly loading
3. JIT compilation
4. Type initialization (static constructors)
5. Dependency injection setup
6. Application initialization

### Pattern: Assembly Loading Analysis

```bash
# Collect loader events
dotnet-trace collect --process-id <PID> \
  --providers "Microsoft-Windows-DotNETRuntime:0x8:4"
```

**Optimization strategies**:
- Use ReadyToRun (R2R) compilation
- Lazy-load optional assemblies
- Use trimming for single-file apps
- Profile and defer non-critical initialization

```xml
<!-- Enable ReadyToRun -->
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

### Pattern: JIT Compilation Analysis

```bash
# Collect JIT events
dotnet-trace collect --process-id <PID> \
  --providers "Microsoft-Windows-DotNETRuntime:0x10:4"
```

**JIT optimization strategies**:
- Use tiered compilation (default in .NET Core 3.0+)
- Pre-JIT critical paths with warmup
- Use AOT/R2R for startup-sensitive scenarios

---

## Production Profiling Patterns

### Pattern: Low-Overhead Production Monitoring

**Approach**: Use dotnet-counters for continuous, low-impact monitoring.

```bash
# Lightweight monitoring script
#!/bin/bash
while true; do
    dotnet-counters collect \
        --process-id $APP_PID \
        --counters "System.Runtime[cpu-usage,working-set,gc-heap-size,exception-count]" \
        --output metrics-$(date +%Y%m%d-%H%M%S).csv \
        --format csv \
        --duration 60
done
```

### Pattern: Triggered Diagnostics Collection

**Approach**: Collect detailed diagnostics only when anomalies detected.

```bash
# Collect trace when CPU exceeds threshold
CPU_THRESHOLD=80

while true; do
    CPU=$(dotnet-counters monitor --process-id $APP_PID \
          --counters "System.Runtime[cpu-usage]" \
          --duration 5 2>/dev/null | grep cpu-usage | awk '{print $2}')

    if (( $(echo "$CPU > $CPU_THRESHOLD" | bc -l) )); then
        dotnet-trace collect --process-id $APP_PID \
            --profile cpu-sampling \
            --duration 00:00:30 \
            --output high-cpu-$(date +%Y%m%d-%H%M%S).nettrace
    fi

    sleep 60
done
```

### Pattern: Memory Dump on OOM

```bash
# Configure automatic dump on OOM
export DOTNET_DbgEnableMiniDump=1
export DOTNET_DbgMiniDumpType=4
export DOTNET_DbgMiniDumpName=/var/dumps/oom-%p-%t.dmp

# Run application
dotnet MyApp.dll
```

---

## Comparison and Benchmarking Patterns

### Pattern: Before/After Comparison

**Approach**: Collect identical metrics before and after changes.

```bash
# Baseline collection
dotnet-counters collect --process-id $BASELINE_PID \
    --counters System.Runtime \
    --output baseline-counters.csv \
    --format csv \
    --duration 300

# After optimization
dotnet-counters collect --process-id $OPTIMIZED_PID \
    --counters System.Runtime \
    --output optimized-counters.csv \
    --format csv \
    --duration 300
```

**Comparison checklist**:
- [ ] Same workload profile
- [ ] Same duration
- [ ] Same warmup period
- [ ] Same hardware/environment
- [ ] Multiple runs for statistical significance

### Pattern: Regression Detection

**Approach**: Establish performance baselines in CI.

```bash
# CI performance check script
#!/bin/bash
set -e

# Run app with load
./start-load-test.sh &
LOAD_PID=$!

# Collect metrics
dotnet-counters collect --process-id $APP_PID \
    --counters System.Runtime \
    --output ci-metrics.csv \
    --format csv \
    --duration 60

# Stop load test
kill $LOAD_PID

# Compare against baseline (example threshold check)
ALLOC_RATE=$(grep alloc-rate ci-metrics.csv | awk -F',' '{sum+=$2; count++} END {print sum/count}')
BASELINE_ALLOC_RATE=50000000  # 50 MB/s

if (( $(echo "$ALLOC_RATE > $BASELINE_ALLOC_RATE * 1.2" | bc -l) )); then
    echo "REGRESSION: Allocation rate increased by >20%"
    exit 1
fi
```

---

## Tool Selection Guide

| Symptom | Primary Tool | Secondary Tool |
|---------|-------------|----------------|
| High CPU | dotnet-trace (cpu-sampling) | dotnet-counters |
| Memory growth | dotnet-gcdump | dotnet-dump |
| GC pauses | dotnet-trace (gc-verbose) | dotnet-counters |
| Slow startup | dotnet-trace (from launch) | - |
| Lock contention | dotnet-trace (contention) | dotnet-counters |
| Exception storms | dotnet-counters | dotnet-trace |
| Crash analysis | dotnet-dump | - |
| Live health | dotnet-counters | - |
