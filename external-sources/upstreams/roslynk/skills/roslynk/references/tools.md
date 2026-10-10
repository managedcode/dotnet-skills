# Roslynk tool reference

Deep per-tool reference: the operation-specific contract (exact envelopes, mechanics, edge cases) behind
what each tool's published MCP description summarizes. Read the section you need; SKILL.md carries the
workflows and the tool descriptions carry the concise contract for discovery.

## Contents

- [Shared conventions](#shared-conventions)
- [Lifecycle: open_solution, get_solution_status, reload_solution](#lifecycle)
- [Navigation: find_definition, get_expression_info, get_symbol, get_symbol_body, get_members, search_symbols](#navigation)
- [Relationships: find_references, find_reads, find_writes, get_callers, get_callees, find_implementations, get_type_hierarchy](#relationships)
- [Diagnostics: get_diagnostics](#diagnostics)
- [Razor and CSHTML: how path-based and write tools handle .razor/.cshtml](#razor-and-cshtml)
- [Code actions: get_code_actions, apply_code_action, apply_code_fix](#code-actions)
- [Editing: apply_patch, rename_symbol, rename_parameter, change_signature, extract_method, remove_unused_usings](#editing)
- [Dead code: find_dead_code, find_dead_conditionals](#dead-code)
- [Batching: multi_query](#batching)

## Shared conventions

**Output format.** Compact text outline: `key=value` header lines, a blank line, then a tab-indented body. No blank line means the response is all headers (typically an error). Booleans render `Y`/`N`. Newlines inside a field are literal `\n`. A `status=` header (`Building`|`Faulted`) appears only when the solution is not Ready.

**Body nesting** (multi-symbol tools): project → one line per folder segment → file → namespace → `typeKind,typeName,loc` → `memberKind,memberName,loc`. `kind` ∈ `method|property|field|event|class|struct|interface|enum|delegate`. `loc` is `line:col` or `startLine:startCol-endLine:endCol`, 1-based; multiple locations on one line are pipe-delimited. Names containing commas (e.g. `Dictionary<string, int>`) are single-quoted.

**Error shape** (header-only):

```
error=<Indexing|Faulted|NotFound|Ambiguous|NotSupported|Stale|Invalid|Conflict|Truncated>
errorMessage=<text>
candidate=<name>       (0+ lines: NotFound suggestions / Ambiguous matches)
stale=<path>           (0+ lines, Stale only)
```

**Symbol names.** A name is a fully-qualified name, optionally carrying a parameter-type list for a
method or indexer: `N.T.M`, `N.T.M(int, string)`, `N.T.this[int]`, `N.T.M<T>(T)`. Parameter names,
default values, nullable annotations and `global::` are ignored on input, and fully-qualified parameter
types are accepted, so `M(System.Int32)` and `M(int value = 0)` both resolve `M(int)`. A `ref`/`out`/`in`
modifier is optional but honoured when written. Without a parameter list the name matches every overload
→ `error=Ambiguous`, one `candidate=` per match; every candidate is accepted verbatim by the tool that
emitted it, and the `resolvedSymbol`/`resolvedType`/`resolvedMethod`/`#fullName` a tool echoes is in the
same re-queryable form.

**Local functions** are named as members of the member that declares them, through any nesting of types or
of local functions: `N.Parent.Widget.Method1.localMethod`, and `N.Parent.Widget.Method1.localMethod.inner` for
one declared inside another. The container is the enclosing method, constructor, operator, property or event
(or outer local function); a local function inside a lambda belongs to the member around the lambda. Any
segment may carry its own parameter list, so overloads of the container are told apart:
`N.T.Method2(int).helper` vs `N.T.Method2(string).helper`. Tools emit the fully signed form, with every
container's parameter list, e.g. `N.Parent.Widget.Method1(int).localMethod(string, int).inner(string)`, and
that form is what an `Ambiguous` candidate looks like. A bare name such as `inner` also resolves. Outlines
report a local function's kind as `localfunction` and nest it under its containing member.

**Truncation.** Known-total tools emit `count=<total>` and `truncated=Y` when capped; unknown-total scans (`find_dead_code`) emit only `truncated=Y`.

**Defaults.** Don't pass a value for a defaulted parameter unless you need the non-default behavior.

**Generated locations.** A location declared by a source generator has a virtual path that does not exist on disk (unless `EmitCompilerGeneratedFiles` is set). `get_symbol`, `get_symbol_body` and `find_definition` add `generated=Y` and `generator=<generator type>` headers after `loc`; in nested bodies (`get_members`, `find_references`, `find_reads`, `find_writes`, `get_callers`, `get_callees`, `find_implementations`, `search_symbols`) the file-name line ends `,generated=Y`. Read such code with `get_symbol_body`, not from disk; edit tools keep rejecting generated paths. Razor's generated `.g.cs` documents are not marked. `search_symbols` does not currently find symbols declared by generators.

**`#if` projections.** Symbol tools (find_definition, find_implementations, get_members, get_symbol, get_symbol_body, get_type_hierarchy, find_references, find_reads, find_writes, get_callers, get_callees, rename_symbol, search_symbols) also cover inactive `#if`/`#else` branches: Roslynk builds derived compilations toggling each uniformly-defined preprocessor symbol, and unions/dedupes results.

## Lifecycle

### open_solution
`solutionPath` (required): absolute path to `.sln`/`.slnx`.
Returns `solutionId` (= the path you passed), `status`, `projects`, `loadDiagnostics` (a count; `get_solution_status` lists the messages), then one `<projectPath>,<documentCount>` line per project. Loads in the background; idempotent; missing file → `NotFound`, failed load → `Faulted`. While loading, other tools return `error=Indexing` — retry them shortly; never fall back to raw file edits.

### get_solution_status
`allLoadDiagnostics` (optional, default false). Lists every solution loaded by the daemon (daemon-wide, not session-scoped): `<solutionId>,<status>,<loaded>/<total>` per line. `total` is `?` until the first load completes. Status values: `Building`, `Updating`, `Ready`, `Faulted`.

When the load reported messages (the count `open_solution` shows as `loadDiagnostics`: a skipped or unloadable analyzer/generator such as an unbuilt generator DLL, a project that failed to evaluate, an SDK that was not found), each is a tab-indented `loadDiagnostic=<message>` line under its solution line, shown only once that solution is `Ready` (never while `Building`, `Updating` or `Faulted`); a solution with none stays one line. These often explain a batch of confusing compiler errors. Messages are sorted, and without `solutionId` only the first 20 per solution are listed, followed by `\tloadDiagnosticsTruncated=<n>` for the rest. With `solutionId` only that solution is listed, with every message. A `solutionId` that is not loaded → `error=NotFound`. Not batchable in `multi_query`.

```
C:\repo\GeneratorSolution.slnx,Ready,2/2
	loadDiagnostic=Skipped unresolved analyzer in project 'ConsumerLib': 'C:\repo\GeneratorLib\bin\Debug\netstandard2.0\GeneratorLib.dll' does not exist, so the generated code of project 'GeneratorLib' is unavailable. Build project 'GeneratorLib' in the configuration this path names; the next call picks it up without a reload.
```

A missing generator DLL (typically a generator project referenced as an analyzer and built only in Release, while Roslynk loads Debug) is reported with the project that builds it. Every data tool call compares the file stamps of such DLLs, and of every analyzer DLL a project of the solution builds, with the ones loaded: a DLL built or rebuilt since is attached before the call reads, so that call already sees the new generated code, wherever the DLL lives (`bin`, an artifacts directory, outside the solution). No reload is needed. The message goes away once the DLL exists; a DLL that then fails to load is reported as `Analyzer load failed for '<path>': ...` until a later build loads.

### reload_solution
`solutionId` (required). Forces a from-disk MSBuild re-evaluation; the old snapshot keeps serving as `Building` until the fresh one is ready. Manual backstop for missed watcher events (network/WSL filesystems), `dotnet restore`, SDK/`global.json`/`Directory.Build.props` changes, branch switches, or retrying a `Faulted` load. **Never call it unless the user explicitly instructs you to** — suggest it instead.

## Navigation

### find_definition
`solutionId`, `filePath` (absolute or solution-relative), `line`, `column` (1-based). Position-based go-to-definition. Returns `#fullName`, `#kind`, plus `#project`/`#path`/`#loc` for source symbols or `#assembly=` for metadata-only. (Note the `#`-prefixed header style, unique to this tool family.)

### get_expression_info
`solutionId`, `filePath` (`.cs`, `.razor` or `.cshtml`; absolute or solution-relative), `line`, `column` (1-based, anywhere inside the expression; the position just after a token counts as that token). Returns compiler facts about the expression at the position. Batchable in `multi_query`.

The expression is the innermost one containing the token, widened to what an edit means: a member name reports its whole member access (`a.B`), a called name its invocation (`a.B(c)`, so `type` is the return type and `symbol` the selected overload), and a created type its object creation (`new T(...)`, symbol = constructor). A position on the name a declaration introduces (local, parameter, field, property, event, method, type, foreach/catch/pattern variable) reports the declared symbol instead, like an editor hover: `declaration=Y`, `expr` is the name, `exprKind` the declaring syntax kind (e.g. `VariableDeclarator`, `MethodDeclaration`), `type` the declared type (a method's return type; `none` for a type declaration), `nullability` the declared annotation with the initializer's flow state (`None` without an initializer), `constant` for a `const`, then the usual symbol keys; no `convertedType`/`conversion`. A position on a keyword, punctuation, comment or whitespace is `error=NotFound`; a line/column outside the file or a file not compiled in the solution is `error=NotFound`; a loading solution is `error=Indexing`. Positions inside an inactive `#if` branch are tried in each projection.

Output is header only, in this order (optional keys only when applicable):

| Key | Meaning |
|---|---|
| `expr` | Expression text, whitespace collapsed, cut at 200 chars with `...` |
| `exprKind` | Roslyn `SyntaxKind` (e.g. `InvocationExpression`, `IdentifierName`) |
| `declaration` | `Y` only when the position is on a declared name (see above) |
| `path`, `loc` | Solution-relative file and `startLine:startCol-endLine:endCol` (Razor source positions for `.razor`/`.cshtml`) |
| `type` | Expression's natural type, fully qualified with C# keywords and `?` for annotated references; `none` for a method group, namespace or typeless expression (`null`, lambda) |
| `convertedType` | Only when the target type differs (implicit conversion, target typing) |
| `nullability` | `<annotation>/<flowState>`: annotation `None|NotAnnotated|Annotated`, flow state `None|NotNull|MaybeNull`; `None/None` when nullable analysis is off. Flow state reflects prior null checks |
| `conversion` | Only when converted type is known and not a plain identity: `implicit`/`explicit` followed by kinds (`numeric`, `nullable`, `reference`, `boxing`, `constant`, `user-defined <operator>`, `target-typed-new`, `collection-expression`, ...), `identity`, or `none` when no conversion exists |
| `constant` | Compile-time constant as a C# literal (`40`, `"x"`, `null`), else `none` |
| `symbol` | Bound symbol: generic definition / static extension form, re-queryable with name-based tools (`get_symbol`, `find_references`, ...); locals, parameters and range variables report their bare name; `none` when nothing binds |
| `candidateReason`, `candidates` | With `symbol=none`: Roslyn's reason (e.g. `OverloadResolutionFailure`) and pipe-delimited candidate names |
| `symbolKind` | Same kind words as other tools (`method`, `property`, `field`, `local`, `parameter`, `class`, ...) |
| `instantiation` | Constructed generic or reduced extension form, e.g. `Formatter.Echo<string>(string)` |
| `origin` | `source` with `symbolPath`/`symbolLoc` (`line:col`), or `metadata` with `assembly` |
| `doc` | XML-doc `<summary>` as plain text (cref short names), cut at 400 chars; absent when none is available |

### get_symbol
`solutionId`, `symbolName` (FQN, any symbol kind including members). Unambiguous source match → `#project`/`#path`/`#loc` headers + body containing the verbatim declaration text cut before the body (brace/`=>` excluded). Metadata symbol → `#source=metadata`, `#kind`, `#signature`, `#assembly`. Ambiguous → `error=Ambiguous` with one `candidate=` per match. Preferred over reading a file to identify a symbol.

### get_symbol_body
`solutionId`, `symbolName` (FQN, any declared symbol kind), `includeLeadingTrivia` (default false). Returns the symbol's **complete source text including its body**, verbatim — original indentation, line endings and spacing, no reformatting or truncation. Headers `#project`/`#path`/`#loc`, a blank line, then the text. `includeLeadingTrivia=true` extends the text (and the `loc`) back over the declaration's comments, XML docs and directives. A partial type, or a partial method/property/event, returns `#parts=<n>` and one `part=<n>,project=...,path=...,loc=...` line per block instead; a partial member's parts are its defining declaration then its implementation, including one supplied by a source generator (`[GeneratedRegex]`, `[LoggerMessage]`, `[LibraryImport]`), whose part line ends `,generated=Y,generator=<type>`. Overloads (or any name matching several distinct symbols) — `error=Ambiguous`, resolved by retrying with a candidate's parameter-type list; a metadata/referenced-assembly symbol — `error=NotSupported` (use `get_symbol` for its signature). Prefer this over reading or grepping the file to see what a member does.

### get_members
`solutionId`, `typeName` (FQN), `includeInherited` (default false), `nameFilter` (default null; trailing `*` = prefix match, otherwise case-insensitive substring), `includeMethods`/`includeFields`/`includeProperties`/`includeEvents`/`includeNestedTypes` (all default true). Lists all members including private, grouped by declaring file (`<metadata>` bucket for referenced-assembly types). Member lines: `kind,name,loc[,paramType|paramType|...]` (param list only for methods with parameters). Returns declarations + spans, not bodies — use `get_symbol_body` for a member's body. Each member's local functions are listed beneath it in declaration order, one level deeper (`localfunction,<name>,<loc>,<paramTypes>`), a local function declared inside another one level further.

### search_symbols
`solutionId`, `query` (case-insensitive substring), `maxResults` (default 50). Source-declared symbols only (no metadata), local functions included. Matched members nest under their type, and a local function under its containing member (`localfunction` kind); a type's `loc` appears only if the type itself matched.

## Relationships

### find_references
`solutionId`, `symbolName` (FQN), `maxResults` (default 100). Compiler-accurate: skips comments, strings, unrelated same-named members. References grouped file→namespace→type→member; pipe-delimited `loc`s on one line for multiple hits. Deduped across `#if` projections. `count=`/`truncated=Y` when capped. Prefer this over text search for usages in `*.cs`, `*.cshtml` and `*.razor`; combine with get_callers/find_implementations via multi_query for impact analysis.

### find_reads / find_writes
`solutionId`, `symbolName`, `maxResults` (default 100). Target a field or property by FQN (`N.T.Field`), or a parameter as its containing method, constructor, indexer or local function's FQN + `:` + parameter name (`N.T.Method:value`, `N.T.Method(int):value`; the member part follows the usual overload grammar). Local variables are not supported. Same body as find_references, but one location per leaf with the access kind appended: `<memberKind>,<memberName>,<loc>,<accessKind>`.

`accessKind`: `read`, `assign` (`=`, including deconstruction targets), `compound` (`+=`, `??=`, ...), `increment` (`++`/`--`), `ref` (`ref x` argument, `ref x` expression, `&x`), `out`, `init` (field/property declaration initialiser, object or `with` initialiser, attribute named argument, or an assignment to the constructor's own field/property inside its constructor; a lambda inside the constructor is `assign`). find_reads returns `read`, `compound`, `increment`, `ref`; find_writes returns everything except `read`. **`compound`, `increment` and `ref` appear in both tools with the same kind** — do not add the two counts. `nameof(...)`, doc crefs and named arguments (`M(value: 1)`) are omitted from both.

`count=`/`truncated=Y` when capped, as find_references. Errors: `NotFound` (for an unknown parameter, candidates list the member's real parameters), `Ambiguous` with candidates, `NotSupported` (method, type, event, local variable), `Indexing`. Coverage: all loaded target frameworks plus derived compilations that each toggle one uniformly-defined/undefined preprocessor symbol; a branch reachable only with several symbols toggled together, or guarded by a symbol defined in only some projects, is not analysed. Both are available in `multi_query`.

### get_callers
`solutionId`, `methodName` (FQN). Callers grouped file→namespace→containing type→calling member with the caller's declaration `loc`. Target one overload by writing its parameter-type list.

### get_callees
`solutionId`, `memberName` (FQN), `excludeExternal` (default false). The inverse of get_callers: every method, constructor, property/event accessor and user-defined operator or conversion the member's code calls. Resolved from the compiler's operation model, so each call names the exact overload: a property read reports `get_X`, an assignment `set_X`, a compound assignment or increment both; `new T()` reports T's constructor; `e += h` reports `add_E` (`remove_E` for `-=`). Also reported: user-defined operators and conversions (including `+=` and `++`), the `Dispose`/`DisposeAsync` a `using` runs, and a method group passed as a delegate (`xs.Select(Helper)`). Only calls the source spells out are listed: the plumbing the compiler inserts for `foreach` (`GetEnumerator`, `MoveNext`, `get_Current`) and `await` (`GetAwaiter`, `get_IsCompleted`, `GetResult`) is not, though an explicit `.GetAwaiter()` call is. Also not reported: calls inside deconstruction assignments and interpolated-string handlers.

The member may be a method, constructor, operator, property, indexer, event or field. What counts as its code: a property or indexer is its accessor bodies or expression body (plus an auto-property initializer), a field is its initializer, and a constructor also includes the field and property initializers it runs (instance ones, or static ones for a static constructor; none when it chains to `this(...)`). Lambdas and local functions declared inside the member are part of it. A member with no code in the solution (an abstract or interface declaration, an auto-property without initializer, or one in referenced metadata) calls nothing. Target one overload by writing its parameter-type list.

Output: callees in the solution use the get_callers outline (project → folder → file → namespace → type → `kind,name,loc`). Callees declared in a referenced assembly (BCL, NuGet) sit under one top-level `<external:AssemblySimpleName>` line per assembly (`<external>` if the assembly is unknown), then namespace → type → leaf, with the leaf's `loc` field present but empty. `excludeExternal=true` leaves them out. A leaf gets a fourth field only when the callee's own type declares more than one method of that name (inherited overloads do not count): its declared parameter types, pipe-delimited, minimally qualified with nullable annotations and without `ref`/`out`/`in`, a type containing a comma single-quoted; `()` for the parameterless overload. Every callee is reported as declared: generic instantiations (`List<int>.Add`, `To<string>`) collapse to their definition, a classic extension method lists its `this` parameter, and a C# 14 `extension(...)` block member lists only the parameters declared on it.

```
resolvedSymbol=MyApp.Report.Save(string, int)

<external:System.Console>
	System
		class,Console
			method,WriteLine,,int
			method,WriteLine,,string?
<external:System.Runtime>
	System
		class,String
			method,get_Length,
		struct,Int32
			method,ToString,,()
MyApp
	MyApp
		Audit.cs
			MyApp
				class,Audit
					method,Log,5:21
		Tally.cs
			MyApp
				class,Tally
					method,Add,5:14,int
					method,Reset,7:14,()
```

### find_implementations
`solutionId`, `symbolName` (FQN of interface, abstract member, or virtual member). Implementors/overrides across all projections, deduped.

### get_type_hierarchy
`solutionId`, `typeName` (FQN). Up to three sections — `base`, `interfaces`, `derived` (omitted when empty) — entries `typeKind,FQN`.

## Diagnostics

### get_diagnostics
`solutionId`, `includeErrors`/`includeWarnings`/`includeInfo`/`includeHidden` (**all default false**), `includeAnalyzers` (default true; `false` = faster compiler-only pass).

**Costly:** it compiles and analyzes every project the edits since the last call affect (seconds on a large solution), so make all of a task's edits first and call it once at the end rather than after every edit. Header always carries `errors=`, `warnings=`, `infos=`, `hidden=` counts regardless of include flags, so filtering is never silent. Body (per included severity) nests file→severity→`<id>,<line:col>,<message>`. Ids that exist only to trigger a code fix (they carry no message and accompany a public rule, as IDE0005's does) are not listed; fix the public id instead. Multi-targeted projects report diagnostics across their loaded target frameworks. The Razor compiler's own diagnostics (`RZ*`, e.g. `RZ1006` for an unclosed `@code` block) are reported against the `.razor`/`.cshtml` file with Razor-source positions, once even for a multi-targeted project (not when the Razor compiler cannot be loaded and the last build's generated output is used instead). Results are cached per `includeAnalyzers`: a repeat call with no change in between is near-instant, and after a change only the edited projects and the projects depending on them are recomputed. This replaces `dotnet build` for correctness checking.

## Razor and CSHTML

Every tool that takes a `documentPath` (`get_code_actions`, `apply_code_fix`, `extract_method`, `remove_unused_usings`) accepts `.cs`, `.razor` and `.cshtml` files, and every write tool edits `.razor`/`.cshtml` sources, including `apply_code_action`, `change_signature`, `rename_symbol`, `rename_parameter` and `apply_patch`. Roslyn only analyses C#, so for a Razor file the tools work on the C# the Razor compiler generates from it and write the result back to the Razor file. The generated `.g.cs` is never written.

- **Positions** (`line`/`column`, selections) are given in the `.razor`/`.cshtml` file. They must fall inside C# the compiler copied from the file: an `@code`/`@functions` block, an `@{ }` block, an inline expression or a directive. Markup is `error=NotSupported`.
- **Mapping back.** An edit is written to the Razor file only when it lies inside those C# regions; code Roslyn formats is re-indented to the file's own indentation, and lines it did not touch are kept verbatim. Edits to a `@bind-X="Expr"` expansion's hidden scaffolding (the setter lambdas and `ValueExpression` lambda the compiler derives from the one source span) are copies of the edit already made at the mapped attribute-value span (`@bind-X`, `:get`/`:set`/`:after`, `@bind`), so renames of bound members update the `.razor` source once at the user-written span. An edit to generated scaffolding with no attributable Razor source (for example a `using` or member added to the generated class) is `error=NotSupported`. Razor text that no longer matches the generated code is `error=Conflict`. Nothing is written in either case.
- **Analyzers do not run on Razor-generated code**, so a Razor file offers compiler-diagnostic fixes (e.g. `CS0219`) and refactorings, but no `IDE*`/`CA*` fixes. The exception is unnecessary usings: see `remove_unused_usings` and `apply_code_fix`.
- **Imports files** (`_Imports.razor`, `_ViewImports.cshtml`) apply to every component or view beneath them. Using removal never edits them.
- **Freshness.** An edit to an existing `.razor`/`.cshtml` file (yours or the user's) is applied in place: the Razor compiler reruns incrementally for that project and the projects using it, with no solution reload. A C# edit that changes declarations (not just method bodies) reruns it too, so generated code follows a code-behind change such as a renamed `[Parameter]`; this holds for every write tool, `apply_patch` included. Adding or deleting a Razor file reloads the solution on the next call. Multi-targeted projects get generated code for every target framework.
- **Some `.cshtml` views cannot take a new method.** Roslyn will not add a member where the generated view class hides the insertion point. `extract_method` then returns `error=NotSupported` and suggests `asLocalFunction=true`, which still works.

## Code actions

### get_code_actions
`solutionId`, `documentPath` (.cs, .razor or .cshtml, absolute or solution-relative), `line`, `column` (1-based), `endLine`/`endColumn` (optional, for a selection span). Returns lines `<actionId>,<kind>,<diagnosticId> <title>`; a grouped action (such as the choices under "Generate type") is listed as its individual actions; `kind` ∈ `Fix|Refactoring`; `diagnosticId` is `-` for refactorings. Fixes cover analyzer diagnostics as well as compiler ones, so `IDE*` and `CA*` ids appear here when the project references the analyzer that reports them. Capped at 50. `actionId` is opaque — pass it back verbatim. In a `.razor`/`.cshtml` file the position must be inside C# (see [Razor and CSHTML](#razor-and-cshtml)); a position in markup → `NotSupported`. Actions are listed before their edits are computed, so an action whose edits cannot be mapped back to the Razor file is only refused when applied.

### apply_code_action
`solutionId`, `actionId` (from get_code_actions), `checkOnly` (default false). The action is re-resolved at apply time, not cached — if the code changed since discovery, `error=Conflict`: re-run `get_code_actions`. Malformed id → `error=Invalid`. Returns `applied`, `action`, changed files. For an action discovered in a `.razor`/`.cshtml` file the edits are written to the Razor source; an unmappable edit → `NotSupported`, mismatched Razor text → `Conflict`, nothing written. External edit since load → `Stale`.

### apply_code_fix
`solutionId`, `documentPath` (.cs, .razor or .cshtml), `diagnosticId` (a compiler id such as `CS0219`, or an analyzer id such as `IDE0005`), `line`, `column` (**required**, 1-based), `checkOnly` (default false). Quick path from `get_diagnostics`: pass an entry's id and its `line:col` unchanged, and the diagnostic with that id whose span contains the position (start and end included) is fixed, without an actionId round-trip. Other occurrences of the same id are left alone.

- No diagnostic with that id at the position → `NotFound`; `line` or `column` below 1 → `Invalid`; no registered fix → `NotSupported`; fix produced no changes → `Conflict` (no candidates); external edit since load → `Stale`.
- **Several fixes → hand off to `apply_code_action`.** When the diagnostic has more than one distinct fix (e.g. `CS0535`: implement interface / implement all members explicitly; `CS0246`: add a `using` / fully qualify / generate the type), nothing is written and the result is `error=Conflict` with one `candidate=<actionId>,Fix,<diagnosticId> <title>` header per fix. Show the user the candidate titles and ask which to apply, unless their request clearly names one, then call `apply_code_action` with its actionId (the text before the first comma). Do not call `apply_code_fix` again for it: it will return the same Conflict. `checkOnly` returns the same Conflict.

  ```
  error=Conflict
  errorMessage=CS0535 at 9:22 has 2 fixes; apply the chosen candidate's actionId with apply_code_action.
  candidate=eyJEb2N1...,Fix,CS0535 Implement interface
  candidate=eyJEb2N1...,Fix,CS0535 Implement all members explicitly
  ```

In a `.razor`/`.cshtml` file the position is in the Razor file, only diagnostics located in that file count, and the fix is written to the Razor source (unmappable → `NotSupported`, mismatched text → `Conflict`). `CS8019`/`IDE0005` remove the unnecessary `@using` on that line, the same analysis as `remove_unused_usings`; a used `@using` there → `NotFound`.

Analyzer ids are only reported — and so only fixable — when the project actually references the analyzer. `IDE*` rules come from the CodeStyle analyzers, which a project pulls in with `EnforceCodeStyleInBuild` or an explicit `Microsoft.CodeAnalysis.CSharp.CodeStyle` package reference; an SDK copy built against a newer compiler than Roslynk's is skipped.

## Editing

All write tools: `checkOnly=true` previews changed files without writing; writes are atomic (all-or-nothing) and stale-guarded (on-disk content re-hashed against the loaded snapshot; mismatch → `error=Stale`, nothing written). Successful writes advance the in-memory model immediately. Response shape: `applied=Y|N` + tool-specific headers + changed-file list grouped by project.

### apply_patch
`solutionId`, `patch` (git unified diff: `---`/`+++`/`@@` hunks, one or more file sections), `baseVersions` (optional list of `{path, version}` from a prior read, for explicit optimistic concurrency), `checkOnly`.

- Hunks are **content-anchored**; line numbers in `@@` headers are untrusted and may be omitted (`@@`). Each hunk must match exactly one place — include enough context. Ambiguous or unmatched → `error=Conflict` naming the file and reason.
- Edits any existing text file inside the solution folder. Compiled `.cs` and `.razor`/`.cshtml` documents (regular and additional) are folded into the in-memory model; other files (`.csproj`, `.json`, `.md`, ...) are written to disk only. Creation, deletion, binary files, `obj`/`bin` paths, and paths outside the solution folder are rejected before any hunk is attempted: `error=NotSupported` with `rejected=<path>` lines. A path naming no file → `error=NotFound` (the message names the solution folder that paths are relative to); a suffix matching several distinct files → `error=Ambiguous` with `candidate=<solution-relative path>` lines. Mixed reasons give one error (NotFound, else Ambiguous, else NotSupported) whose message explains each, with every path in `rejected=`. Encoding (UTF-8/UTF-16 BOM) is preserved.
- Path resolution: absolute → solution-relative → unique suffix match across solution documents (regular and additional; a multi-targeted file counts once, so a repo-root-relative path such as `src/Multi/Api.cs` resolves); a path not in the model falls back to solution-folder-relative.
- The stale-write guard runs even without `baseVersions`.

### rename_symbol
`solutionId`, `symbolName` (FQN), `newName` (must be a valid C# identifier), `checkOnly`. Renames across partial classes, all `#if` projections, all TFMs, and Razor: edits computed against generated `.g.cs` are mapped back through `#line` directives and written to the real `.razor`/`.cshtml` (covering `@code` blocks, markup expressions, component attributes in other components, and `@bind-X="Expr"` bindings — the compiler's hidden setter/`ValueExpression` expansions collapse into the one attribute-value edit). Unverifiable mapping aborts the whole rename before writing. Ambiguous → `error=Ambiguous` with candidates; retry with one verbatim to rename a single overload. A file the rename changes that was edited on disk, or in the loaded model after the rename was computed → `error=Stale` with a `stale=` path; nothing is written. Edits to other files made meanwhile are kept.

### rename_parameter
`solutionId`, `methodId` (FQN of the method, local function — see [Symbol names](#shared-conventions) — constructor — written `Namespace.Type.Type(...)` — or indexer; add a parameter-type list to pick one overload), `parameterName` (existing name), `newName` (valid C# identifier, different from `parameterName`), `checkOnly`. Uses Roslyn's semantic rename on the parameter: declaration, body uses, named arguments at call sites and `<paramref>`/`<param>` doc references; strings and comments untouched. Covers all `#if` projections, TFMs and Razor like `rename_symbol`. Cascades across the override/interface group (overridden/overriding members, interface members and implementations, both partial-method parts) when the related declaration is in source and its parameter at the same position has the same old name; related members with a different name, or in metadata, are left alone and listed. Headers: `applied`, `resolvedMethod` (before the rename), `parameter` (old name), `renamedMembers` (declarations renamed, including the target), `unchangedRelated` (`; `-separated, omitted when empty), then changed files. Errors: invalid/unchanged `newName` → `error=Invalid`; overloads → `error=Ambiguous` with candidates; unknown member → `error=NotFound` with suggestions; unknown `parameterName` → `error=NotFound` listing the member's parameters; `newName` already declared in a renamed member (parameter, local, local function, lambda/query variable, type parameter) → `error=Conflict` naming the member; not a method/constructor/indexer, positional record constructor, or metadata-only member → `error=NotSupported`; changed on disk → `error=Stale`. Nothing written on any error. Not available in `multi_query`.

### change_signature
`solutionId`, `methodId` (FQN), `parameterType` (e.g. `System.Threading.CancellationToken`), `parameterName` (valid identifier), `defaultValue` (**required** — keeps the parameter optional, e.g. `default`, `null`, `0`), `callSiteArgument` (optional; when given, threaded into every call site as a named argument), `checkOnly`.

v1 scope: appends one optional parameter to one ordinary method or local function. `NotSupported` for virtual/override/abstract methods, interface members and implementations, partial methods, `params` methods, constructors/operators/accessors. An overloaded method is targeted by its parameter-type list; a bare name → `Ambiguous`. Only true invocation call sites are updated — method groups, `nameof`, and `cref` are left alone (still valid because the parameter is optional). Returns `updatedCallSites` count. Razor: a method declared in a `.razor`/`.cshtml` `@code` block, and call sites in `@code` or markup expressions, are mapped back through `#line` directives and written to the Razor source; an unmappable edit → `NotSupported`, mismatched Razor text → `Conflict`, nothing written. External edit since load → `Stale`.

### extract_method
`solutionId`, `documentPath` (.cs, .razor or .cshtml, absolute or solution-relative), `startLine`, `startColumn`, `endLine`, `endColumn` (1-based, in that file; the end column is exclusive, so `endLine+1`, column `1` selects through the end of `endLine`), `methodName` (optional; defaults to Roslyn's own name, e.g. `NewMethod` or one derived from the code), `asLocalFunction` (default false), `checkOnly` (default false).

Runs Roslyn's Extract Method refactoring (the same provider behind get_code_actions' "Extract method"/"Extract local function"), so parameters, `ref`/`out`, the return value, `static`/`async` modifiers and the call-site replacement all come from Roslyn's data-flow analysis - never text splicing. Whitespace at either end of the selection is ignored, so whole-line selections work. Roslyn may widen a partial expression to the enclosing complete expression. When `methodName` is given, the extracted symbol is renamed semantically before anything is written. Roslyn formats the new method and the edited call site with the project's formatting options (`.editorconfig`); the rest of the file is untouched.

Returns `applied`, `method` (final name), `symbolName` (the new method's full name, ready for the name-based tools — for a local function it carries its containers, e.g. `N.T.M(int).NewMethod(string)`), `kind` (`Method`|`LocalFunction`), `signature` (the declaration header on one line, e.g. `private static int Total(int a, int b)`), `call` (the statement now containing the call), then the changed files. `checkOnly=true` returns the same preview without writing.

Nothing is written on any failure:
- Selection Roslyn cannot extract (spans members, partial statements, jumps out of the selection, `yield`, ...) → `NotSupported` with the constraints.
- Roslyn flags the extraction as possibly changing behavior → `NotSupported` with Roslyn's warning.
- The result would add compile errors to an edited file (e.g. a `methodName` equal to the type name, CS0542) → `NotSupported` listing up to five `id: message (line n)` entries. Errors already present are not counted.
- The call would bind to another member, or the rename produced no declaration of that name → `Conflict`.
- An edited file changed since the extraction was computed (in memory or on disk) → `Stale` with `stale=<path>`.
- Unknown document → `NotFound`; generated `.g.cs` (incl. Razor output; pass the `.razor`/`.cshtml` source instead) or non-C# → `NotSupported`; out-of-range, empty or reversed selection, or an invalid/keyword `methodName` → `Invalid`.
- `.razor`/`.cshtml`: a selection outside C# (in markup) → `NotSupported`; an edit that cannot be mapped back to the Razor file → `NotSupported`, mismatched Razor text → `Conflict`. Where Roslyn cannot add a new method to a `.cshtml` view (the generated class hides the insertion point), the error suggests `asLocalFunction=true`, which extracts into the containing member instead. The new code is written in the file's own indentation.

Prefer this over get_code_actions + apply_code_action for extraction: it is deterministic (no action-id round trip, no title matching), names the method in one step, and checks the result compiles. Not available in multi_query (it is a write tool).

### remove_unused_usings
`solutionId`, `documentPath` (optional .cs, .razor or .cshtml; omit to clean the whole solution, Razor files included), `checkOnly`. CS8019 finds the files to touch; each one is then rewritten by Roslyn's own IDE0005 fix, which preserves the surrounding trivia, falling back to a syntactic removal when the IDE0005 analyzer is not referenced. Nothing to remove → `applied=N`, `removedCount=0` (not an error). Safe to re-run (idempotent). External edit since load → `Stale`.

Razor: the compiler never reports CS8019 in generated code, so each `.razor`/`.cshtml` file's generated C# is checked as ordinary code, and every unnecessary `@using` line written in the file itself is deleted. Directives in imports files (`_Imports.razor`, `_ViewImports.cshtml`) are shared by other components and views, so they are never removed, even when unused by the file being cleaned.

## Dead code

### find_dead_code
`solutionId`, `scope` (optional FQN prefix, e.g. `MyApp.Services` — use it on large solutions; the scan runs find-references per symbol), `includePublic` (default false — public/protected API excluded unless set), `maxResults` (default 50).

Leaf lines: `memberKind,memberName,loc,confidence,reason` with confidence `High|Medium`. Automatically excludes interface implementations, virtual/override/abstract chains, test-attributed members (xUnit/NUnit/MSTest), generated code, and DI/reflection-attributed members (`[Export]`, `[ImportingConstructor]`). The `loc` is the full declaration span, ready to pass to `apply_patch` for removal — this tool never deletes anything itself. Treat results as candidates, not verdicts.

### find_dead_conditionals
`solutionId` only. Header `#deadConditionals=<n>`; body per file: `<line:col>,<directive>,<condition>` with directive ∈ `if|elif|else` (condition `(else)` for `#else`). Flags branches never compiled under any configuration Roslynk actually loaded (each project's defined symbols, and that set minus DEBUG, across TFMs). A branch used only by a configuration not loaded (CI-injected define, missing workload) is a false positive — treat as "possibly dead".

## Batching

### multi_query
`solutionId`, `operations` (1-25 items), optional `expectSnapshot`.

Each operation is `{ "tool": <name>, "arguments": { ... } }` where `tool` is one of the 15 read-only query tools (get_symbol, get_symbol_body, get_members, find_definition, find_implementations, find_references, find_reads, find_writes, get_callers, get_callees, search_symbols, get_type_hierarchy, find_dead_code, find_dead_conditionals, get_expression_info) and `arguments` uses exactly that tool's single-call parameter names - unknown or misspelled keys are rejected (`error=Invalid` naming the key), never ignored; omitted parameters take the tool's declared defaults. The schema's `tool` enum lists the legal names, so a write tool, get_diagnostics or get_solution_status is unrepresentable and fails the whole call at binding (`error=Invalid` naming the offending value and the permitted set).

**Impact analysis.** To answer "what uses X / who calls X / what breaks if I change X", batch find_references (`symbolName`), get_callers (`methodName`), get_callees (`memberName`), find_implementations (`symbolName`) and get_type_hierarchy (`typeName`) in one call instead of grepping - parameter names are each tool's own and a wrong key is an `Invalid` slot. get_callers and get_callees are inverses: who calls this member, and what does this member call - together they trace a call chain one hop per query.

Everything runs against ONE snapshot of the solution, so results inside one response can never disagree with each other. The response is one envelope, not JSON:

```
operations=<n>
snapshot=<32-hex id of the snapshot every slot was computed against>
boundary=<32-hex, fresh per request>

--<boundary>
slot=<i> tool=<tool name>

<that tool's own output format, verbatim - headers, blank line, tab-indented body, or its error= block>

--<boundary>
...

--<boundary>--
```

A failing operation does not abort the batch: its slot carries its normal `error=` block (NotFound/Ambiguous/Invalid/...) and the other slots still deliver. Slot bodies are never cut mid-output.

**Truncation and continuation.** More than 25 operations, or a total output over the response budget, truncates: unexecuted operations still get their numbered slot carrying `error=Truncated`, and the header carries `truncatedSlots=<n>`. To continue, re-send exactly the operations whose slots were Truncated - the slot meta line gives the index and tool name, but NOT the arguments, so rebuild the continuation from your own copy of the request (the server does not echo them back). Pass the first response's `snapshot=` value as `expectSnapshot` when you do: if the solution changed between the calls (another client, or the user saving a file) the continuation is refused with `error=Stale` naming both ids plus a `snapshot=<current id>` header - re-run the whole batch instead of stitching two generations. Consistency is guaranteed within one response, never across two. `get_diagnostics` and `get_solution_status` are deliberately not multi-queryable - call them directly.
