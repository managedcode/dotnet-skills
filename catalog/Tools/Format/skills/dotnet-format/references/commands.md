# dotnet format CLI Commands

Use this reference for all `dotnet format` command patterns, options, and CI integration.

## Basic Commands

```bash
# Format entire solution
dotnet format MySolution.sln

# Format specific project
dotnet format MyProject.csproj

# Format current directory (finds solution or project automatically)
dotnet format
```

## Verification Mode (CI)

```bash
# Fail if any files would change (CI gate)
dotnet format --verify-no-changes

# Verify with specific target
dotnet format MySolution.sln --verify-no-changes

# Verify with diagnostics output
dotnet format --verify-no-changes --verbosity diagnostic
```

## Subcommands

```bash
# Format whitespace only (indentation, line endings)
dotnet format whitespace MySolution.sln

# Format code style only (editorconfig style rules)
dotnet format style MySolution.sln

# Apply analyzer code fixes
dotnet format analyzers MySolution.sln
```

## Subcommand Verification

```bash
# Verify whitespace only
dotnet format whitespace MySolution.sln --verify-no-changes

# Verify style only
dotnet format style MySolution.sln --verify-no-changes

# Verify analyzers only
dotnet format analyzers MySolution.sln --verify-no-changes
```

## Filtering Options

```bash
# Include only specific files (glob pattern)
dotnet format --include "src/**/*.cs"

# Exclude specific files (glob pattern)
dotnet format --exclude "**/*.Designer.cs"

# Combine include and exclude
dotnet format --include "src/**/*.cs" --exclude "**/Generated/**"

# Multiple includes
dotnet format --include "src/**/*.cs" --include "tests/**/*.cs"
```

## Diagnostic Options

```bash
# Apply fixes for specific diagnostic IDs
dotnet format analyzers --diagnostics IDE0005

# Apply fixes for multiple diagnostics
dotnet format analyzers --diagnostics IDE0005 IDE0051 IDE0052

# Apply fixes for specific severity
dotnet format analyzers --severity error
dotnet format analyzers --severity warn
dotnet format analyzers --severity info
```

## Verbosity Options

```bash
# Quiet output
dotnet format --verbosity quiet

# Minimal output
dotnet format --verbosity minimal

# Normal output (default)
dotnet format --verbosity normal

# Detailed output
dotnet format --verbosity detailed

# Diagnostic output (most verbose)
dotnet format --verbosity diagnostic
```

## Report Generation

```bash
# Generate JSON report
dotnet format --report format-report.json

# Generate report with verification
dotnet format --verify-no-changes --report format-report.json
```

## Binary Log Support

```bash
# Use existing binary log for analysis
dotnet format --binarylog build.binlog
```

## No Restore Option

```bash
# Skip restore (use when already restored)
dotnet format --no-restore
```

## CI Workflow Examples

### GitHub Actions

```yaml
- name: Verify formatting
  run: dotnet format --verify-no-changes --verbosity diagnostic
```

### Azure DevOps

```yaml
- script: dotnet format --verify-no-changes --verbosity diagnostic
  displayName: 'Verify code formatting'
```

### Pre-commit Hook

```bash
#!/bin/bash
dotnet format --verify-no-changes
if [ $? -ne 0 ]; then
  echo "Code formatting issues detected. Run 'dotnet format' to fix."
  exit 1
fi
```

## Common Patterns

```bash
# Full verification pipeline
dotnet format whitespace --verify-no-changes && \
dotnet format style --verify-no-changes && \
dotnet format analyzers --verify-no-changes

# Fix all formatting issues
dotnet format

# Fix and report
dotnet format --report format-changes.json

# Targeted analyzer fixes
dotnet format analyzers --diagnostics IDE0005 --severity warn
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (no changes needed or changes applied) |
| 1 | Error occurred |
| 2 | Changes detected (with `--verify-no-changes`) |

## Sources

- [dotnet format command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [Code style rule options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options)
