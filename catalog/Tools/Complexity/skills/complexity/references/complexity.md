# .NET Complexity and Maintainability Rules

## Open/Free Status

- first-party .NET analyzers
- free to use with the .NET SDK

## Best Answer for Complex Methods

If the question is "which analyzer finds methods that are too complex?", the primary built-in answer is:

- `CA1502` for excessive cyclomatic complexity

Closely related maintainability rules:

- `CA1505` for low maintainability index
- `CA1506` for excessive class coupling
- `CA1501` for excessive inheritance depth

## Verify First

Before adding anything, check whether the repo already configures these rules:

```bash
rg -n "CA1501|CA1502|CA1505|CA1506|CodeMetricsConfig" -g '.editorconfig' -g '*.csproj' -g 'Directory.Build.*' .
rg --files -g 'CodeMetricsConfig.txt'
```

## Root .editorconfig

Keep severity in the repo-root `.editorconfig`, for example:

```ini
root = true

[*.cs]
dotnet_diagnostic.CA1502.severity = warning
dotnet_diagnostic.CA1505.severity = warning
dotnet_diagnostic.CA1506.severity = warning
dotnet_diagnostic.CA1501.severity = suggestion
```

## Threshold Configuration

Thresholds for these code-metrics rules are configured through a checked-in `CodeMetricsConfig.txt` file, for example:

```text
CA1502: 20
CA1502(Type): 6
CA1505: 10
CA1506: 30
CA1506(Type): 80
CA1501: 6
```

Then include it in the project:

```xml
<ItemGroup>
  <AdditionalFiles Include="CodeMetricsConfig.txt" />
</ItemGroup>
```

## CI Fit

- use `dotnet build` as the enforcement path
- keep thresholds checked in
- treat `CA1502` as the main answer for overly complex methods, not as a vanity metric

## When Not To Use

- when the repo only wants stylistic linting and does not intend to act on maintainability findings

## Sources

- [CA1502: Avoid excessive complexity](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1502)
- [CA1505: Avoid unmaintainable code](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1505)
- [CA1506: Avoid excessive class coupling](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1506)
- [CA1501: Avoid excessive inheritance](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1501)
- [Code metrics values](https://learn.microsoft.com/en-us/visualstudio/code-quality/code-metrics-values?view=vs-2022)
