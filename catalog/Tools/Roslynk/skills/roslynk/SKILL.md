---
name: roslynk
description: How to use the Roslynk MCP tools (mcp__roslynk__*) effectively for .NET work. Use this whenever a Roslynk server is connected and the task touches *.cs, *.cshtml or *.razor files in a solution — checking for compile errors or warnings, finding a symbol's definition, references, callers, or implementations, renaming symbols, applying code fixes or refactorings, finding dead code, or editing those files — even if the user never says "Roslynk". Consult it before reaching for grep, file reads, hand-written edits, or `dotnet build` on such files: Roslynk's semantic tools are faster and more correct for all of those jobs.
---

# Using Roslynk

Roslynk is an MCP server holding a live Roslyn compilation of a .NET solution. The questions you'd normally answer with grep, file reads, or `dotnet build` — "where is this used?", "does it compile?", "rename this safely" — it answers from the compiler's symbol model instead. Text search lies: it hits comments, strings and unrelated same-named members, and misses partial classes, generated code and `#if` branches. Roslynk doesn't.

**Default to Roslynk for all semantic work on `*.cs`, `*.cshtml` and `*.razor` files in the loaded solution.** Plain file tools remain only for what Roslynk doesn't cover: files outside the solution folder, file creation/deletion, and non-compiled files (`.csproj`, `.json`, `.md` — still editable safely via `apply_patch`).

Tools that take a `documentPath` accept `.razor` and `.cshtml` files as well as `.cs`: give positions in the Razor file itself, inside its C# (`@code`/`@functions`, `@{ }`, expressions). Edits are written back to the Razor source in the file's own indentation; renames and other edits that reach a `@bind-X="Expr"` attribute's generated lambdas update the Razor source once at the attribute itself. Analyzer (`IDE*`/`CA*`) fixes are not available in Razor files, and `_Imports.razor`/`_ViewImports.cshtml` usings are never removed. `get_diagnostics` also reports the Razor compiler's own `RZ*` errors against the Razor file. In a `.cshtml` view where a new method cannot be added, `extract_method` suggests `asLocalFunction=true`.

Each tool's exact contract — parameters, output format, limits, error codes — lives in that tool's own MCP description. This skill covers *when* to use the tools and *how* to combine them; [references/tools.md](references/tools.md) holds the deeper reference (exact output envelopes, `#if` projection mechanics) for when a tool's behavior surprises you.

## Solution setup

1. Call `open_solution` with the absolute path to the `.sln`/`.slnx`. It returns immediately and loads in the background; the returned `solutionId` is the handle every other tool needs.
2. While loading, other tools return `error=Indexing` — retry the call shortly, or poll `get_solution_status` (~1s) and report progress. Do **not** fall back to reading or editing files directly; loading finishes within seconds to a minute. If `open_solution` reports `loadDiagnostics` above 0, call `get_solution_status` to read the messages (skipped analyzers, failed project loads): they often explain confusing compiler errors. A missing generator DLL names the project to build: once built (in the configuration its path names, normally Debug), the next call compiles with its generated code, no reload needed.
3. `open_solution` is idempotent and the daemon keeps solutions warm across sessions — calling it again is cheap and safe.
4. Never call `reload_solution` on your own initiative: the file watcher picks up all changes, including your own edits, and a generator project built or rebuilt after load is picked up on the next call. If results look stale (branch switch, `dotnet restore`, SDK/props change, a rebuilt analyzer from a NuGet package or outside the solution), *suggest* it to the user.

## Choose semantic tools over text search

