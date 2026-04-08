# CSharpier

## Open/Free Status

- open source
- free to use

## Install

Global tool:

```bash
dotnet tool install csharpier -g
```

## Verify First

Before installing, check whether the repo already has a local or global CSharpier tool:

```bash
rg --files -g '.config/dotnet-tools.json' -g '.csharpierignore'
dotnet tool list --local
dotnet tool list --global
command -v csharpier
```

## Common Usage

```bash
csharpier format .
csharpier check .
dotnet csharpier --version
```

## CI Fit

- use `csharpier check .` as the gate
- keep `.csharpierignore` versioned if needed
- document the relationship to `.editorconfig` and `dotnet format`

## When Not To Use

- when the repo wants detailed `.editorconfig`-driven formatting rather than one opinionated formatter

## Sources

- [CSharpier repository](https://github.com/belav/csharpier)
