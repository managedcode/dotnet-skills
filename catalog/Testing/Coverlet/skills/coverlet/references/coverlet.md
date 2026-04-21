# Coverlet

## Open/Free Status

- open source
- free to use

## Install

VSTest collector:

```bash
dotnet add package coverlet.collector
```

MSBuild driver:

```bash
dotnet add package coverlet.msbuild
```

Console tool:

```bash
dotnet tool install --global coverlet.console
```

## Verify First

Before installing a coverage driver, check what the repo already uses:

```bash
rg -n "coverlet\\.(collector|msbuild)|CollectCoverage|XPlat Code Coverage" -g '*.csproj' -g '*.props' -g '*.targets' .
dotnet tool list --local
dotnet tool list --global
command -v coverlet
```

## Common Usage

Collector:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

MSBuild:

```bash
dotnet test /p:CollectCoverage=true
```

Console:

```bash
coverlet /path/to/test-assembly.dll --target "dotnet" --targetargs "test /path/to/test-project --no-build"
```

## CI Fit

- add coverage packages only to test projects
- do not combine `coverlet.collector` and `coverlet.msbuild` in one test project
- render the resulting files with `ReportGenerator` if humans need HTML or Markdown output

## When Not To Use

- when the repo already standardized on another coverage engine

## Sources

- [Coverlet](https://github.com/coverlet-coverage/coverlet)