| You want to... | Use | Why not grep / reading files |
|---|---|---|
| Check it compiles / see warnings | `get_diagnostics`, once after the task's edits | far faster than `dotnet build`, but still costly per call |
| Find where a symbol is used, or who calls it | `find_references` / `get_callers` | text search finds false hits and misses partial classes, generated code, `#if` branches |
| See what a member calls (its callees) | `get_callees` | reading the body and tracing each call by eye misses overloads and extension-method bindings |
| Find where a field, property or parameter is read or written (assign, `+=`, `++`, `ref`/`out`, initialisers) | `find_reads` / `find_writes` | text search cannot tell a read from a write; `compound`/`increment`/`ref` appear in both results, so do not add the counts |
| Jump from a usage to its declaration | `find_definition` | compiler binding; correct through overloads and shadowing |
| Know what an expression means before editing it (type, overload chosen, nullability, constant, conversion) | `get_expression_info` | `var`, overloads, implicit conversions and nullable flow are invisible in the text |
| Find implementations of an interface/abstract member | `find_implementations` | compiler's type graph |
| See a type's members (and their local functions) / what a name refers to | `get_members` / `get_symbol` | compiler's view; correct across partial classes |
| Read a member's implementation (incl. a partial method's implementation part) | `get_symbol_body` | returns the declaration verbatim; a path marked `generated=Y` is virtual - read it with this tool, not from disk |
| Explore base/derived types | `get_type_hierarchy` | includes referenced-assembly base types |
| Find a symbol by partial name | `search_symbols` | compiler-declared symbols |
| Rename a symbol everywhere (incl. `.razor`/`.cshtml`) | `rename_symbol` | find-and-replace misses markup and same-named text |
| Rename one parameter of a method/constructor/indexer | `rename_parameter` | also fixes named arguments, `<paramref>` docs and the override/interface family |
| Fix a diagnostic `get_diagnostics` reported (incl. `.razor`/`.cshtml`) | `apply_code_fix` with that entry's id, `line` and `column`; on `error=Conflict`, ask the user which candidate, unless the request names one, then `apply_code_action` with its actionId | hand-editing |
| Apply a refactoring, or choose among fixes at a position | `get_code_actions` + `apply_code_action` | hand-editing |
| Add a parameter to a method and its callers (incl. `.razor`/`.cshtml`) | `change_signature` | hand-editing misses call sites in markup |
| Extract statements or an expression into a new method (incl. `@code`/`@functions`) | `extract_method` | hand-cutting code misses parameters, `ref`/`out` and return values |
| Edit source or text files | `apply_patch` | keeps the in-memory model in sync; stale-guarded |
| Remove unused usings / find dead code | `remove_unused_usings`, `find_dead_code`, `find_dead_conditionals` | eyeballing |

A fix must keep the code's declared intent: never hand-edit or `apply_patch` to make a diagnostic go away, and don't remove an interface, base type, member, attribute or other declaration to silence an error unless the user asks for that.

All name arguments are fully-qualified (`Namespace.Type` or `Namespace.Type.Member`, with an optional parameter-type list to target one overload). A local function is a member of the method declaring it: `Namespace.Type.Method.local`, or `Namespace.Type.Method.outer.inner` when nested; put a parameter list on any segment to pick an overload (`Namespace.Type.Method(int).local`). On `error=Ambiguous` or `error=NotFound` the response lists `candidate=` lines that are exact names the same tool accepts — copy one back verbatim rather than guessing.

## Combine tools, don't chain calls

When you need several facts before making a change, send them as **one `multi_query`** call instead of sequential calls: every operation runs against a single snapshot, so the facts can never disagree with each other. `multi_query` accepts only the read-only query tools, and each operation uses exactly that tool's own parameter names — a wrong key is `error=Invalid` naming the key, never silently ignored. Over 25 operations or the output budget, remaining slots return `error=Truncated` with `truncatedSlots=<n>`: re-send exactly those operations, passing the response's `snapshot=` value as `expectSnapshot`, so a continuation that would straddle a solution change is refused with `error=Stale` instead of mixing states. The envelope format is in the multi_query section of [references/tools.md](references/tools.md).

