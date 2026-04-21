# Meziantou.Analyzer EditorConfig Configuration

## Basic Setup

Add configuration to the root `.editorconfig` file to control rule severities.

### Severity Levels

- `none` - Disable the rule
- `silent` - Hidden from user, still runs
- `suggestion` - Shows as info message
- `warning` - Shows as warning
- `error` - Shows as error, fails build

## Recommended Starter Configuration

```ini
[*.cs]
# === Meziantou.Analyzer Configuration ===

# String handling - enforce explicit culture
dotnet_diagnostic.MA0001.severity = warning
dotnet_diagnostic.MA0002.severity = warning
dotnet_diagnostic.MA0006.severity = warning
dotnet_diagnostic.MA0011.severity = warning
dotnet_diagnostic.MA0074.severity = warning
dotnet_diagnostic.MA0076.severity = warning

# Async best practices
dotnet_diagnostic.MA0004.severity = warning
dotnet_diagnostic.MA0040.severity = warning
dotnet_diagnostic.MA0042.severity = warning
dotnet_diagnostic.MA0045.severity = warning
dotnet_diagnostic.MA0067.severity = warning

# Performance
dotnet_diagnostic.MA0005.severity = warning
dotnet_diagnostic.MA0020.severity = warning
dotnet_diagnostic.MA0028.severity = warning
dotnet_diagnostic.MA0029.severity = warning
dotnet_diagnostic.MA0031.severity = warning
dotnet_diagnostic.MA0063.severity = suggestion

# Design
dotnet_diagnostic.MA0015.severity = warning
dotnet_diagnostic.MA0025.severity = warning
dotnet_diagnostic.MA0047.severity = warning

# Security
dotnet_diagnostic.MA0014.severity = error
dotnet_diagnostic.MA0035.severity = error
dotnet_diagnostic.MA0055.severity = warning

# Regex
dotnet_diagnostic.MA0009.severity = suggestion
dotnet_diagnostic.MA0110.severity = suggestion

# Style - often project-specific
dotnet_diagnostic.MA0003.severity = none
dotnet_diagnostic.MA0026.severity = none
dotnet_diagnostic.MA0048.severity = none
dotnet_diagnostic.MA0051.severity = none
dotnet_diagnostic.MA0053.severity = none
```

## Library Configuration

Libraries should be stricter about async patterns and public API design:

```ini
[*.cs]
# Library-specific: require ConfigureAwait
dotnet_diagnostic.MA0004.severity = error

# Public API design
dotnet_diagnostic.MA0011.severity = error
dotnet_diagnostic.MA0015.severity = error
dotnet_diagnostic.MA0016.severity = error
dotnet_diagnostic.MA0018.severity = warning
```

## Application Configuration

Applications can be more relaxed about some rules:

```ini
[*.cs]
# Applications may not need ConfigureAwait
dotnet_diagnostic.MA0004.severity = none

# More relaxed on certain design rules
dotnet_diagnostic.MA0053.severity = suggestion
```

## Test Project Configuration

Test projects often need different rules:

```ini
[**/Tests/**/*.cs]
[**/Test/**/*.cs]
[**/*.Tests/**/*.cs]

# Disable sealed class requirement for test fixtures
dotnet_diagnostic.MA0053.severity = none

# Allow longer methods in tests
dotnet_diagnostic.MA0051.severity = none

# Allow TODO comments in tests
dotnet_diagnostic.MA0026.severity = none

# Tests may have blocking calls
dotnet_diagnostic.MA0042.severity = suggestion
dotnet_diagnostic.MA0045.severity = suggestion
```

## Generated Code Configuration

Exclude generated files from analysis:

```ini
[*.Generated.cs]
[*.g.cs]
[*.designer.cs]
generated_code = true
```

## Combining with Other Analyzers

When using Meziantou.Analyzer with other analyzers, avoid duplicate diagnostics:

### With Roslynator

```ini
# Roslynator RCS1197 overlaps with MA0020 (LINQ optimization)
# Pick one and disable the other
dotnet_diagnostic.RCS1197.severity = none
dotnet_diagnostic.MA0020.severity = warning

# Roslynator RCS1075 overlaps with MA0042 (async blocking)
dotnet_diagnostic.RCS1075.severity = none
dotnet_diagnostic.MA0042.severity = warning
```

### With StyleCop

```ini
# StyleCop SA1649 overlaps with MA0048 (file naming)
# Pick one and disable the other
dotnet_diagnostic.SA1649.severity = none
dotnet_diagnostic.MA0048.severity = warning
```

## Rule-Specific Options

Some Meziantou rules support additional configuration:

### MA0051 - Method Length

```ini
# Configure maximum method length
MA0051.maximum_lines_per_method = 60
MA0051.maximum_statements_per_method = 40
```

### MA0053 - Sealed Classes

```ini
# Configure when to suggest sealing
MA0053.public_class_should_be_sealed = true
```

## Migration Strategy

When adding Meziantou.Analyzer to an existing project:

### Phase 1: Start with suggestions

```ini
[*.cs]
dotnet_analyzer_diagnostic.category-Meziantou.severity = suggestion
```

### Phase 2: Gradually increase severity

```ini
[*.cs]
# Keep most as suggestions
dotnet_analyzer_diagnostic.category-Meziantou.severity = suggestion

# Promote critical rules to warnings
dotnet_diagnostic.MA0001.severity = warning
dotnet_diagnostic.MA0004.severity = warning
dotnet_diagnostic.MA0040.severity = warning
```

### Phase 3: Enforce in CI

```ini
[*.cs]
dotnet_analyzer_diagnostic.category-Meziantou.severity = warning

# Security rules as errors
dotnet_diagnostic.MA0014.severity = error
dotnet_diagnostic.MA0035.severity = error
```

## CI Integration

Build with warnings as errors for enforced rules:

```bash
dotnet build -warnaserror:MA0001,MA0004,MA0014,MA0035,MA0040
```

Or use the global setting:

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

## Sources

- [Meziantou.Analyzer Configuration](https://github.com/meziantou/Meziantou.Analyzer)
- [.editorconfig Documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
