---
name: blazing-fast-dotnet
description: High-performance .NET guidance for writing and tuning C# apps and benchmarks (BenchmarkDotNet, ASP.NET, challenges). Use when asked to make .NET code faster, reduce allocations or GC overhead, fix lock contention or thread pool starvation, inspect JIT codegen, profile apps, or tune runtime/NativeAOT settings.
---

# Blazing Fast .NET Performance

> [!WARNING]
> Much of the advice here might not age well: as .NET evolves, old hacks and custom knobs may become obsolete or even detrimental. Apply only after careful consideration and testing. Revisit periodically to ensure continued relevance.

This skill focuses on optimizing .NET applications for maximum performance, providing tips, techniques, and best practices to achieve blazing-fast execution times. It targets .NET 10.0 and 11.0. Older versions have not been tested.

**Contents**

- [Profiling and Performance Analysis](#profiling-and-performance-analysis)
  - [dotnet-counters](#dotnet-counters)
  - [dotnet-trace](#dotnet-trace)
  - [Low-level profiling with Linux `perf`](#low-level-profiling-with-linux-perf)
  - [Popular low-level problems](#popular-low-level-problems)
  - [Inspecting JIT codegen](#inspecting-jit-codegen)
- [Best practices for high-performance C# code](#best-practices-for-high-performance-c-code)
  - [Quick tips](#quick-tips)
  - [Strings and globalization](#strings-and-globalization)
  - [Thread pool starvation](#thread-pool-starvation)
  - [Contention on shared state](#contention-on-shared-state)
    - [False sharing](#false-sharing)
  - [Safe vectorization (SIMD or SWAR)](#safe-vectorization-simd-or-swar)
  - [Generic specialization via static abstract members](#generic-specialization-via-static-abstract-members)
- [ASP.NET Core](#aspnet-core)
  - [Hosting and logging](#hosting-and-logging)
  - [JSON and response writing](#json-and-response-writing)
  - [Routing and middleware](#routing-and-middleware)
  - [Kestrel socket transport](#kestrel-socket-transport)
  - [Beyond benchmarks](#beyond-benchmarks)
- [Magic knobs to Tune .NET Performance](#magic-knobs-to-tune-net-performance)
- [NativeAOT-specific tuning](#nativeaot-specific-tuning)
- [General notes](#general-notes)

# Profiling and Performance Analysis

Start cheap and go deeper only when needed: counters (what kind of bottleneck?) → traces (where?) → `perf` and JIT disassembly.
Install the tools with:

```sh
dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-trace
dotnet tool install -g dotnet-stack
dotnet tool install -g dotnet-symbol
```

## dotnet-counters

[dotnet-counters](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters) is a low-overhead, first-level triage tool. Run it while the app is under load:

```sh
dotnet-counters monitor -p <pid>                                    # System.Runtime counters by default
dotnet-counters monitor -p <pid> --counters System.Runtime,Microsoft.AspNetCore.Hosting,System.Net.Http
dotnet-counters collect -p <pid> --format csv -o counters.csv      # save for post-processing
```

See the `System.Runtime` [built-in metrics](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-runtime) (.NET 9+) for what each counter means. Typical readings:

| Symptom | Counters | Next step |
| --- | --- | --- |
| Low CPU, rising latency | `dotnet.thread_pool.queue.length` grows, `dotnet.thread_pool.thread.count` keeps creeping up | [Thread pool starvation](#thread-pool-starvation) |
| Throughput doesn't scale with cores | `dotnet.monitor.lock_contentions` rate | `dotnet-trace --clrevents contention`, [Contention on shared state](#contention-on-shared-state) |
| High CPU, much of it in GC | `dotnet.gc.pause.time`, `dotnet.gc.collections` (gen2 share), `dotnet.gc.heap.total_allocated` rate | [Allocations and GC](#allocations-and-gc) |
| Unexplained slow requests | `dotnet.exceptions` rate | `dotnet-trace --clrevents exception` |
| Numbers still drifting | `dotnet.jit.compiled_methods` still growing | Not at steady state yet: warm up longer |

## dotnet-trace

[dotnet-trace](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace) collects EventPipe traces on any OS without root or a native profiler (managed frames only):

```sh
dotnet-trace collect -p <pid> --duration 00:00:00:10    # default profiles: dotnet-common + dotnet-sampled-thread-time
dotnet-trace collect -p <pid> --profile gc-verbose      # GCs + sampled allocations: which types/stacks allocate
dotnet-trace collect -p <pid> --clrevents contention    # lock contention events with stacks
dotnet-trace collect -p <pid> --providers Microsoft-Windows-DotNETRuntime:0x80000000000:4  # .NET 10+: randomized allocation sampling, cheaper than gc-verbose
dotnet-trace collect -- ./MyApp arg1                    # trace from startup
dotnet-trace list-profiles

dotnet-trace report trace.nettrace topN -n 20           # top methods (exclusive) as plain text
dotnet-trace report trace.nettrace topN -n 20 --inclusive
dotnet-trace convert trace.nettrace --format Speedscope # open in https://www.speedscope.app
```

On Linux with kernel 6.4+ and root, `dotnet-trace collect-linux` (.NET 10+) records native, kernel and managed frames in one trace without the `perf` setup below.

For more tips on using `dotnet-trace`, refer to the [dotnet-trace collection skill](https://raw.githubusercontent.com/dotnet/skills/refs/heads/main/plugins/dotnet-diag/skills/dotnet-trace-collect/SKILL.md).

## Low-level profiling with Linux `perf`

The instructions below are mostly for CoreCLR/JIT scenarios on Linux with the native `perf` tool. NativeAOT/R2R scenarios should presumably work as is, without `perf inject`.

Typically, the following environment variables need to be set in the context of the running application:

```sh
DOTNET_PerfMapEnabled=1
DOTNET_PerfMapStubGranularity=3
DOTNET_JitFramed=1
DOTNET_EnableWriteXorExecute=0
```

`DOTNET_PerfMapEnabled` has an overhead of its own: set it for the profiling run only, not for the runs you take throughput numbers from.

What to profile depends on the scenario. Typically it's steady-state performance, so wait until the process is properly warmed up (e.g. 1 minute), then attach and collect traces:

```sh
perf record -k 1 -g -F 3999 -p $PROCESS_PID -o perf.data -- sleep 5
```
Next, symbolize the collected JIT frames using `perf inject`:

```sh
perf inject --input perf.data --jit --output perfjit.data
```

Then use the resulting `perfjit.data` file as needed, e.g. to generate flame graphs or annotated asm:

```sh
# annotated asm & report:
perf report --input perfjit.data --no-children --percent-limit 1 --stdio > perf_report.txt
perf annotate --stdio2 -i perfjit.data --percent-limit 1 -M intel > perf.asm

# Generate flamegraph (svg)
git clone --depth 1 --quiet https://github.com/brendangregg/FlameGraph || true
perf script -i perfjit.data | ./FlameGraph/stackcollapse-perf.pl | ./FlameGraph/flamegraph.pl > flamegraph.svg
```

Now `perf.asm` contains the annotated assembly output for the hottest methods in your application. Keep in mind that sample attribution is imprecise ("skid"): the cost is often reported on an instruction shortly after the one that actually stalls (e.g. right after a slow load or division), so inspect the neighboring instructions too.

In case of missing symbols you may need to use `dotnet-symbol` to download the necessary debug symbols for .dll/.dylib/.so:

```sh
dotnet-symbol --symbols $PUBLISH_FOLDER/*.dll
dotnet-symbol --symbols $PUBLISH_FOLDER/*.so
```

All other `perf`-related techniques (e.g. c2c, branch profiling, cache misses) can be applied similarly, depending on the performance aspects you want to analyze.

For simple functions, create a self-contained console application that calls the function in an infinite loop. For BenchmarkDotNet, use the in-process toolchain (`-i` or `[InProcess]`) with a long iteration duration and a very large iteration count so it is clear which process to attach `perf` to.

## Popular low-level problems

- Cache contention and false sharing in multi-threaded code. Fix with per-thread data and padding (see [False sharing](#false-sharing)).
- Bounds checks not being elided by the JIT.
- Store-to-load forwarding stalls, e.g. writing a struct field by field and reading it back with a wider load.
- Bad data locality causing cache misses.
- Code alignment noise. Loop or branch placement, Intel JCC erratum - not something we can control, just to keep in mind when analyzing performance variations.
- GC's alignment is 8 bytes (64-bit), which might not be suitable for SIMD optimizations (e.g. AVX512 might penalize every load/store if misaligned since the cache line is 64 bytes).

## Inspecting JIT codegen

These are diagnostic switches (not tuning knobs) and work with the shipped Release runtime:

```sh
DOTNET_JitDisasm="Test"            # method list: 'Test', 'Program:Test', '*Test*', 'Program:*' (space-separated)
DOTNET_JitDisasmSummary=1          # list every jitted method with its tier
DOTNET_JitStdOutFile=/tmp/jit.txt  # write JIT output to a file instead of stdout
DOTNET_JitDisasmDiffable=1         # stable output (no addresses) for before/after diffs
DOTNET_JitDisasmOnlyOptimized=1    # show only Tier-1/FullOpts code
```
^ might require `MethodImplOptions.NoInlining` on the method to ensure it gets JIT-compiled and not inlined away.

> [!TIP]
> Set them only for the app process, not for `dotnet run`, which would also dump the SDK's own methods.

- A method is compiled several times as it tiers up. Judge the last version: header `; Tier1 code` (or `; Tier1-OSR code` / `; FullOpts code`), ideally with `; optimized using Dynamic PGO`. `; Tier0 code` and `; Instrumented Tier0 code` are unoptimized.
- BCL methods precompiled with ReadyToRun show up only once they get hot and are re-jitted at Tier1.
- NativeAOT: pass the same options to ILC, e.g. `<IlcArg Include="--codegenopt:JitDisasm=Test" />` and `<IlcArg Include="--codegenopt:JitStdOutFile=jit.txt" />`.
- Inlining decisions: `DOTNET_JitPrintInlinedMethods` needs a Checked JIT. On the Release runtime, collect the `MethodJitInliningSucceeded`/`MethodJitInliningFailed` events (with the reason) via `dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0x1000:5`.

# Best practices for high-performance C# code

> [!IMPORTANT]
> Only safe C# code is considered in these best practices. Unsafe code is highly discouraged and not covered here.

## Quick tips

- Use [source-generated System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation): pass generated `JsonTypeInfo<T>` to avoid reflection-based metadata discovery and enable supported serialization fast paths. Preserve existing serialization options and behavior.
- Use [`[LoggerMessage]`](https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation) source-generated logging to avoid template parsing, boxing, and temporary allocations. Still guard expensive argument computation with `IsEnabled`.
- Use [`[GeneratedRegex]`](https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators) for compile-time-known patterns instead of runtime pattern compilation. Preserve options, culture, and match timeouts.
- Prefer optimized bulk APIs over handwritten loops: `Span.CopyTo`, `Clear`, `Fill`, `SequenceEqual`, and `IndexOf` already have specialized implementations, including SIMD where appropriate.
- Use RVA-backed spans `static ReadOnlySpan<long> RVA => [1, 2, 3];` for immutable data (only primitives, e.g., `long`, `byte`, `char`) - the data will be embedded into metadata, and the JIT will be able to perform constant folding on it.
- Prefer static lambdas and state-passing overloads in hot paths to avoid capturing closures.
- Parse and format UTF-8 directly: numeric `TryParse(ReadOnlySpan<byte>, ...)` and `TryFormat(Span<byte>, ...)` avoid intermediate strings and UTF-8/UTF-16 transcoding. Preserve culture and formatting semantics.
- Cache `SearchValues<char>` / `SearchValues<byte>` for repeated searches against fixed sets; use `SearchValues<string>` for multi-substring searches. Construction has a cost, so do not create these per call.
- Consider `FrozenDictionary` / `FrozenSet` for build-once, read-many data. Their construction cost must be amortized over enough lookups; preserve the intended comparer.
- Use dictionary alternate lookups from spans: `dictionary.GetAlternateLookup<ReadOnlySpan<char>>()` avoids allocating temporary string keys when the comparer supports it. Use `TryGetAlternateLookup` when comparer support is not known.
- Implement `IEquatable<T>` and a good `GetHashCode` for struct dictionary/set keys. This can avoid expensive default equality paths, boxing, and collision-heavy hashes.
- Try [`TensorPrimitives`](https://learn.microsoft.com/dotnet/api/system.numerics.tensors.tensorprimitives) before hand-vectorizing numeric operations. Its span-based implementations handle SIMD and tails; prefer it over manual SIMD loops or LINQ for things like finding min/max, sums, etc.
- Prefer [`[LibraryImport]`](https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke-source-generation) over `[DllImport]` for supported signatures to generate marshalling at build time; both `DllImport` and `LibraryImport` require `AllowUnsafeBlocks=true` under unsafe-v2, but if interop is not avoidable, `LibraryImport` is generally preferred.
- Use `static readonly` flags for genuinely immutable process-wide configuration. Optimized JIT code can fold their values and eliminate disabled branches.
- Cache fields in locals in hot loops (`var items = _items;`). The JIT often can't do it for you: calls and stores through references may modify the field, and the memory model restricts hoisting reads. Locals also enable bounds check elimination for arrays/spans.
- Consider `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` for UTF-8 JSON-only responses ([documentation](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/character-encoding#serialize-all-characters)). Never embed its raw output in HTML or `<script>`; any custom encoder disables STJ's generated serialization fast path, so benchmark rather than assuming the gains add up.
- Create `JsonSerializerOptions` once and reuse it (CA1869). A new instance with the same settings still pays a global cache lookup; one that also creates converter instances (`Converters = { new JsonStringEnumConverter() }`) misses that cache, since converters compare by reference, and rebuilds metadata for every type it touches on each call.
- For most bit/byte manipulations, prefer the built-in `BitOperations`, `BinaryPrimitives`, `BitConverter` or static numeric helpers such as `uint.Log2`, `uint.PopCount`, `uint.TrailingZeroCount`, etc.
- Span/ReadOnlySpan have a lot of useful extension methods via `MemoryExtensions` such as `SequenceEqual`, `IndexOf`, `IndexOfAnyExcept`, `LastIndexOf`, `StartsWith`, `CommonPrefixLength`, and a lot more - prefer these APIs over manual loops or vectorization.
- Don't pin benchmarks to a single core - doing so might significantly affect internal VM heuristics (e.g. tiered compilation timing).
- For BenchmarkDotNet methodology, see the [dotnet/runtime microbenchmark skill](https://github.com/dotnet/runtime/blob/main/.github/skills/microbenchmark/SKILL.md).
- `AggressiveOptimization` is usually a deoptimization.
- Overusing `AggressiveInlining` may hurt performance (e.g. by exhausting the inliner's time budget or increasing the number of tracked local variables).
- Moving rarely taken paths (grow/resize, slow fallback) into a separate method marked with  `[MethodImpl(MethodImplOptions.NoInlining)]` may improve performance (as `List<T>.Add` does with `AddWithResize`), but it may also hinder escape analysis.
- Don't put too much data into TLS (Thread-Local Storage) - excessive use can lead to increased memory usage and cache pressure, reducing overall performance (there can be a lot of threads, each with its own copy).
- Consider `CollectionsMarshal.AsSpan(list)` for `List<T>` (there is no `List<T>.AsSpan()`), or `.AsSpan()` on collections that support it, to avoid unnecessary overhead from bound and covariance checks, and to enable span-based algorithms directly on the collection's underlying memory. Don't add or remove list items while the span is in use.
- Prefer XXH3 via Microsoft's `System.IO.Hashing.XxHash3` for fast fingerprinting of large blobs. Use `XxHash128` when 128-bit fingerprints are needed. For exact deduplication, verify matches byte for byte. Use cryptographic hashes when security requires them. NOTE: Be careful with data that may contain garbage or uninitialized bytes (e.g. arrays of structs with padding).
- Split without allocating: `foreach (Range r in span.Split(','))` (.NET 9+) or `span.Split(destinationRanges, ',')`, instead of `string.Split` plus `Substring`.
- Use `System.Threading.Lock` (.NET 9+) rather than `object` for new `lock` targets.
- For tiny loop bodies, `Parallel.For` pays a delegate call per element. Iterate ranges instead: `Parallel.ForEach(Partitioner.Create(0, n), range => { for (int i = range.Item1; i < range.Item2; i++) ... })`.
- `Console.Out` flushes after every write. For heavy output, write through one buffered `Stream`/`StreamWriter` over `Console.OpenStandardOutput()` and flush once at the end.
- To read a large file from several threads, use `File.OpenHandle` + `RandomAccess.Read(handle, buffer, fileOffset)`: offset-based, thread-safe, no shared position and no `FileStream` buffering layer.

## Strings and globalization

- Prefer ordinal operations. Culture-sensitive comparisons go through ICU, are many times slower than ordinal ones, and are often a correctness bug anyway.
- Culture-sensitive by default: `string.Compare`, `CompareTo`, `StartsWith(string)`, `EndsWith(string)`, `IndexOf(string)`, `LastIndexOf(string)`, `ToUpper()`, `ToLower()`, and default string sorting (`Array.Sort`, `List<T>.Sort`, `OrderBy`, `SortedDictionary`/`SortedSet` with the default comparer). Pass `StringComparison.Ordinal`/`OrdinalIgnoreCase` or `StringComparer.Ordinal`; analyzers CA1307, CA1309 and CA1310 find these calls.
- Already ordinal: `==`, `Equals`, `Contains(string)`, `Replace(string, string)`, `IndexOf(char)`, `Split`, `GetHashCode`, and the default `Dictionary`/`HashSet` comparer.
- Case-insensitive equality: `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` instead of `a.ToLower() == b.ToLower()` (two allocations plus culture rules). For ASCII data, `System.Text.Ascii` has vectorized `EqualsIgnoreCase`, `ToUpper`/`ToLower` (span to span) and `IsValid`.
- `Dictionary<string, T>` / `HashSet<string>` use fast non-randomized hashing only with the default comparer, `StringComparer.Ordinal` or `StringComparer.OrdinalIgnoreCase` (falling back to randomized hashing only on heavy collisions). Any other comparer loses it.
- Stay in UTF-8 when input and output are UTF-8: search, parse and format `ReadOnlySpan<byte>` directly (`u8` literals, `SearchValues<byte>`, `TryParse`/`TryFormat` on bytes, `Utf8.TryWrite`) and convert to `string` only when needed.
- If the app needs no culture data, `<InvariantGlobalization>true</InvariantGlobalization>` removes the ICU dependency (faster startup, less memory) and makes culture-sensitive string operations ordinal. This changes behavior, so verify the output.

## Thread pool starvation

Symptoms: low CPU and rising latency while `dotnet.thread_pool.queue.length` grows and `dotnet.thread_pool.thread.count` creeps up (beyond the minimum, the pool adds threads slowly). The cause is pool threads blocked instead of running work: sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`), synchronous I/O, or locks held across slow operations.

```sh
dotnet-stack report -p <pid>   # all managed stacks: many threads parked in the same Wait/GetResult frame is the culprit
dotnet-trace collect -p <pid> --providers Microsoft-Windows-DotNETRuntime:0x40000000000:5  # .NET 9+: wait events with stacks
```

The fix is to make the blocked path async end to end; wrapping it in `Task.Run` only moves the blocked thread. As a stopgap, raise the minimum worker count with `ThreadPool.SetMinThreads` or `DOTNET_ThreadPool_ForceMinWorkerThreads` (see the knobs table).

## Contention on shared state

Writes to one shared location from many threads (`Interlocked.Increment` on a global counter, a shared "total" field, etc.) serialize all of them on a single cache line, so throughput can get *worse* as cores are added. Split the state, update only the local part, and combine on read. Two options:

**1. Per-core stripes** - index an array by `Thread.GetCurrentProcessorId()` (cheap: cached and periodically refreshed). The id is only a hint: threads migrate, and ids can exceed `ProcessorCount` (hence the modulo), so keep `Interlocked` on the stripe - it's almost always uncontended. Fixed memory and allocation-free reads; works with any number of threads (thread pool, request handlers). Stripes must be padded, see [False sharing](#false-sharing).

```cs
sealed class StripedCounter
{
    private readonly PaddedLong[] _stripes = new PaddedLong[Environment.ProcessorCount];

    public void Increment()
    {
        PaddedLong[] stripes = _stripes;
        Interlocked.Increment(ref stripes[(uint)Thread.GetCurrentProcessorId() % (uint)stripes.Length].Value);
    }

    // Not an atomic snapshot, which is fine for statistics.
    public long Sum()
    {
        PaddedLong[] stripes = _stripes;
        long sum = 0;
        for (int i = 0; i < stripes.Length; i++)
            sum += Volatile.Read(ref stripes[i].Value);
        return sum;
    }
}
```

**2. `ThreadLocal<T>` with `trackAllValues: true`** - each thread updates its own slot with no atomics at all; sum `Values` on read. `Values` allocates a list and includes values of threads that have already exited, and every thread that ever touched it keeps a slot, so this suits a bounded set of long-lived threads (e.g. `Parallel` workers) where writes vastly outnumber reads. Dispose the `ThreadLocal<T>` when done.

```cs
private readonly ThreadLocal<long> _count = new(trackAllValues: true);

void OnItem() => _count.Value++;
long Total() => _count.Values.Sum();
```

If the work structure allows it, aggregating into plain locals and combining once per task/partition (`Parallel.For` with `localInit`/`localFinally`) is cheaper than both.

### False sharing

Threads writing *different* variables still contend if those variables share a cache line (64 bytes on most x64/arm64 CPUs, 128 bytes on Apple M-series): every write invalidates the line in the other cores' caches. The naive striped counter is the classic case:

```cs
// 8 longs per 64-byte line: the per-core slots look independent, but cores keep stealing the line from each other.
long[] stripes = new long[Environment.ProcessorCount];
Interlocked.Increment(ref stripes[(uint)Thread.GetCurrentProcessorId() % (uint)stripes.Length]);
```

Fix it by padding each slot so no two hot values can land on the same line:

```cs
struct PaddedLong
{
    public long Value;
#pragma warning disable CS0169 // padding only
    private InlineArray8<long> _padding; // 64 bytes: adjacent Values are 72 bytes apart
#pragma warning restore CS0169
}
```

- Most server hardware (all x64 CPUs and Neoverse-based arm64 CPUs such as Cobalt and Graviton) has 64-byte cache lines, so 64 bytes of padding is usually sufficient. Apple M-series CPUs have 128-byte cache lines, so consider padding to 128 bytes there.
- The same applies to fields of one object written by different threads (e.g. a ring buffer's head and tail indices): put a padding field between them.

## Safe vectorization (SIMD or SWAR)

In .NET 11.0 the JIT got improvements in bounds check elimination and now recognizes the following shape for safely loading and processing multiple elements from an array/span:

```cs
while (data.Length >= CNS)
{
    // JIT knows that the slice has at least CNS elements, so bounds checks can be elided.
    // e.g.:
    //  VectorX.Create(data);
    //  BitConverter.ToInt32(data);
    //  BinaryPrimitives.ReadInt32LittleEndian(data);
    //  etc.
    data = data.Slice(CNS);
}
```

For example, the following C# snippet has no bounds checks in .NET 11.0:

```cs
int Sum(Span<int> data)
{
    Vector128<int> sum = default;
    while (data.Length >= Vector128<int>.Count)
    {
        sum += Vector128.Create(data);
        data = data.Slice(Vector128<int>.Count);
    }
    int result = Vector128.Sum(sum);
    foreach (var t in data)
        result += t;
    return result;
}
```

.NET 12.0 (Preview 1 or main) extends it to more complex loop shapes (e.g. multiple spans):

```cs
static bool Equal(ReadOnlySpan<ushort> a, ReadOnlySpan<ushort> b)
{
    if (a.Length != b.Length)
        return false;

    int i = 0;
    if (Vector128.IsHardwareAccelerated)
    {
        for (; i <= a.Length - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
        {
            var va = Vector128.Create(a.Slice(i));
            var vb = Vector128.Create(b.Slice(i));
            if (va != vb)
                return false;
        }
    }

    for (; i < a.Length; i++)
    {
        if (a[i] != b[i])
            return false;
    }

    return true;
}
```
^ has no bounds checks for the main loop in .NET 12.0 (Preview 1).

> [!TIP]
> This only illustrates the loop shape. For this exact task, just call `a.SequenceEqual(b)`.

More info about vectorization and SIMD in .NET can be found in the [official documentation](https://learn.microsoft.com/en-us/dotnet/standard/simd). Also consider using [TensorPrimitives](https://learn.microsoft.com/dotnet/api/system.numerics.tensors.tensorprimitives). The [dotnet/runtime vectorization skill](https://github.com/dotnet/runtime/blob/main/.github/skills/vectorization/SKILL.md) has authoring and testing checklists (remainder handling, running with `DOTNET_EnableAVX2=0` / `DOTNET_EnableHWIntrinsic=0` to cover every path); the `unsafe` load/store variants it mentions are out of scope here.

## Generic specialization via static abstract members

Static abstract interface members let you specialize a generic method for values supplied by struct tags, without duplicating its source code. The JIT generates separate native versions of `Big<C42>` and `Big<C100>`. In optimized code, it can inline the small `Value` getters and fold expressions and branches that use `T.Value`. This works even when `Big<T>` itself is too large to inline; The tradeoff is increased native code size. `T.Value` is not a C# compile-time constant: constant folding depends on JIT optimizations and getter inlining.

```cs
using System.Runtime.CompilerServices;

interface IConstant {
    static abstract int Value { get; }
}

readonly struct C42 : IConstant {
    public static int Value => 42;
}

readonly struct C100 : IConstant {
    public static int Value => 100;
}

static class Example
{
    static int Big<T>(int x) where T : struct, IConstant
    {
        int arg = T.Value;

        // The large body would go here.

        return arg == 42 ? x * arg : x + arg;
    }

    static int Use42(int x) => Big<C42>(x);
    static int Use100(int x) => Big<C100>(x);
}
```

It is also handy for scenarios where you want to leverage different vector sizes or constants in a generic way, allowing the JIT to generate optimized code for each specialization without duplicating the method's source code. Example: summing an array using different vector widths with static abstract members.

```cs
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

class Program
{
    static void Main()
    {
        int[] values = new int[1003];

        if (Vector128.IsHardwareAccelerated)
            Console.WriteLine(Sum<Vector128<int>, Ops128>(values));
        if (Vector256.IsHardwareAccelerated)
            Console.WriteLine(Sum<Vector256<int>, Ops256>(values));
        if (Vector512.IsHardwareAccelerated) 
            Console.WriteLine(Sum<Vector512<int>, Ops512>(values));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Sum<V, TOps>(ReadOnlySpan<int> s)
        where V : struct
        where TOps : struct, IOps<V>
    {
        V acc = default;
        int count = TOps.Count;

        while (s.Length >= count)
        {
            acc = TOps.Add(acc, TOps.Load(s));
            s = s.Slice(count);
        }

        int sum = TOps.Sum(acc);
        foreach (int x in s) sum = unchecked(sum + x);
        return sum;
    }
}


interface IOps<V> where V : struct
{
    static abstract int Count { get; }
    static abstract V Load(ReadOnlySpan<int> s);
    static abstract V Add(V a, V b);
    static abstract int Sum(V v);
}

readonly struct Ops128 : IOps<Vector128<int>>
{
    public static int Count => Vector128<int>.Count;
    public static Vector128<int> Load(ReadOnlySpan<int> s) => Vector128.Create(s);
    public static Vector128<int> Add(Vector128<int> a, Vector128<int> b) => a + b;
    public static int Sum(Vector128<int> v) => Vector128.Sum(v);
}

readonly struct Ops256 : IOps<Vector256<int>>
{
    public static int Count => Vector256<int>.Count;
    public static Vector256<int> Load(ReadOnlySpan<int> s) => Vector256.Create(s);
    public static Vector256<int> Add(Vector256<int> a, Vector256<int> b) => a + b;
    public static int Sum(Vector256<int> v) => Vector256.Sum(v);
}

readonly struct Ops512 : IOps<Vector512<int>>
{
    public static int Count => Vector512<int>.Count;
    public static Vector512<int> Load(ReadOnlySpan<int> s) => Vector512.Create(s);
    public static Vector512<int> Add(Vector512<int> a, Vector512<int> b) => a + b;
    public static int Sum(Vector512<int> v) => Vector512.Sum(v);
}
```

# ASP.NET Core

Code-level advice for HTTP throughput benchmarks. To run the TechEmpower apps against a locally built runtime, see the [dotnet/runtime aspnet-benchmark skill](https://github.com/dotnet/runtime/blob/main/.github/skills/aspnet-benchmark/SKILL.md).

## Hosting and logging

- Start from `WebApplication.CreateSlimBuilder(args)` (or `CreateEmptyBuilder` + `builder.WebHost.UseKestrelCore()`) and add only what's needed. Every middleware component (HTTPS redirection, auth, CORS, response compression, static files, developer exception page) costs something per request. The slim builder drops HTTPS configuration, HTTP/3, regex route constraints, IIS integration and hosting startup assemblies, but still reads `appsettings.json` and still adds a console logger.
- Logging: without an `appsettings.json` that raises `Microsoft.AspNetCore` to `Warning`, the default level is `Information` and the host logs every request to the console - a massive bottleneck. Raising the level removes the log writes, but while any provider is registered the host still creates an `Activity` and a logging scope per request; `builder.Logging.ClearProviders()` removes those too.
- Measure in the `Production` environment. `Development` turns on DI `ValidateScopes`/`ValidateOnBuild`, and `dotnet run` picks it up from `launchSettings.json`: run the published output, or `dotnet run -c Release --no-launch-profile`.

## JSON and response writing

- JSON: register source-generated metadata with `builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));`. For hand-written responses, serialize with a reused `Utf8JsonWriter` (`writer.Reset(response.BodyWriter)`) instead of allocating one per request.
- Make response DTOs `sealed` (or structs). A minimal API handler returning a sealed `T` gets a direct serialization path chosen once when the endpoint is built; for a non-sealed class the framework calls `GetType()` on every response and falls back to serializing as `object` when the runtime type differs.
- On the hottest endpoints return `T` itself. `TypedResults.Ok(value)` and `Results.Json(value)` do a type-info lookup and a `GetType()` check per call even for sealed types.
- `WriteAsJsonAsync` never sets `Content-Length`, and Kestrel only fills it in for empty bodies, so HTTP/1.1 responses go out chunked. When the exact size is known up front, set `Response.ContentLength` (a mismatch is an error).
- Static payloads: keep a pre-encoded `static readonly byte[]`, set `ContentLength`, write once. `Results.Text(utf8Span, ...)` copies the span into a new array on each call; the result object is immutable, so create it once in a static field. Don't pass `statusCode` to it on hot paths: that resolves `ILoggerFactory` and creates a logger per request.
- Set headers through the typed properties (`response.Headers.ContentType = ...`, analyzer ASP0015): on Kestrel they are direct field writes, while the string indexer goes through a name lookup.

## Routing and middleware

- A handler that is a plain `RequestDelegate` (`app.MapGet("/x", (HttpContext ctx) => ...)` returning `Task`) bypasses parameter binding and the generated request delegate entirely. If such a lambda returns `Task<T>`, the value is silently discarded (analyzer ASP0016).
- Inline middleware: use `app.Use(async (context, next) => { ...; await next(context); })`. The `next()` form (`Func<Task>`) allocates a closure per request.
- Prefer convention-based middleware classes (constructed once) over `IMiddleware`, which is resolved from DI and released on every request.
- `.ShortCircuit()` on an endpoint (or `app.MapShortCircuit(404, "favicon.ico")`) runs it inside the routing middleware and skips the rest of the pipeline. It throws if the endpoint has auth, CORS or antiforgery metadata.
- Avoid `IHttpContextAccessor` in hot services: once registered (ASP.NET Core Identity does it for you), an `AsyncLocal` is set on every request. Pass `HttpContext` explicitly.

## Kestrel socket transport

Configure with `builder.WebHost.UseSockets(o => ...)`:

- `WaitForDataBeforeAllocatingBuffer = false` trades memory per idle connection for throughput.
- `IOQueueCount` sets the number of I/O queues that socket continuations are scheduled on (default is based on the processor count); `0` schedules them directly on the thread pool.

## Beyond benchmarks

- Output caching (`AddOutputCache`, `.CacheOutput()`): the default policy caches only unauthenticated `GET`/`HEAD` 200 responses without `Set-Cookie`.
- Serve static files with `MapStaticAssets` rather than `UseStaticFiles`: precompressed variants and ETags are computed at build time.
- Response compression is CPU per request and is off for HTTPS by default; prefer precompressed assets or the reverse proxy.
- `-p:EnableRequestDelegateGenerator=true` moves minimal API handler codegen from startup to build time (startup win; on by default with `PublishAot` or `PublishTrimmed`).

# Magic knobs to Tune .NET Performance

> [!WARNING]
> It is highly recommended to keep the default settings unless you have measured and understood the performance implications of changing them. 

Most of these knobs are relevant only for server workloads or performance-critical scenarios. They're not applicable for general-purpose libraries.

⭐ - known to have a visible impact on performance (typically, in synthetic benchmarks).

> [!NOTE]
> Numeric `DOTNET_*` values are parsed as **hexadecimal** unless a row says otherwise: `DOTNET_GCHeapCount=16` means 22. Most knobs also have a `runtimeconfig.json`/MSBuild form that takes decimal values (`System.GC.HeapCount`, `System.Threading.ThreadPool.MinThreads`, `<ServerGarbageCollector>`, `<TieredPGO>`). All of them are read once at startup.

| Environment variable (CoreCLR Release, .NET 10.0+) | Default | Values to try | Purpose and trade-offs |
| --- | --- | --- | --- |
| ⭐`DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS` | `0` | `1` | Run I/O continuations directly on poller threads instead of dispatching them to workers - beneficial for workloads with many small requests. ⚠️ Blocking or lengthy continuations can stall I/O ⚠️ Kestrel still dispatches application code to the thread pool unless its `SocketTransportOptions.UnsafePreferInlineScheduling` is also set, which then runs request handlers on the I/O threads. |
| ⭐`DOTNET_ThreadPool_UnfairSemaphoreSpinLimit` | `70` in .NET 10.0; `1024` in current .NET 11.0 | `0`, `0x400`, `0xC00` | Trade worker wake latency against CPU spent spinning. `0` might be useful as a mitigation for high CPU usage (especially on arm64) at the cost of throughput. Large values might improve synthetic benchmark performance. |
| ⭐`DOTNET_GCDynamicAdaptationMode` | `1` | `0` | Disable DATAS for server GC. Can improve throughput at the expense of memory; compare against tuning DATAS (e.g. `DOTNET_GCDGen0GrowthPercent`) rather than disabling it. |
| ⭐`DOTNET_HillClimbing_Disable` | `0` | `1` | Disable ThreadPool hill climbing (improves time to steady state). |
| `DOTNET_SYSTEM_NET_SOCKETS_THREAD_COUNT` | Automatic; processor count when inline completions are enabled | `1`, `2`, `4`, `8`, up to processor count | Tune I/O poller count. Fewer pollers can reduce scheduling and cache overhead; too few can serialize processing. Values are decimal. |
| `DOTNET_gcServer` | varies by environment | `1` | Typically enabled by default for server workloads. Might improve the performance of desktop/console tools. |
| `DOTNET_GCDTargetTCP` | Effective target `2%` (configuration `0`) | `1` | Lower DATAS's target throughput-cost percentage. Encourages more heaps when GC cost exceeds the target. Only relevant with DATAS enabled. |
| `DOTNET_GCDGen0GrowthPercent` | Effective multiplier `100%` (configuration `0`) | `0xC8` | Scale DATAS's gen0 budget multiplier to 200% of its default. Trades memory for potentially fewer collections. Only relevant with DATAS enabled. |
| `DOTNET_GCHeapAffinitizeMask`, `DOTNET_GCNoAffinitize` | GC threads affinitized to all cores | A core mask, or `1` | Keep server GC threads on a subset of cores, or let them float, when the process doesn't own the whole machine. |
| `DOTNET_MaxVectorTBitWidth` | Automatic (`Vector<T>` is at most 256-bit) | `512` | Allow 512-bit `Vector<T>`. Decimal. |
| `DOTNET_ThreadPool_ForceMinWorkerThreads` | Processor count | e.g. `0x40` | Minimum worker threads created without delay. A stopgap for [thread pool starvation](#thread-pool-starvation) or bursty blocking work. |
| `DOTNET_TC_CallCountingDelayMs` | `100` ms; longer on a single processor | `0` | Only for short-lived console tools where JIT warmup takes too long: remove the tiering delay while retaining the normal call-count threshold. Can increase compilation work early in execution. |
| `DOTNET_TieredCompilation` | `1` | `0` | Alternative for short-lived console tools where JIT warmup takes too long: disable tiering and JIT optimized code on first use. Removes tiered warmup but disables dynamic PGO and can increase initial JIT cost. |

Using NativeAOT or disabling tiered compilation via `DOTNET_TC_CallCountingDelayMs=0`/`DOTNET_TieredPGO=0`/`DOTNET_TieredCompilation=0` may significantly improve performance for short-lived console tools or benchmarks that measure the entire process rather than just steady-state execution.

# NativeAOT-specific tuning

Recommended reading: Microsoft's [Optimizing AOT deployments](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing) article, plus the runtime's [CPU instruction-set tuning guide](https://github.com/dotnet/runtime/blob/9da219572c1/src/coreclr/nativeaot/docs/optimizing.md). The latter describes implementation-specific options that can change between SDK versions.

> [!NOTE]
> These are publish-time MSBuild properties: republish for each experiment. NativeAOT has no JIT or tiered compilation, so the CoreCLR JIT/tiering environment variables above do not apply.

| MSBuild setting | Purpose and trade-offs |
| --- | --- |
| `-p:OptimizationPreference=Speed` | Optimize for execution time (`--Ot`) instead of the default size/speed balance. A good throughput experiment, but can increase executable size and instruction-cache pressure. Also applies the framework's static PGO data (`IlcPgoOptimize`). |
| `-p:OptimizationPreference=Size` | Optimize for executable size (`--Os`). Useful when distribution size or memory footprint matters more than peak throughput; measure the speed trade-off. |
| `-p:IlcInstructionSet=native` | Target instruction sets supported by the build machine's CPU. Useful for local benchmarks or homogeneous deployments; no cross-architecture compilation support, and the executable may not run on older CPUs. Use an explicit instruction-set baseline for heterogeneous deployments. |
| `-p:IlcInstructionSet=x86-64-v3` | The x64 equivalent: raise the baseline to AVX2-class CPUs (`x86-64-v4` adds AVX-512). The executable refuses to start on older CPUs. |
| `-p:IlcMaxVectorTBitWidth=512` | Widen `Vector<T>` (needs a matching instruction set). The compile-time counterpart of `DOTNET_MaxVectorTBitWidth`. |
| `-p:IlcInstructionSet=armv8.1-a` | If `IlcInstructionSet=native` is not acceptable for you, consider explicitly raising the baseline from ARMv8.0-A to ARMv8.1-A to include LSE (crucial for atomics-heavy workloads). |

```sh
dotnet publish -c Release -r linux-arm64 -p:PublishAot=true -p:OptimizationPreference=Speed -p:IlcInstructionSet=native
```

# General notes

- Consider filing a [dotnet/runtime](https://github.com/dotnet/runtime/issues) issue only for a clear, reproducible missed optimization in the JIT or libraries with meaningful measured impact. Check for duplicates, include a minimal repro and measurements, and obtain the user's approval before filing.
- Compare equivalent execution plans before blaming the language. Consider pairwise comparisons, stronger filtering, and verification requirements before tuning code generation.
- Don't draw conclusions about workloads from tiny synthetic benchmark results.