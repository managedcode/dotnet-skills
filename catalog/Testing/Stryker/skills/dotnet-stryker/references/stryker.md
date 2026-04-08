# Stryker.NET

## Open/Free Status

- open source
- free to use

## Install

```bash
dotnet tool install -g dotnet-stryker
```

Or as a local tool:

```bash
dotnet new tool-manifest
dotnet tool install dotnet-stryker
```

## Verify First

Before installing, check whether the repo already has a local or global Stryker tool:

```bash
rg --files -g '.config/dotnet-tools.json'
dotnet tool list --local
dotnet tool list --global
command -v dotnet-stryker
```

## Common Usage

```bash
dotnet stryker
```

## CI Fit

- best for critical libraries, domain logic, and less frequently changing hotspots
- usually too expensive for the fastest PR gate in large solutions

## When Not To Use

- when the test suite is already flaky or too slow
- when simple build, test, and coverage gates are still not stable

## Sources

- [Stryker.NET](https://github.com/stryker-mutator/stryker-net)
