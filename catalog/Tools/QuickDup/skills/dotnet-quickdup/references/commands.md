# QuickDup CLI Commands

## Basic Syntax

```bash
quickdup [options]
```

## Core Options

| Option | Description | Default |
|--------|-------------|---------|
| `-path <dir>` | Root directory to scan | `.` (current directory) |
| `-ext <extensions>` | File extensions to include | `.cs` |
| `-exclude <patterns>` | Comma-separated glob patterns to exclude | none |
| `-top <n>` | Show top N duplicate groups | all |
| `-select <range>` | Select specific duplicate groups for detail | none |
| `-min-lines <n>` | Minimum lines for a clone to be reported | 6 |
| `-min-tokens <n>` | Minimum tokens for a clone to be reported | 50 |
| `-output <format>` | Output format: `text`, `json`, `html` | `text` |
| `-o <file>` | Write results to file | stdout |

## Scanning Commands

### Full Solution Scan

```bash
quickdup -path . -ext .cs
```

### Targeted Folder Scan

```bash
quickdup -path src/Domain -ext .cs
```

### Multiple Extensions

```bash
quickdup -path . -ext ".cs,.razor"
```

### With Exclusions

```bash
quickdup -path . -ext .cs -exclude "bin/*,obj/*,*.g.cs,*.generated.cs,*.Designer.cs"
```

### Migration-Heavy Projects

```bash
quickdup -path . -ext .cs -exclude "bin/*,obj/*,Migrations/*,*.g.cs,*.generated.cs"
```

## Filtering Commands

### Top N Duplicates

```bash
quickdup -path . -ext .cs -top 20
```

### Select Specific Groups

```bash
quickdup -path . -ext .cs -select 0..5
```

### Minimum Clone Size

```bash
quickdup -path . -ext .cs -min-lines 10 -min-tokens 100
```

## Output Commands

### JSON Output

```bash
quickdup -path . -ext .cs -output json -o results.json
```

### HTML Report

```bash
quickdup -path . -ext .cs -output html -o duplicates.html
```

### Store Results for Suppression

```bash
quickdup -path . -ext .cs -output json -o .quickdup/results.json
```

## Help and Version

```bash
quickdup -h
quickdup --help
quickdup --version
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success, no duplicates found |
| 1 | Success, duplicates found |
| 2 | Error during scan |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `QUICKDUP_CONFIG` | Path to configuration file |
| `QUICKDUP_CACHE_DIR` | Directory for cached parse results |