**Impact analysis** — before renaming a symbol or changing a signature, and whenever someone asks "what uses X", "who calls X", "what breaks if I change X": batch `get_symbol` + `find_references` + `get_callers` + `get_callees` + `find_implementations` + `get_type_hierarchy` in one call, pointing each at the member or type you're changing (`get_callers` takes `methodName`, `get_callees` takes `memberName`, `get_type_hierarchy` takes `typeName`, the others `symbolName`). `get_callers` and `get_callees` are inverses — who calls this member, what does this member call — so alternating them walks a call chain one hop per query. Then follow each reference with `get_symbol_body` to read the calling convention before editing. Answer with Roslynk, never with grep over `*.cs`/`*.cshtml`/`*.razor` — text search both misses real usages and wrongly matches identically-named symbols.

**Understand before editing** — when an edit depends on what an expression *is* (the type behind `var`, which overload a call picks, whether a value can be null at that point, whether a conversion is implicit or user-defined), call `get_expression_info` on its position instead of inferring from text; on a declared name (e.g. the `x` of `var x = ...`) it reports the declared symbol and its type, like an editor hover. It distinguishes the expression's `type` from the `symbol` it binds to — both can matter — and reports a missing fact as `none` rather than guessing; `symbol=none` with `candidateReason`/`candidates` means the call does not resolve. Batch several positions in one `multi_query`.

Results are **snapshots** of a solution that is edited live: re-query after any write rather than reusing an earlier response.

## Preview broad edits before writing

Every write tool takes `checkOnly=true`, which returns the changed-file list without writing anything. Use it whenever a change might be broad — a rename of a widely-used symbol, a whole-solution cleanup — and confirm the scope before applying. Write tools are atomic and stale-guarded: if a target file changed on disk since Roslynk read it, the whole operation is rejected with `error=Stale` and nothing is written — recompute from current state and retry. Writes go straight to disk and advance the in-memory model immediately, so no reload or rebuild is ever needed afterwards.

## Rename parameters with Roslyn

To rename a parameter, call `rename_parameter` with the declaring member's `methodId` (add the parameter-type list when overloaded; a constructor is `Namespace.Type.Type(int)`), the current `parameterName` and `newName`. It updates named arguments at call sites and `<paramref>` docs, and renames the same parameter across overrides, interface members and implementations that use the same old name - check `renamedMembers` and `unchangedRelated` in the response to see what was and wasn't cascaded. On `error=Conflict` pick a name not already used inside the member. Prefer it over `rename_symbol` for parameters, which cannot target a parameter by name.

## Extract methods with Roslyn

To pull code into its own method, call `extract_method` with the selection (1-based; end column exclusive - `endLine+1`, column `1` takes whole lines) and a `methodName`; add `asLocalFunction=true` for a local function. Preview with `checkOnly=true` to see the `signature` and `call` Roslyn chose. It writes nothing when the selection cannot be extracted safely or the result would not compile - read the `NotSupported` reason and adjust the selection (whole statements from one block, or one complete expression) rather than extracting by hand.

## Check diagnostics once a task's edits are done

The core edit cycle: make all the changes the task needs (any write tools) → `get_diagnostics` once at the end (bare call; the header always reports error/warning/info/hidden counts; each call compiles and analyzes everything the edits affect, so do not run it after every edit) → if counts are non-zero, re-call with `includeErrors=true` to see the details → fix each entry via `apply_code_fix` with its id, `line` and `column` → if that returns `error=Conflict` with `candidate=` lines, the diagnostic has several fixes: pick the one whose title matches your intent and pass its actionId (the text before the first comma) to `apply_code_action`; do not retry `apply_code_fix` → repeat until clean. This replaces `dotnet build` during development. Pass `includeAnalyzers=false` for a faster compiler-only pass when style rules don't matter yet.

`find_dead_code` and `find_dead_conditionals` never delete anything — treat the results as candidates (public or reflection-used API can be a false positive), confirm intent with the user before bulk-removing, and hand each reported `loc` span to `apply_patch`.
