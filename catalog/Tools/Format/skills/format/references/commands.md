# dotnet format Commands

Use the repository's documented workspace and SDK. Replace `MySolution.slnx` and paths below with real values from the checkout.

## Inspect Before Running

```bash
dotnet --version
dotnet format --version
git status --short
```

Run only against trusted code. The formatter can restore, compile, and load analyzers from the selected project or solution.

## Verify Or Apply The Intended Surface

```bash
# Read-only CI/local gate across all applicable formatter surfaces.
dotnet format MySolution.slnx --verify-no-changes --verbosity diagnostic

# Apply all configured formatting and fixable diagnostics.
dotnet format MySolution.slnx --verbosity normal

# Limit the operation to one surface.
dotnet format whitespace MySolution.slnx --verify-no-changes
dotnet format style MySolution.slnx --verify-no-changes
dotnet format analyzers MySolution.slnx --verify-no-changes
```

The workspace can also precede the subcommand, for example `dotnet format MySolution.slnx whitespace`. Prefer one command shape consistently within a repository.

## Narrow By Path Or Diagnostic

`--include` and `--exclude` accept space-separated paths relative to the workspace. They are not glob expressions.

```bash
# Format only selected source and test directories.
dotnet format MySolution.slnx --include ./src/FeatureA/ ./tests/FeatureA.Tests/

# Exclude generated or vendored directories.
dotnet format MySolution.slnx --exclude ./src/Generated/ ./vendor/

# Apply one built-in code-style fix.
dotnet format style MySolution.slnx --diagnostics IDE0005 --severity info

# Apply one fixable non-style analyzer diagnostic.
dotnet format analyzers MySolution.slnx --diagnostics CA1831 --severity warn
```

The default diagnostic threshold is `warn`. Set `--severity info` only when the repository intends informational diagnostics to participate.

## Restore, Reports, And Build Logs

```bash
# Skip restore only after a successful restore for this checkout.
dotnet format MySolution.slnx --no-restore --verify-no-changes

# Write a JSON report into the specified directory.
dotnet format MySolution.slnx --report ./artifacts/format-report/

# Capture project/solution loading details when formatter loading fails.
dotnet format MySolution.slnx --binarylog ./artifacts/format.binlog --verbosity diagnostic
```

## CI

Pin the SDK using the repository's `global.json` or CI setup, restore once, and run the non-mutating gate:

```yaml
- name: Restore
  run: dotnet restore MySolution.slnx

- name: Verify .NET formatting
  run: dotnet format MySolution.slnx --no-restore --verify-no-changes --verbosity diagnostic
```

Do not make pull-request CI apply and commit formatter changes. CI should fail with evidence; a developer or authorized automation can apply and review them separately.

## Troubleshooting

- If project loading fails, verify the selected SDK, restore, workload availability, and exact solution/project path before changing source code.
- If the diff is much larger than expected, narrow by subcommand, paths, or diagnostic IDs and inspect line-ending changes.
- If output says a diagnostic cannot be fixed, do not treat repeated full-solution runs as progress. Use the analyzer/build output to decide whether a targeted manual fix is required.
- If verification fails immediately after a mutating run, inspect files the formatter could not load, generated files, analyzer limitations, and concurrent edits.

## Sources

- [dotnet format command](https://learn.microsoft.com/dotnet/core/tools/dotnet-format)
- [Code analysis configuration](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-options)
