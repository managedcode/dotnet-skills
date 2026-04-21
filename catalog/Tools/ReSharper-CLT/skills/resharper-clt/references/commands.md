# ReSharper Command Line Tools Commands

## jb inspectcode

Run code inspections and produce a report of issues.

### Basic Usage

```bash
jb inspectcode <solution|project> [options]
```

### Common Options

| Option | Description |
|--------|-------------|
| `-o`, `--output` | Output file path |
| `-f`, `--format` | Output format: `Sarif` (default), `Xml`, `Html`, `Text` |
| `--severity` | Minimum severity to report: `ERROR`, `WARNING`, `SUGGESTION`, `HINT` |
| `--include` | Glob pattern for files to include |
| `--exclude` | Glob pattern for files to exclude |
| `--project` | Project name filter within the solution |
| `--properties` | MSBuild properties (e.g., `Configuration=Release`) |
| `--dotSettings` | Path to custom settings file |
| `--caches-home` | Path to cache directory |
| `--no-build` | Skip building the solution before inspection |
| `--build` | Force build before inspection |
| `--verbosity` | Output verbosity: `OFF`, `FATAL`, `ERROR`, `WARN`, `INFO`, `VERBOSE`, `TRACE` |

### Examples

Basic inspection with SARIF output (default since 2024.1):

```bash
jb inspectcode MySolution.sln -o=artifacts/inspectcode.sarif
```

XML output format:

```bash
jb inspectcode MySolution.sln -f=Xml -o=artifacts/inspectcode.xml
```

HTML report:

```bash
jb inspectcode MySolution.sln -f=Html -o=artifacts/inspectcode.html
```

Filter by severity (only errors and warnings):

```bash
jb inspectcode MySolution.sln --severity=WARNING -o=artifacts/inspectcode.sarif
```

Include only specific files:

```bash
jb inspectcode MySolution.sln --include="src/**/*.cs" -o=artifacts/inspectcode.sarif
```

Exclude test files:

```bash
jb inspectcode MySolution.sln --exclude="**/*Tests*/**" -o=artifacts/inspectcode.sarif
```

Inspect specific project only:

```bash
jb inspectcode MySolution.sln --project=MyProject -o=artifacts/inspectcode.sarif
```

With explicit MSBuild configuration:

```bash
jb inspectcode MySolution.sln --properties:Configuration=Release -o=artifacts/inspectcode.sarif
```

Use custom settings file:

```bash
jb inspectcode MySolution.sln --dotSettings=custom.DotSettings -o=artifacts/inspectcode.sarif
```

CI-friendly with cache control:

```bash
jb inspectcode MySolution.sln --caches-home=.cache/resharper -o=artifacts/inspectcode.sarif
```

### Severity Levels

| Level | Description |
|-------|-------------|
| `ERROR` | Critical issues that likely cause runtime errors |
| `WARNING` | Issues that may cause problems or violate best practices |
| `SUGGESTION` | Improvements that could enhance code quality |
| `HINT` | Minor suggestions and style preferences |

---

## jb cleanupcode

Apply code cleanup and reformatting based on a profile.

### Basic Usage

```bash
jb cleanupcode <solution|project|files> [options]
```

### Common Options

| Option | Description |
|--------|-------------|
| `--profile` | Cleanup profile name (required for predictable results) |
| `--include` | Glob pattern for files to include |
| `--exclude` | Glob pattern for files to exclude |
| `--properties` | MSBuild properties (e.g., `Configuration=Release`) |
| `--dotSettings` | Path to custom settings file |
| `--caches-home` | Path to cache directory |
| `--no-build` | Skip building the solution before cleanup |
| `--verbosity` | Output verbosity |
| `--disable-settings-layers` | Disable specific settings layers |

### Built-in Profiles

| Profile | Description |
|---------|-------------|
| `Built-in: Full Cleanup` | Complete cleanup including code style, formatting, and optimizations |
| `Built-in: Reformat Code` | Only reformat without code changes |
| `Built-in: Reformat & Apply Syntax Style` | Reformat and apply syntax style preferences |

### Examples

Full cleanup on entire solution:

```bash
jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup"
```

Reformat only (no code changes):

```bash
jb cleanupcode MySolution.sln --profile="Built-in: Reformat Code"
```

Reformat with syntax style:

```bash
jb cleanupcode MySolution.sln --profile="Built-in: Reformat & Apply Syntax Style"
```

Cleanup specific files:

```bash
jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup" --include="src/**/*.cs"
```

Exclude generated files:

```bash
jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup" --exclude="**/*.g.cs;**/*.Designer.cs"
```

Use custom cleanup profile from settings:

```bash
jb cleanupcode MySolution.sln --profile="MyCustomProfile"
```

With explicit build configuration:

```bash
jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup" --properties:Configuration=Release
```

CI-friendly with cache:

```bash
jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup" --caches-home=.cache/resharper
```

### Important Notes

- Always build the solution before solution-wide cleanup so binary references resolve correctly.
- Use `--no-build` only when you are certain the solution is already built.
- Custom profiles must be defined in a `.DotSettings` file.
- Changes are applied in-place; use version control to review and revert if needed.

---

## Combined CI Workflow

Typical CI sequence:

```bash
# Build first
dotnet build MySolution.sln -c Release

# Inspect before cleanup
jb inspectcode MySolution.sln -o=artifacts/inspectcode-before.sarif

# Apply cleanup
jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup"

# Inspect after cleanup
jb inspectcode MySolution.sln -o=artifacts/inspectcode-after.sarif

# Run tests
dotnet test MySolution.sln -c Release --no-build
```

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| Non-zero | Failure (check output for details) |

Note: `jb inspectcode` returns 0 even when issues are found. Parse the output file to determine if issues exist.
