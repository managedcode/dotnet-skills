# ReportGenerator

## Open/Free Status

- open source
- free to use under Apache 2.0
- optional paid PRO features exist, but the core tool is free

## Install

Global tool:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Local tool:

```bash
dotnet new tool-manifest
dotnet tool install dotnet-reportgenerator-globaltool
```

## Verify First

Before installing, check whether the repo already has a local tool manifest or an existing global install:

```bash
rg --files -g '.config/dotnet-tools.json'
dotnet tool list --local
dotnet tool list --global
command -v reportgenerator
```

## Common Usage

```bash
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"artifacts/coverage" -reporttypes:"HtmlSummary;Cobertura"
dotnet reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"artifacts/coverage" -reporttypes:"MarkdownSummaryGithub"
```

## CI Fit

- generate reports into a stable artifact directory
- use one or more machine-readable formats alongside HTML if the pipeline consumes them

## When Not To Use

- when raw coverage files are enough and no human-readable output is required

## Sources

- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
