# MCP setup and profiling patterns

## Connection and artifacts

The global tool is an MCP server. For local development, configure the client to spawn `dotnet-diagnostics-mcp --stdio`; stdio manages the process lifetime and does not need bearer credentials. The upstream [client setup](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/client-setup.md) includes client-specific JSON examples. Follow the client's native format instead of assuming that one `mcpServers` schema applies everywhere.

Ensure its environment can find the global tool and .NET 10 runtime. The target process can use CoreCLR .NET 8, 9, or 10; the server's runtime requirement and the diagnosed process's runtime are separate. NativeAOT and privileged collectors have narrower support: inspect the target's capabilities and the returned errors before choosing them.

`MCP_ARTIFACT_ROOT` selects the server's artifact directory. Use an existing writable physical directory and resolve symlinks in its full path. On macOS, `/tmp` is commonly a symlink to `/private/tmp`; the published server's capture cleanup can reject a symlink-based artifact root during startup. Configure a real directory in the server's environment, for example this additional field inside a Claude Desktop / Cursor-style server entry:

```json
{
  "env": {
    "MCP_ARTIFACT_ROOT": "/path/to/physical-directory/diagnostics-artifacts"
  }
}
```

Replace the placeholder and create the directory before connecting. Use a private location for trace artifacts. The default live handles are session-local and expire; record `handleExpiresAt`. Set `persist=true` only when evidence must survive a server restart and follow the upstream durable-capture access contract; saving a handle string alone does not retain its data.

For a remote HTTP deployment, use the upstream bearer/sidecar configuration and `inspect_process(view="preflight")` to diagnose permission failures. A local stdio recipe does not configure a shared HTTP server.

## Collector selection

| Evidence needed | MCP call | Interpretation or constraint |
|---|---|---|
| Select a host | `inspect_process(view="list", commandLineContains=...)` | Filter by the workload, then use an explicit PID. |
| Long-lived app triage | `inspect_process(view="triage", processId=...)` | Short runtime-counter window and evidence-selected next steps. |
| CPU and managed allocation together | `collect_batch` with `collect_sample/cpu` and `collect_sample/allocation` | 1–4 unique entries, one PID, shared duration, per-entry errors. |
| Source-resolved CPU hotspots | `collect_sample(kind="cpu", resolveSourceLines=true)` | Matching PDB/SourceLink; remote symbol servers require an allowlist. |
| GC pauses and generations | `collect_events(kind="gc")` | Query its handle with `pauseHistogram` or `byGeneration`. |
| Runtime pressure | `collect_events(kind="counters")` | Interpret CPU, allocation rate, heap, queue, and contention together. |
| Managed lock contention | `collect_events(kind="contention")` | Inspect event evidence before blaming CPU work. |
| Waiting stacks | `collect_sample(kind="off_cpu")` | Linux perf or elevated Windows ETW; unsupported hosts return a structured error. |
| Call sites and before/after changes | `query_snapshot` | Reuse handles with supported per-kind views; no new live capture. |

Choose heap or thread snapshots only when sampling/event evidence needs that follow-up: live attach can pause the target and requires additional permissions. `Busy` concerns the same target PID's attach collision domain; it does not justify serializing unrelated test modules. Method-parameter capture is a separate privileged, explicitly gated workflow and is unnecessary for ordinary unit-test CPU/allocation profiling.

## Settings that change behavior

- `durationSeconds`: collection window, at least one second; longer windows improve representation only if the workload continues. Match baseline/follow-up duration.
- `depth="compact"` on `collect_batch`: returns summaries, handles, and the bounded digest instead of repeating large child payloads. Inspect entry errors even when the parent call succeeds.
- `topN`, `maxDepth`, and `maxNodes` on supported `query_snapshot` views: bound drilldown output. Request detail for the measured branch, not an entire raw tree.
- `cpuBackend`: `Automatic` selects EventPipe for CoreCLR; `EventPipe` and `Os` are explicit choices and never silently fall back. The OS backend needs Linux perf or elevated Windows ETW for true on-core evidence.
- `resolveSourceLines`, `maxResolvedSources`, and `symbolPath`: control source resolution. Use local matching symbols; a missing source location does not invalidate a named stack sample.
- `exportTrace=true` on an EventPipe CPU sample: retains the raw `.nettrace` and returns an artifact-relative path. Fetch it with `get_bytes(kind="trace")` when an offline viewer needs the bytes; a server-local path is not a client download.

`GCAllocationTick` samples roughly every 100 KB of managed allocations. It ranks sampled type/site pressure rather than counting every object or proving a leak. CoreCLR EventPipe CPU evidence is managed stack frequency with heuristic wait classification; use its `evidence` metadata and do not call it exact CPU time. Different runtimes/backends and NativeAOT symbol gaps must stay visible in the result.

The `collect_events` `launch` option is restricted to startup collection, stdio, and `Diagnostics:AllowProcessLaunch=true`. Its implementation terminates the target afterward. Neither `collect_sample` nor `collect_batch` accepts that launch option.

## Source map

The guidance was checked against the published `v0.28.0` source and its NuGet tool. Recheck current schemas after upgrading.

- [Repository and installation](https://github.com/pedrosakuma/dotnet-diagnostics#install)
- [Consumer setup and runtime prerequisites](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/consumer-install.md)
- [Local stdio and HTTP client configuration](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/client-setup.md)
- [Tool parameters, response envelopes, handles, and evidence semantics](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/tool-reference.md)
- [CPU, allocation, waiting, and regression playbooks](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/investigation-playbooks.md)
- [Target-runtime compatibility and validation boundaries](https://github.com/pedrosakuma/dotnet-diagnostics/blob/main/docs/runtime-version-compat-matrix.md)
- [Published source baseline](https://github.com/pedrosakuma/dotnet-diagnostics/tree/v0.28.0)
