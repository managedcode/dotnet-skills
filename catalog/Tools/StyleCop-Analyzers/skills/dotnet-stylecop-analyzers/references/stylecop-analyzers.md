# StyleCop.Analyzers

## Open/Free Status

- open source
- free to use

## Install

```bash
dotnet add package StyleCop.Analyzers
```

## Verify First

Before adding the package, check whether the repo already references it:

```bash
rg -n "StyleCop\\.Analyzers|stylecop\\.json" -g '*.csproj' -g 'stylecop.json' .
```

## Ownership Model

- keep severity and enable/disable policy in the repo-root `.editorconfig`
- keep `stylecop.json` only for StyleCop behavior that `.editorconfig` does not express well
- do not let `stylecop.json` become a second rule-severity system

## Common Usage

- keep rule severity in the root `.editorconfig`
- use `stylecop.json` for StyleCop-specific behavior where needed
- run through normal build:

```bash
dotnet build MySolution.sln -warnaserror
```

## Root .editorconfig Example

```ini
root = true

[*.cs]
dotnet_diagnostic.SA1200.severity = warning
dotnet_diagnostic.SA1208.severity = warning
dotnet_diagnostic.SA1516.severity = warning
dotnet_diagnostic.SA1600.severity = none
```

## stylecop.json Example

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "companyName": "Managed Code",
      "documentExposedElements": true,
      "documentInternalElements": false
    },
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace",
      "blankLinesBetweenUsingGroups": "require"
    },
    "layoutRules": {
      "newlineAtEndOfFile": "require"
    }
  }
}
```

## What stylecop.json Is Good For

- documentation behavior
- `using` placement
- ordering behavior
- file-header settings
- indentation behavior that must match StyleCop expectations

## What Should Stay In .editorconfig

- rule severity
- whether a specific `SAxxxx` rule is warning, error, suggestion, or none
- cross-analyzer ownership with SDK analyzers, Roslynator, or Meziantou

## CI Fit

- use build warnings or errors as the gate
- do not let `stylecop.json` replace `.editorconfig` as severity owner
- keep `stylecop.json` in source control and schema-backed when possible

## When Not To Use

- when the repo intentionally wants a lighter analyzer surface
- when a bundled coding-standard package already supersedes StyleCop

## Sources

- [StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [StyleCop Analyzers configuration](https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/documentation/Configuration.md)
