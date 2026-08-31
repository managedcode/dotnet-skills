# dotnet format Configuration

Use this reference only when the task changes formatter or analyzer configuration. For ordinary formatting, preserve the repository's existing settings.

## Sources Of Truth

- `.editorconfig` owns whitespace and C#/.NET code-style preferences for the files in its scope.
- Project and shared MSBuild files own analyzer packages and properties such as `AnalysisLevel`, `AnalysisMode`, `EnableNETAnalyzers`, and `EnforceCodeStyleInBuild`.
- `global.json` and CI SDK setup pin the formatter implementation because `dotnet format` ships with the SDK.

Read nested `.editorconfig` files before changing a rule: the nearest applicable section can override a root setting.

## Configure Deliberately

Set individual rule severity when a specific diagnostic must participate:

```ini
[*.cs]
dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.CA1822.severity = warning
```

Code-style options can also carry a preference and severity:

```ini
[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
csharp_prefer_braces = true:warning
```

Do not paste a generic `.editorconfig` over an existing repository. Choose settings from the codebase's real conventions, framework constraints, and enforcement goals.

## Important Boundaries

- `dotnet format` uses default preferences when no `.editorconfig` is present. Creating one is a policy change, not a prerequisite for running the command.
- `--severity` selects the minimum diagnostic severity the formatter attempts to fix; it does not redefine the repository's analyzer policy.
- `--include-generated` is an explicit opt-in. Use it only when generated files are intentionally versioned and owned by the repository.
- Analyzer severity in `.editorconfig` and bulk MSBuild analysis settings have precedence rules. Verify effective build output instead of assuming one file controls everything.
- If another formatter owns C# layout, document the ownership boundary and avoid running overlapping tools over the same files.

## Validate A Configuration Change

1. Run the narrow formatter command that exercises the changed rule.
2. Inspect the resulting diff rather than accepting broad mechanical churn.
3. Run the same command with `--verify-no-changes`.
4. Build the affected projects so analyzer severity and MSBuild configuration are proven outside the formatter.
5. Run focused tests when a code fix can change behavior.

## Sources

- [dotnet format command](https://learn.microsoft.com/dotnet/core/tools/dotnet-format)
- [Configuration options for code analysis](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-options)
- [Code style rule options](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/code-style-rule-options)
