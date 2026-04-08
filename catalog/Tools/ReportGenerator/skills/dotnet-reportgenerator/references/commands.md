# ReportGenerator CLI Commands

## Basic Syntax

```bash
reportgenerator [options]
```

or when installed as a local tool:

```bash
dotnet tool run reportgenerator [options]
```

or shorthand:

```bash
dotnet reportgenerator [options]
```

## Required Parameters

| Parameter | Description |
|-----------|-------------|
| `-reports:<pattern>` | Coverage report file(s) or glob pattern. Separate multiple with semicolon. |
| `-targetdir:<path>` | Output directory for generated reports. |

## Common Parameters

| Parameter | Description |
|-----------|-------------|
| `-reporttypes:<types>` | Output format(s), semicolon-separated. Default: `Html`. |
| `-sourcedirs:<paths>` | Source code directories for line coverage. |
| `-historydir:<path>` | Directory for history tracking across runs. |
| `-plugins:<paths>` | Custom plugin assemblies. |
| `-assemblyfilters:<filters>` | Include/exclude assemblies. Prefix `-` to exclude, `+` to include. |
| `-classfilters:<filters>` | Include/exclude classes by name. |
| `-filefilters:<filters>` | Include/exclude files by path. |
| `-verbosity:<level>` | Log level: `Off`, `Error`, `Warning`, `Info`, `Verbose`. |
| `-title:<title>` | Title for the coverage report. |
| `-tag:<tag>` | Build tag or version string shown in reports. |

## Example Commands

### Basic HTML Report

```bash
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"artifacts/coverage"
```

### Multiple Output Formats

```bash
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"artifacts/coverage" \
  -reporttypes:"Html;Cobertura;MarkdownSummaryGithub;Badges"
```

### Merge Multiple Coverage Files

```bash
reportgenerator \
  -reports:"**/coverage.*.xml" \
  -targetdir:"artifacts/coverage" \
  -reporttypes:"Cobertura"
```

### Filter Assemblies

```bash
reportgenerator \
  -reports:"coverage.xml" \
  -targetdir:"coverage" \
  -assemblyfilters:"+MyApp.*;-*.Tests"
```

### With History Tracking

```bash
reportgenerator \
  -reports:"coverage.xml" \
  -targetdir:"coverage" \
  -historydir:"coverage/history" \
  -reporttypes:"Html"
```

### CI Pipeline with Badge Generation

```bash
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"artifacts/coverage" \
  -reporttypes:"HtmlSummary;Cobertura;Badges" \
  -title:"MyProject Coverage" \
  -tag:"$BUILD_NUMBER"
```

### GitHub Actions Summary

```bash
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage" \
  -reporttypes:"MarkdownSummaryGithub"

cat coverage/SummaryGithub.md >> $GITHUB_STEP_SUMMARY
```

## Filter Syntax

Filters use prefix notation:

- `+Pattern` - include items matching pattern
- `-Pattern` - exclude items matching pattern

Wildcards supported:
- `*` matches any characters
- Patterns are case-insensitive

Example:

```bash
-assemblyfilters:"+MyApp.*;+MyLib.*;-*.Tests;-*.TestUtilities"
-classfilters:"+MyApp.Core.*;-*Generated*"
-filefilters:"-*Designer.cs;-*AssemblyInfo.cs"
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `REPORTGENERATOR_LICENSE` | License key for PRO features. |

## Help

```bash
reportgenerator --help
reportgenerator -?
```

## Sources

- [ReportGenerator Usage](https://github.com/danielpalme/ReportGenerator#usage)
- [ReportGenerator Wiki](https://github.com/danielpalme/ReportGenerator/wiki)
