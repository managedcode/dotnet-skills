# Roslynk

**Roslyn + link** — the link between AI test harnesses and Roslyn.

> [![YourKit](https://www.yourkit.com/images/yklogo.png)](https://www.yourkit.com/)
>
> Roslynk is an open-source project using [YourKit .NET Profiler](https://www.yourkit.com/dotnet-profiler/).

Add the MCP to your AI harness, then type "Open (solution file name)" - that's it!

The biggest time saver you will see is checking for compiler errors and warnings; with
Roslynk it is practically instant, no need to wait minutes for the solution to build.

Roslynk gives an MCP client (e.g. Claude) semantic intelligence over C# code via Roslyn:
* diagnostics
* symbol navigation
* find-references
* semantic rename
* code actions
* dead-code detection
* and more

All operating directly on the projects compiled in a loaded solution!

- **Transport:** HTTP only, bound to loopback (`127.0.0.1`/`::1`).
- **Host:** foreground console on Linux/WSL/macOS; on Windows, also installable as a headless service. No UI.
- **Observability:** OpenTelemetry, exported via OTLP to a backend you configure.


## Connect an MCP client (all platforms)

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on `PATH`. The `stdio` verb is a
self-launching bridge: the MCP client spawns it, it starts the shared HTTP daemon on
`localhost:6502` if one isn't already running, and pipes the session through. Nothing to launch or
babysit by hand.

First install the global .NET tool from NuGet (no clone, no build needed):

```bash
dotnet tool install roslynk -g
```

### Claude Desktop

```bash
claude mcp add roslynk -- dnx Roslynk --yes -- stdio
```

### Antigravity (`agy`)

Using the CLI:

```bash
agy mcp add roslynk roslynk stdio
```

(Or with `dnx`: `agy mcp add roslynk dnx Roslynk --yes -- stdio`)

Or manually in `~/.gemini/config/mcp_config.json`:

```json
{
  "mcpServers": {
    "roslynk": {
      "command": "roslynk",
      "args": ["stdio"]
    }
  }
}
```

To attach to an already-running daemon instead of spawning one:

```bash
agy mcp add roslynk http://localhost:6502
```

### Manual `mcp.json` (VS Code, Cursor, Windsurf, and others)

Add the following entry to your MCP configuration file (e.g. `.vscode/mcp.json`,
`~/.cursor/mcp.json`, or your AI tool's equivalent):

```json
{
  "mcpServers": {
    "roslynk": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Roslynk", "--yes", "--", "stdio"]
    }
  }
}
```

> **Important:** the `stdio` argument at the end is required. Without it the process starts as an
> HTTP daemon and never responds to the MCP `initialize` handshake, causing the client to hang.

Tagged releases (e.g. `1.0.0-beta.1`, no `v` prefix) are packed and pushed to nuget.org by CI.

From a source checkout:

```bash
claude mcp add roslynk -- dotnet run --project /path/to/Roslynk/Source/App/Morris.Roslynk.Mcp -- stdio
```

Or in `mcp.json` from a source checkout:

```json
{
  "mcpServers": {
    "roslynk": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/Roslynk/Source/App/Morris.Roslynk.Mcp", "--", "stdio"]
    }
  }
}
```

(With a published build, use `Morris.Roslynk.Mcp stdio` as the command instead — it starts faster.)

### DeepSeek Harness (`dsh`)

DeepSeek Harness bridges MCP servers through `@deepseek-ai/dsh-mcp-client` — one plugin row per
server. Add Roslynk to your user patch layer at `$DSH_HOME/cordis.patch.yml` (defaults to
`~/.dsh/cordis.patch.yml` or `%USERPROFILE\.dsh\cordis.patch.yml`), which is applied to every profile:

```yaml
- insert:
    - id: mcp-roslynk
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: roslynk
        transport: stdio
        command: dnx
        args: ['Roslynk', '--yes', '--', 'stdio']
```

> **Important:** a new plugin has to go inside an `insert:` list. A bare top-level `- id: mcp-roslynk`
> row is an *id-targeted config override* of a row that already exists in the composed tree, so `dsh`
> prints `patch: entry "mcp-roslynk" not found` to stderr and boots with no bridge and no tools.

To attach to an already-running daemon instead of spawning one, use the HTTP transport:

```yaml
- insert:
    - id: mcp-roslynk
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: roslynk
        transport: streamable-http
        url: http://localhost:6502
```

The tools reach the model as `mcp__roslynk__*`. Check the composition without booting a session:

```bash
dsh --profile web --dump-config
```

The row should be listed under a `# == <path>/cordis.patch.yml` comment, with no `not found` warning
above it. `dsh` watches the patch file, so edits reconnect the server without restarting the process.

The daemon outlives individual sessions so Roslyn workspaces stay warm across clients. Its console
output goes to `Roslynk/daemon.log` under the local application-data folder (`~/.local/share` on
Linux, `~/Library/Application Support` on macOS, `%LOCALAPPDATA%` on Windows).

## 🤖 Configure your agent

Connecting the MCP server is only half the job. Left to its own habits an agent will still `grep`
for call sites, read whole files to find a member, and run `dotnet build` to check for errors — all
slower and less correct than the equivalent Roslynk call. Teach it the tools once and it stops.

### Claude Code: install the skill

This repo ships a ready-made skill in [`skills/roslynk`](skills/roslynk). Copy it into your skills
folder and Claude will load it automatically whenever a task touches C#:

```bash
# user-wide (all projects)
cp -r skills/roslynk ~/.claude/skills/

# or per-project
cp -r skills/roslynk .claude/skills/
```

### Antigravity: install the skill

Antigravity discovers skills in `~/.gemini/config/skills` for user-wide skills, and
`<projectRoot>/.agents/skills` for project ones:

```bash
# user-wide (all projects)
cp -r skills/roslynk ~/.gemini/config/skills/

# or per-project
cp -r skills/roslynk .agents/skills/
```

### DeepSeek Harness: install the skill

`dsh` scans `$DSH_HOME/skills` (defaults to `~/.dsh/skills`) for user-wide skills, and
`<projectRoot>/.dsh/skills` for project ones — the project root being the nearest ancestor
containing `.git`:

```bash
# user-wide (all profiles, all projects)
cp -r skills/roslynk ~/.dsh/skills/

# or per-project
cp -r skills/roslynk .dsh/skills/
```

The skill directory is watched, so a fresh copy is picked up without restarting `dsh`.

### Everything else: example system prompt for LLMs

> [!IMPORTANT]
> **⚡ Pro Tip:** Add this system prompt to your `AGENTS.md` / `CLAUDE.md`, Cursor Rules, Windsurf
> rules, or any AI agent configuration file. It dramatically improves how often and how well your
> LLM reaches for Roslynk instead of grepping, reading files, or rebuilding the solution.

> You have access to `roslynk`, an MCP server holding a live Roslyn compilation of a C# solution.
> Prefer it over grep, file reads, hand-written edits, and `dotnet build` for anything semantic:
> it answers from the compiler's symbol model, so it sees partial classes, generated code, inactive
> `#if` branches and Razor, and it never matches names inside comments or strings.
>
> **Getting started:** call `open_solution` with the absolute path to the `.sln`/`.slnx`; the
> `solutionId` it returns is the handle every other tool needs. It loads in the background — while
> it does, other calls return `error=Indexing`; retry shortly or poll `get_solution_status`. Never
> fall back to reading or editing files directly. Never call `reload_solution` on your own
> initiative — a file watcher picks up edits automatically; suggest a reload instead.
>
> **Lifecycle:**
> - `open_solution`: Load a solution. Parameters: `solutionPath` (absolute). Idempotent, cheap to repeat.
> - `get_solution_status`: List every loaded solution and its progress, with the messages behind `loadDiagnostics` (skipped analyzers, failed project loads) indented under each Ready solution. A missing generator DLL names the project to build; once built, the next call picks it up without a reload. Optional parameter: `allLoadDiagnostics` (every message instead of the first 20 per solution).
> - `reload_solution`: Force a from-disk re-evaluation. Parameter: `solutionId`. Only when the user explicitly asks.
>
> **Navigation:**
> - `find_definition`: Go to definition from a cursor position. Parameters: `solutionId`, `filePath`, `line`, `column` (1-based).
> - `get_expression_info`: Compiler facts about the expression at a position: type and converted type, bound symbol and selected overload, nullability, constant value, implicit conversion, origin and doc summary; on a declared name, the declared symbol and its type. Parameters: `solutionId`, `filePath` (`.cs`/`.razor`/`.cshtml`), `line`, `column` (1-based).
> - `get_symbol`: Identify a symbol and get its declaration. Parameters: `solutionId`, `symbolName` (fully-qualified).
> - `get_symbol_body`: Read a symbol's complete source text, body included. Parameters: `solutionId`, `symbolName` (fully-qualified), `includeLeadingTrivia`.
> - `get_members`: List a type's members with their locations. Parameters: `solutionId`, `typeName`, `includeInherited`, `nameFilter`, `includeMethods`/`includeFields`/`includeProperties`/`includeEvents`/`includeNestedTypes`.
> - `search_symbols`: Find symbols by partial name. Parameters: `solutionId`, `query`, `maxResults`.
> - `multi_query`: Run several of the read-only queries above in one call, against a single snapshot of the solution. Parameters: `solutionId`, `operations` (each names a tool and carries that tool's own parameters), optional `expectSnapshot`. See the skill's multi_query section. For impact questions ("what uses X?", "what breaks if I change X?"), batch `find_references`, `get_callers`, `get_callees`, `find_implementations` and `get_type_hierarchy` in one call.
>
> **Relationships:**
> - `find_references`: Every usage of a symbol. Parameters: `solutionId`, `symbolName`, `maxResults`.
> - `find_reads` / `find_writes`: Where a field, property or parameter is read or written, each location tagged `read`, `assign`, `compound`, `increment`, `ref`, `out` or `init`; `compound`, `increment` and `ref` appear in both. Parameters: `solutionId`, `symbolName` (a parameter is `Namespace.Type.Method:parameter`), `maxResults`.
> - `get_callers`: Who calls a method, overloads resolved. Parameters: `solutionId`, `methodName`.
> - `get_callees`: What a member calls - the inverse of `get_callers`; methods, constructors, property/event accessors and operators, overload-exact, with referenced-assembly callees grouped per assembly. Parameters: `solutionId`, `memberName`, `excludeExternal`.
> - `find_implementations`: Implementors/overrides of an interface, abstract or virtual member. Parameters: `solutionId`, `symbolName`.
> - `get_type_hierarchy`: Base types, interfaces and derived types. Parameters: `solutionId`, `typeName`.
>
> **Diagnostics:**
> - `get_diagnostics`: Compile check — this replaces `dotnet build`. Parameters: `solutionId`, `includeErrors`, `includeWarnings`, `includeInfo`, `includeHidden` (**all default false**), `includeAnalyzers`. The header always reports `errors=`/`warnings=`/`infos=`/`hidden=` counts; opt into detail only when they are non-zero. Costly per call (it compiles and analyzes everything the edits affect), so run it once after a task's edits, not after each one. Razor compiler (`RZ*`) errors are included, reported against the `.razor`/`.cshtml` file.
>
> **Code actions:**
> - `get_code_actions`: List fixes and refactorings at a position. Parameters: `solutionId`, `documentPath`, `line`, `column`, `endLine`, `endColumn`.
> - `apply_code_action`: Apply one by its opaque `actionId` (pass it back verbatim). Parameters: `solutionId`, `actionId`, `checkOnly`.
> - `apply_code_fix`: Fix the diagnostic (compiler or analyzer, e.g. `CS0219` or `IDE0005`) at a position `get_diagnostics` reported, no round-trip. Parameters: `solutionId`, `documentPath`, `diagnosticId`, `line`, `column` (required), `checkOnly`. When the diagnostic has several fixes it writes nothing and returns `error=Conflict` with a `candidate` actionId per fix; apply the chosen one with `apply_code_action`.
>
> **Editing:**
> - `apply_patch`: Edit text files with a git unified diff. Parameters: `solutionId`, `patch`, `baseVersions`, `checkOnly`. Hunks are content-anchored, not line-number-anchored — include enough context that each matches exactly one place. Paths are relative to the solution folder (a unique suffix also resolves); a missing file is `NotFound`, an ambiguous suffix `Ambiguous`.
> - `rename_parameter`: Rename one parameter of a method, constructor or indexer overload with Roslyn: declaration, body, named arguments and `<paramref>` docs, cascading across the override/interface family. Parameters: `solutionId`, `methodId`, `parameterName`, `newName`, `checkOnly`.
> - `rename_symbol`: Compiler-correct rename across partial classes, every `#if` branch, every target framework, and `.razor`/`.cshtml`. Parameters: `solutionId`, `symbolName`, `newName`, `checkOnly`.
> - `change_signature`: Append one optional parameter to an ordinary method and thread an argument into every call site. Parameters: `solutionId`, `methodId`, `parameterType`, `parameterName`, `defaultValue`, `callSiteArgument`, `checkOnly`.
> - `extract_method`: Extract a selection of statements or an expression into a new method or local function with Roslyn's own Extract Method refactoring; writes nothing when Roslyn cannot extract it safely or the result would not compile. Parameters: `solutionId`, `documentPath`, `startLine`, `startColumn`, `endLine`, `endColumn`, `methodName`, `asLocalFunction`, `checkOnly`.
> - `remove_unused_usings`: Strip unused `using` directives (CS8019), preserving surrounding trivia. Parameters: `solutionId`, `documentPath` (omit for the whole solution), `checkOnly`.
>
> **Dead code:**
> - `find_dead_code`: Unreferenced members with a confidence and a reason — candidates, never verdicts; it deletes nothing. Parameters: `solutionId`, `scope` (FQN prefix; use it on large solutions), `includePublic`, `maxResults`.
> - `find_dead_conditionals`: `#if` branches never compiled under any loaded configuration. Parameter: `solutionId`.
>
> **Conventions:** most tools take a fully-qualified `Namespace.Type.Member` name (no `global::`); a local
> function is named as a member of the method declaring it (`Namespace.Type.Method.local`, nesting further
> for one inside another);
> `find_definition`, `get_expression_info` and `get_code_actions` are position-based instead. Every tool that takes a
> `documentPath`, and every write tool, works on `.razor` and `.cshtml` files as well as `.cs`: positions
> are given in the Razor file, and edits are written back to it in its own indentation. Responses are a compact
> `key=value` header block, a blank line, then a tab-indented outline; booleans are `Y`/`N`. Errors
> are header-only: `error=Indexing` (retry), `Ambiguous`/`NotFound` (pick from the `candidate=`
> lines), `Stale` (re-read and recompute), `Conflict` (re-run the discovery step). Watch for
> `truncated=Y` and raise `maxResults`. Leave defaulted parameters alone unless you need the
> non-default behaviour, and re-query rather than reusing an earlier response — the solution is
> edited live.
>
> **Writing safely:** every write tool accepts `checkOnly=true` to preview the changed-file list
> without writing — use it before a broad rename. Writes are atomic and stale-guarded: if a file
> changed on disk since Roslynk read it the whole batch is rejected with `error=Stale` rather than
> clobbering the other edit. Successful writes advance the in-memory model immediately, so
> `get_diagnostics` straight afterwards reflects the change — no reload, no rebuild.
>
> **The edit loop:** make the task's changes → `get_diagnostics` once at the end (bare call, read the counts) → if errors
> appeared, re-call with `includeErrors=true` → fix with `apply_code_fix` at each entry's `line`/`column`
> (or `apply_code_action` with a `candidate` it offers) or `apply_patch` → repeat.

## Run the daemon manually (Linux / WSL / macOS)

For a foreground daemon with visible logs:

```bash
./installer/run.sh
```

Listens on `http://localhost:6502`. Point your MCP client at that URL (streamable HTTP). Ctrl+C stops it.

## Run (Windows)

Foreground dev:

```powershell
dotnet run --project Source/App/Morris.Roslynk.Mcp
```
