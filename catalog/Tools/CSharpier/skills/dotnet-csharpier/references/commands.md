# CSharpier CLI Commands

## Installation

### Local Tool (Recommended)

```bash
# Create tool manifest if missing
dotnet new tool-manifest

# Install CSharpier as local tool
dotnet tool install csharpier

# Update to latest version
dotnet tool update csharpier
```

### Global Tool

```bash
# Install globally
dotnet tool install -g csharpier

# Update globally
dotnet tool update -g csharpier
```

## Formatting Commands

### Format Files

```bash
# Format all files in current directory recursively
dotnet csharpier .

# Format specific file
dotnet csharpier MyFile.cs

# Format specific directory
dotnet csharpier src/

# Format multiple paths
dotnet csharpier src/ tests/
```

### Check Mode (CI)

```bash
# Check formatting without modifying files
# Returns non-zero exit code if files need formatting
dotnet csharpier check .

# Check specific directory
dotnet csharpier check src/
```

### Additional Options

```bash
# Write formatted files to stdout instead of modifying
dotnet csharpier --write-stdout MyFile.cs

# Skip write (dry run) - validates syntax without output
dotnet csharpier --skip-write .

# Include generated files (excluded by default)
dotnet csharpier --include-generated .

# Disable cache (useful for debugging)
dotnet csharpier --no-cache .

# Use fast exit on first error
dotnet csharpier --fast .

# Pipe content through stdin/stdout
cat MyFile.cs | dotnet csharpier --write-stdout
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success - all files formatted or already formatted |
| 1 | Error - formatting failed or files need formatting (check mode) |

## CI Integration Examples

### GitHub Actions

```yaml
- name: Check formatting
  run: dotnet csharpier check .
```

### Azure DevOps

```yaml
- script: dotnet csharpier check .
  displayName: 'Check CSharpier formatting'
```

### Pre-commit Hook

```bash
#!/bin/sh
dotnet csharpier check .
```

## MsBuild Integration

Add to `.csproj` for build-time formatting check:

```xml
<Target Name="CheckFormatting" BeforeTargets="Build">
  <Exec Command="dotnet csharpier check ." />
</Target>
```

## IDE Extensions

- **Visual Studio**: CSharpier extension from VS Marketplace
- **VS Code**: CSharpier extension
- **JetBrains Rider**: CSharpier plugin

Configure format-on-save in IDE settings for seamless workflow.
