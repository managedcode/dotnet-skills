# Stryker CLI Commands and Configuration

## CLI Commands

### Basic Run

```bash
dotnet stryker
```

### Run with Project Specification

```bash
dotnet stryker --project MyProject.csproj
```

### Run with Test Project

```bash
dotnet stryker --project MyProject.csproj --test-project MyProject.Tests.csproj
```

### Run with Solution

```bash
dotnet stryker --solution MySolution.sln
```

### Run with Mutation Level

```bash
dotnet stryker --mutation-level Basic
dotnet stryker --mutation-level Standard
dotnet stryker --mutation-level Advanced
dotnet stryker --mutation-level Complete
```

### Run with Concurrency

```bash
dotnet stryker --concurrency 4
```

### Run with Reporters

```bash
dotnet stryker --reporter html
dotnet stryker --reporter json
dotnet stryker --reporter markdown
dotnet stryker --reporter progress
dotnet stryker --reporter cleartext
dotnet stryker --reporter dashboard
```

### Run with Thresholds

```bash
dotnet stryker --threshold-high 80 --threshold-low 60 --threshold-break 40
```

### Dry Run (No Mutations)

```bash
dotnet stryker --dry-run
```

### Filter Files

```bash
dotnet stryker --mutate "**/*.cs" --mutate "!**/Migrations/**"
```

### Filter Mutations

```bash
dotnet stryker --ignore-mutations Linq
dotnet stryker --ignore-mutations String
dotnet stryker --ignore-mutations Arithmetic
```

## Configuration File

Stryker uses `stryker-config.json` in the project root.

### Minimal Configuration

```json
{
  "stryker-config": {
    "project": "MyProject.csproj"
  }
}
```

### Standard Configuration

```json
{
  "stryker-config": {
    "project": "MyProject.csproj",
    "test-projects": ["MyProject.Tests.csproj"],
    "mutation-level": "Standard",
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 40
    },
    "reporters": ["progress", "html"],
    "concurrency": 4
  }
}
```

### Full Configuration

```json
{
  "stryker-config": {
    "project": "MyProject.csproj",
    "test-projects": ["MyProject.Tests.csproj", "MyProject.IntegrationTests.csproj"],
    "solution": "MySolution.sln",
    "mutation-level": "Advanced",
    "mutate": [
      "**/*.cs",
      "!**/Migrations/**",
      "!**/Generated/**"
    ],
    "ignore-mutations": [
      "Linq"
    ],
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 40
    },
    "reporters": ["progress", "html", "json"],
    "report-file-name": "mutation-report",
    "concurrency": 4,
    "log-level": "info",
    "coverage-analysis": "perTest",
    "disable-bail": false,
    "since": {
      "enabled": true,
      "target": "main"
    }
  }
}
```

## Key Options Reference

### Mutation Levels

| Level    | Mutators Included                                    |
|----------|------------------------------------------------------|
| Basic    | Core arithmetic, boolean, comparison                 |
| Standard | Basic + string, equality, logical operators          |
| Advanced | Standard + linq, method calls, block statements      |
| Complete | All available mutators                               |

### Reporters

| Reporter   | Output                                               |
|------------|------------------------------------------------------|
| html       | Interactive HTML report                              |
| json       | Machine-readable JSON                                |
| markdown   | Markdown summary                                     |
| progress   | Console progress bar                                 |
| cleartext  | Plain text console output                            |
| dashboard  | Stryker Dashboard upload                             |
| dots       | Minimal dot progress                                 |

### Coverage Analysis Modes

| Mode          | Description                                          |
|---------------|------------------------------------------------------|
| off           | Run all tests for each mutant                        |
| perTest       | Track which tests cover which mutants                |
| perTestInIsolation | Same as perTest but with process isolation      |

### Incremental Mutation Testing

Enable incremental mutation testing to only test changed code:

```json
{
  "stryker-config": {
    "since": {
      "enabled": true,
      "target": "main"
    }
  }
}
```

CLI equivalent:

```bash
dotnet stryker --since --since-target main
```

## CI Integration

### GitHub Actions

```yaml
- name: Run Stryker
  run: dotnet stryker --reporter json --reporter html
  continue-on-error: true

- name: Upload Mutation Report
  uses: actions/upload-artifact@v7
  with:
    name: mutation-report
    path: StrykerOutput/
```

### Azure Pipelines

```yaml
- script: dotnet stryker --reporter json --reporter html
  displayName: 'Run Mutation Tests'
  continueOnError: true

- task: PublishBuildArtifacts@1
  inputs:
    pathToPublish: 'StrykerOutput'
    artifactName: 'MutationReport'
```

## Output Directories

Default output: `StrykerOutput/<timestamp>/`

Custom output:

```bash
dotnet stryker --output ./reports/mutations
```
