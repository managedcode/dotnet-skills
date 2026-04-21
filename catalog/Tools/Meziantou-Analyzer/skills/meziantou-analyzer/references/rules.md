# Meziantou.Analyzer Rules

## Rule Categories

Meziantou.Analyzer organizes rules into several categories:

- **Design** - API design and architecture patterns
- **Usage** - Correct API usage and common pitfalls
- **Security** - Security best practices
- **Performance** - Performance optimizations
- **Style** - Code style and readability

## Commonly Used Rules

### Design Rules

| Rule ID | Description |
|---------|-------------|
| MA0004 | Use `ConfigureAwait(false)` in library code |
| MA0006 | Use `string.Equals` instead of `==` for case-insensitive comparisons |
| MA0011 | `IFormatProvider` is missing |
| MA0015 | Specify the parameter name in `ArgumentException` |
| MA0025 | Implement the functionality instead of throwing `NotImplementedException` |
| MA0026 | Fix TODO comment |
| MA0051 | Method is too long |
| MA0053 | Make class sealed |

### Usage Rules

| Rule ID | Description |
|---------|-------------|
| MA0001 | `StringComparison` is missing |
| MA0002 | `IEqualityComparer<string>` or `StringComparer` is missing |
| MA0003 | Add parameter name to improve readability |
| MA0005 | Use `Array.Empty<T>()` instead of a new empty array allocation |
| MA0009 | Add `RegexOptions.ExplicitCapture` to Regex |
| MA0010 | Mark attributes with `AttributeUsageAttribute` |
| MA0018 | Do not declare static members on generic types |
| MA0020 | Use direct methods instead of LINQ when available |

### Security Rules

| Rule ID | Description |
|---------|-------------|
| MA0007 | Add a comma after the last value |
| MA0008 | Add `StructLayoutAttribute` |
| MA0014 | Do not raise reserved exceptions |
| MA0035 | Do not use dangerous threading methods |
| MA0036 | Make class `static` |
| MA0055 | Do not use finalizer |
| MA0067 | Use `Task.FromResult` instead of creating a new `Task` |

### Performance Rules

| Rule ID | Description |
|---------|-------------|
| MA0028 | Optimize StringBuilder usage |
| MA0029 | Combine LINQ methods |
| MA0031 | Optimize `Enumerable.Count()` usage |
| MA0032 | Use an overload with a `StringComparer` parameter |
| MA0033 | Do not convert implicitly to `DateTimeOffset` |
| MA0040 | Forward the `CancellationToken` parameter to methods that take one |
| MA0041 | Make property static |
| MA0042 | Do not use blocking calls in an async method |
| MA0045 | Do not use blocking calls in an async method |
| MA0063 | Use `Where` before `OrderBy` |
| MA0110 | Use the `Regex` source generator |

### Style Rules

| Rule ID | Description |
|---------|-------------|
| MA0038 | Make method static |
| MA0043 | Use `nameof` operator |
| MA0047 | Declare types in namespaces |
| MA0048 | File name must match type name |
| MA0049 | Type should not implement `ToString()` with `IFormatProvider` parameter |
| MA0050 | Validate arguments correctly in iterator methods |
| MA0052 | Replace `Substring` with `Range` indexer |
| MA0076 | Do not use implicit culture-sensitive `ToString()` |

## High-Value Rules for New Projects

When starting a new project, consider enabling these rules as errors:

```ini
# Async best practices
dotnet_diagnostic.MA0004.severity = error
dotnet_diagnostic.MA0040.severity = error
dotnet_diagnostic.MA0042.severity = error

# String handling
dotnet_diagnostic.MA0001.severity = error
dotnet_diagnostic.MA0006.severity = error
dotnet_diagnostic.MA0011.severity = error

# Performance
dotnet_diagnostic.MA0005.severity = error
dotnet_diagnostic.MA0020.severity = error
dotnet_diagnostic.MA0029.severity = error

# Design
dotnet_diagnostic.MA0015.severity = error
dotnet_diagnostic.MA0053.severity = error
```

## Rules to Consider Disabling

Some rules may conflict with project style or be too strict:

| Rule ID | Reason to Disable |
|---------|-------------------|
| MA0003 | Parameter names may clutter simple method calls |
| MA0026 | TODO comments are valid during development |
| MA0051 | Method length thresholds may be too restrictive |
| MA0053 | Sealed classes may conflict with testing frameworks |
| MA0048 | File naming conventions may differ |

## Rule Interaction Notes

- MA0001 and MA0006 both deal with string comparisons; enable both for complete coverage
- MA0004 is essential for library code but can be disabled for application code
- MA0040, MA0042, and MA0045 all relate to async patterns; keep them aligned
- MA0020, MA0029, MA0031, and MA0063 overlap with LINQ optimization; configure consistently

## Sources

- [Meziantou.Analyzer Rules Documentation](https://github.com/meziantou/Meziantou.Analyzer/tree/main/docs/Rules)
