# ArchUnitNET

## Open/Free Status

- open source
- free to use

## Install

Core package:

```bash
dotnet add package TngTech.ArchUnitNET
```

Framework integration packages vary by test framework, for example:

```bash
dotnet add package TngTech.ArchUnitNET.xUnit
dotnet add package TngTech.ArchUnitNET.xUnitV3
dotnet add package TngTech.ArchUnitNET.MSTestV2
dotnet add package TngTech.ArchUnitNET.TUnit
```

## Verify First

Before adding packages, check whether the repo already references ArchUnitNET and which framework integration it uses:

```bash
rg -n "TngTech\\.ArchUnitNET" -g '*.csproj' .
```

## Common Usage

Load the target assemblies once, then assert rules in tests.

Good fit for:

- layered architecture
- namespace rules
- dependency restrictions
- domain boundary checks

## CI Fit

- runs as part of the normal test suite
- richer than lightweight architecture-rule libraries, but also heavier

## When Not To Use

- when simple `NetArchTest` rules already cover the needed constraints

## Sources

- [ArchUnitNET](https://github.com/TNG/ArchUnitNET)
