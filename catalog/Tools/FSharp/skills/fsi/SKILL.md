---
name: fsi
description: "Use F# Interactive (`dotnet fsi`) for .NET exploration, scriptable experiments, package-backed .fsx workflows, quick data transforms, and reproducible command-line probes. USE FOR: .fsx scripts, F# REPL work, #r nuget references, #load composition, interactive type exploration, and small strongly typed experiments before moving code into a project. DO NOT USE FOR: production application code that needs compiled project structure; C# scripting; long-lived automation better expressed as a normal CLI, test, or build target. INVOKES: run dotnet fsi, edit .fsx scripts, load project or source files, and validate snippets against the target SDK."
compatibility: "Requires a .NET SDK with `dotnet fsi`; NuGet-backed scripts may need package restore and trusted package sources."
---

# F# Interactive

## Trigger On

- the task asks for F# Interactive, FSI, `.fsx`, or `dotnet fsi`
- a quick typed experiment is needed before changing compiled project code
- a script should reference NuGet packages directly with `#r "nuget: ..."`
- an investigation needs quick access to F# type inference, pattern matching, or pipelines
- a repeatable one-file probe is better than a temporary project

## Do Not Use For

- long-lived application code that needs a compiled `.fsproj`
- production automation that should be versioned as a CLI, test project, or build target
- C# script work
- package restore from untrusted sources

## Quick Start

Run an interactive REPL:

```bash
dotnet fsi
```

Run a script:

```bash
dotnet fsi scripts/check.fsx
```

Create a Unix executable script when that fits the repo:

```fsharp
#!/usr/bin/env -S dotnet fsi

printfn "Hello from FSI"
```

```bash
chmod +x scripts/check.fsx
./scripts/check.fsx
```

On Windows, run scripts with `dotnet fsi scripts/check.fsx`.

## Workflow

1. Decide whether the request is a disposable REPL probe, a repeatable `.fsx` script, or code that should be promoted to a compiled F# project.
2. Put repeatable work in an `.fsx` file immediately. Add all required `#r`, `#load`, `open`, input path, and package source directives to the script instead of relying on hidden REPL state.
3. Keep package references pinned when the script should be reproducible, and use only trusted NuGet feeds or local feeds derived from `__SOURCE_DIRECTORY__`.
4. Run the script with `dotnet fsi` from a clean shell, passing the same arguments the user or CI will use.
5. Promote the script to an `.fsproj` when it needs tests, distribution, project references, or long-term CI coverage.

## Current Upstream Notes

- The July 2026 F# Interactive reference keeps `dotnet fsi` as the supported command-line entry point for interactive sessions and `.fsx` scripts; it does not turn hidden REPL state into a reproducible workflow.
- Use repeatable `.fsx` files with explicit `#r "nuget: ..."` and `#load` directives once an experiment affects a repository task; do not rely on hidden REPL state.

## Interactive Session Rules

- End REPL submissions with `;;`.
- Multi-line input is allowed; FSI evaluates when it receives `;;`.
- Previously evaluated values stay in the session, so do not rely on hidden session state in scripts.
- Use `.fsx` files for repeatability once an experiment matters.

```fsharp
let square x = x * x;;

[ 1 .. 5 ] |> List.map square;;
```

## Script Patterns

### Read And Summarize Text

Use ordinary .NET APIs directly from F# scripts.

```fsharp
open System
open System.IO

let summarize path =
    File.ReadLines path
    |> Seq.filter (String.IsNullOrWhiteSpace >> not)
    |> Seq.countBy (fun line -> line.Split(' ')[0])
    |> Seq.sortByDescending snd
    |> Seq.truncate 10
    |> Seq.toList

for key, count in summarize "input.log" do
    printfn $"{key}: {count}"
```

### Write A Checked Output File

Keep script outputs deterministic and fail early when required inputs are missing.

```fsharp
open System
open System.IO

let input = "data/items.txt"
let output = "artifacts/items.normalized.txt"

if not (File.Exists input) then
    failwith $"Missing input file: {input}"

Directory.CreateDirectory(Path.GetDirectoryName output) |> ignore

File.ReadLines input
|> Seq.map (fun line -> line.Trim())
|> Seq.filter (String.IsNullOrWhiteSpace >> not)
|> Seq.distinct
|> Seq.sort
|> fun lines -> File.WriteAllLines(output, lines)
```

### Reference NuGet Packages

Pin package versions for repeatable scripts. Only use package sources the repo trusts.

```fsharp
#r "nuget: Newtonsoft.Json, 13.0.3"

open Newtonsoft.Json

let payload = {| Name = "Ada"; Kind = "sample" |}
let json = JsonConvert.SerializeObject(payload)

printfn $"{json}"
```

Use `#i` only when an additional feed is required. Local feed paths must be absolute; construct them from `__SOURCE_DIRECTORY__` instead of committing personal paths.

```fsharp
let localFeed =
    System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../artifacts/packages")
    |> System.IO.Path.GetFullPath

#i $"nuget: {localFeed}"
```

### Split Scripts With Load

`#load` evaluates another script and exposes it through the generated module name.

```fsharp
// MathHelpers.fsx
let square x = x * x
```

```fsharp
// Check.fsx
#load "MathHelpers.fsx"
open MathHelpers

printfn $"%d{square 12}"
```

## Promote To A Project

Move from FSI to a compiled project when:

- the script has multiple dependencies, tests, or distribution needs
- startup time or restore behavior matters
- C# or other .NET callers need a stable assembly
- the code needs CI coverage beyond a smoke run

Start with:

```bash
dotnet new console -lang "F#" -o tools/Probe
dotnet build tools/Probe/Probe.fsproj
```

## Validate

Use the simplest command that proves the script still runs:

```bash
dotnet fsi scripts/check.fsx
dotnet fsi scripts/check.fsx -- arg1 arg2
```

For scripts that reference packages, run from a clean shell at least once so hidden REPL state cannot mask missing `#r`, `#load`, or `open` directives.

## Sources

- https://learn.microsoft.com/dotnet/fsharp/tools/fsharp-interactive/
- https://learn.microsoft.com/dotnet/fsharp/get-started/get-started-vscode#explore-f-with-scripts-and-the-repl
- https://learn.microsoft.com/dotnet/fsharp/language-reference/fsharp-interactive-options
