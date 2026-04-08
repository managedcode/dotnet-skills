# Roslynator

## Open/Free Status

- open source
- free to use

## Install

Analyzer package:

```bash
dotnet add package Roslynator.Analyzers
```

Optional CLI:

```bash
dotnet tool install -g roslynator.dotnet.cli
```

## Verify First

Before installing anything, check whether the repo already references the analyzer package or already has the CLI:

```bash
rg -n "Roslynator\\.Analyzers" -g '*.csproj' .
dotnet tool list --local
dotnet tool list --global
command -v roslynator
```

## Requirements

- current Roslynator CLI requires a supported modern .NET SDK
- the official CLI docs currently list support for .NET SDK 7, 8, or 9 depending on Roslynator version

## Important Distinction

- the CLI tool itself does not provide the analyzer rules by magic
- build-enforced diagnostics usually come from NuGet packages such as `Roslynator.Analyzers`
- the CLI is most useful for explicit analysis, fix, formatting, and unused-code workflows

## Common Usage

```bash
dotnet build MySolution.sln -warnaserror
roslynator analyze MySolution.sln
roslynator fix MySolution.sln
roslynator format MySolution.sln
roslynator find-unused MySolution.sln
roslynator list-symbols MySolution.sln
roslynator lloc MySolution.sln
```

Useful CI-focused variation:

```bash
roslynator analyze MySolution.sln --severity-level warning
```

Useful scoping flags from the CLI:

- `--projects PROJECT_NAME`
- `--ignored-projects PROJECT_NAME`
- `--include GLOB`
- `--exclude GLOB`
- `--verbosity LEVEL`
- `--properties NAME=VALUE`

## CI Fit

- prefer package-based analyzers as the primary gate
- use `.editorconfig` to configure rule severity
- keep CLI usage explicit if the repo adopts it
- use `analyze` for report-only flows and `fix` only in controlled local or pre-PR cleanup flows
- rebuild and retest after `fix`

## Configuration

Roslynator configuration belongs in `.editorconfig`, for example:

```ini
[*.cs]
dotnet_analyzer_diagnostic.category-roslynator.severity = warning
dotnet_diagnostic.RCS1001.severity = none
dotnet_diagnostic.RCS1036.severity = error
roslynator_refactoring.add_braces.enabled = false
```

## Exit Codes

- `0`: success, or no diagnostics left for the current command
- `1`: diagnostics found, or not all diagnostics fixed
- `2`: error or execution canceled

## When Not To Use

- when the repo already uses enough overlapping analyzers and does not want the extra rule surface
- when the team wants only build-integrated analyzer behavior and has no need for Roslynator CLI workflows

## Sources

- [Roslynator repository](https://github.com/JosefPihrt/Roslynator)
- [Roslynator.Analyzers package](https://www.nuget.org/packages/Roslynator.Analyzers)
- [Roslynator CLI](https://josefpihrt.github.io/docs/roslynator/cli)
- [Roslynator configuration](https://josefpihrt.github.io/docs/roslynator/configuration)
