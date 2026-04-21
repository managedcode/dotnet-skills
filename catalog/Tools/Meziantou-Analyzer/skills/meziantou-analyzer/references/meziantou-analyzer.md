# Meziantou.Analyzer

## Open/Free Status

- open source
- free to use

## Install

```bash
dotnet add package Meziantou.Analyzer
```

## Verify First

Before adding the package, check whether the repo already references it:

```bash
rg -n "Meziantou\\.Analyzer" -g '*.csproj' .
```

## Common Usage

```bash
dotnet build MySolution.sln -warnaserror
```

Configure severity in `.editorconfig`, for example:

```ini
[*.cs]
dotnet_diagnostic.MA0004.severity = warning
```

## CI Fit

- use `dotnet build` as the main enforcement gate
- keep severities explicit in repo config

## When Not To Use

- when the repo already decided the analyzer surface is intentionally smaller

## Sources

- [Meziantou.Analyzer repository](https://github.com/meziantou/Meziantou.Analyzer)
