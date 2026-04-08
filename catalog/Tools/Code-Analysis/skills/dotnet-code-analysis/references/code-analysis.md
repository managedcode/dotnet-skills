# .NET SDK Code Analysis

## Open/Free Status

- first-party .NET SDK analyzers
- free to use
- included with the modern .NET SDK

## Install

For projects targeting .NET 5 or later, built-in analyzers are included with the SDK and code analysis is enabled by default.

For older or explicitly controlled projects, set analyzer properties in the project or `Directory.Build.props`:

```xml
<PropertyGroup>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
</PropertyGroup>
```

## Verify First

Before adding anything, check whether the repo already set analyzer policy:

```bash
rg -n "EnableNETAnalyzers|AnalysisLevel|AnalysisMode|TreatWarningsAsErrors" -g 'Directory.Build.*' -g '*.csproj' .
```

## Common Commands

```bash
dotnet build MySolution.sln
dotnet build MySolution.sln -warnaserror
```

## CI Fit

- use `dotnet build` as the analyzer gate
- keep `AnalysisLevel` explicit in MSBuild
- keep rule severity in the repo-root `.editorconfig`

## When Not To Use

- when you only need a formatter
- when the repo specifically needs a framework or third-party analyzer set beyond the built-in SDK rules

## Sources

- [Overview of .NET source code analysis](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
