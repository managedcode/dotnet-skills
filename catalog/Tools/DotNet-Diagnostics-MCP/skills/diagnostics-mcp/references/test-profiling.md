# Profiling unit-test workloads

## Inventory and launch

Determine the runner from project files and `global.json`: VSTest adapters and Microsoft.Testing.Platform have different command and filter contracts. Identify all unit-test projects in scope and separate integration or end-to-end projects. Keep the normal number of test modules and workers; profiling is not a reason to serialize the suite.

Build Release binaries first. For a VSTest-based project, an illustrative run is:

```bash
dotnet build ./tests/MyApp.UnitTests/MyApp.UnitTests.csproj -c Release
dotnet test ./tests/MyApp.UnitTests/MyApp.UnitTests.csproj -c Release --no-build --no-restore
```

Repeat for the discovered unit-test projects, or use the repository's existing unit-test solution/CI entry point. For an SDK-integrated Microsoft.Testing.Platform runner, use its native `--project` / `--solution` entry point and runner-specific filters. Verify the installed runner's help before supplying options; do not apply VSTest `--filter` or MTP progress flags indiscriminately.

Run the suite in a terminal/process that remains active while the agent calls MCP tools. Retain the runner's result artifact where supported and its real exit code. A shell launch PID often belongs to `dotnet test`, while the measured code executes in one or several child hosts. A self-hosted MTP test executable can have the project assembly name instead of `testhost`.

## Find the actual measured processes

Call `inspect_process` with a narrow command-line filter:

```json
{ "view": "list", "commandLineContains": "MyApp.UnitTests" }
```

If the VSTest host's command line omits that assembly name, use `commandLineContains="testhost"`, then verify the candidates with `view="info"` and the runner's process information. Match the requested run, project, architecture, and target framework; do not profile another checkout's concurrent test run.

Use explicit PIDs for each collector. Repeat discovery when hosts restart or multiple target frameworks run. If tests exercise an out-of-process service, capture that service as well and associate it with the responsible test group.

## Capture before the host exits

`collect_batch` resolves one PID once and starts 1–4 collectors concurrently. CPU plus allocation is a useful first pair; GC or counters can fill the remaining slots. It avoids a second round-trip after a short-lived host has exited, but it cannot recover execution that finished before attachment.

Use independent bounded captures for each live host, while leaving test execution parallel. Align capture duration with the workload, inspect errors for every entry, and record time spent before attachment. The default ten-second window may exceed a short suite's lifetime. Counters need reporting intervals, so a tiny window may yield sparse counter evidence even if CPU samples exist.

When a host exits too quickly, report that gap and rerun the smallest useful test group with its native runner filter. Repeated executions can gather evidence, but are not proof that the first run was fully covered. Do not add artificial sleeps to product code or make the entire suite single-threaded to keep a process alive. The MCP `launch` parameter is limited to `collect_events(kind="startup")`, requires stdio and an explicit process-launch setting, and terminates its launched process afterward; it is not a general CPU/allocation launcher for `dotnet test`.

## Attribute bottlenecks

Start with the batch's `investigationDigest` when present, then query the returned CPU and allocation handles with `view="hot-path"`, `view="call-tree"`, or `view="caller-callee"`. Group by module or namespace when runner infrastructure dominates. Resolve source lines from the matching Release PDBs; when unavailable, retain method/module identity and disclose the missing location.

For each candidate, establish which product call is exercised by which test group. An aggregate capture of concurrent tests does not provide exact per-test CPU or allocation accounting. Reproduce the group before attributing a hotspot to one test, and separate fixture initialization, generated data, assertions, runner dispatch, startup/JIT, and profiler costs from product work.

EventPipe SampleProfiler samples managed stacks including recognizable waits. A large `Task.Wait` or monitor frame is a waiting hypothesis, not measured on-core CPU time. Corroborate it with runtime counters, `collect_events(kind="contention")`, ThreadPool events, or an OS-supported off-CPU capture. Managed allocation samples identify pressure and candidate call sites; they do not establish retained-memory leaks or exact bytes per test.

## Compare and report

Record baseline and follow-up runs with the same code path, input, framework, runner, Release configuration, worker concurrency, and capture duration. Separate cold-start and warmed execution. Save both handles during the same MCP server session and query a compatible CPU/CPU or allocation/allocation pair:

```json
{
  "handle": "<after-handle>",
  "baselineHandle": "<before-handle>",
  "view": "diff",
  "topN": 10,
  "depth": "compact"
}
```

Keep test completion and profiling coverage as separate evidence. Report discovered/executed/passed/failed/skipped counts from the runner, projects and hosts observed, captured intervals, and all gaps. Rank product hotspots with supporting counts/shares, source locations, affected test groups, confidence, and the next actionable measurement or change. Do not convert a sampling percentage into an exact millisecond saving.

Upstream contracts: [process discovery](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/tool-reference.md#inspect_process), [batch capture](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/tool-reference.md#collect_batch), and [investigation playbooks](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/investigation-playbooks.md).
