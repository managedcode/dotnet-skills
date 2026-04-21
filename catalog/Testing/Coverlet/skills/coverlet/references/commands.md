# Coverlet Commands Reference

## Coverlet Drivers Overview

Coverlet provides three drivers for collecting code coverage. Choose exactly one per test project.

| Driver | Package | Use Case |
|--------|---------|----------|
| Collector | `coverlet.collector` | VSTest integration via `--collect` |
| MSBuild | `coverlet.msbuild` | MSBuild property-driven coverage |
| Console | `coverlet.console` | Standalone CLI tool |

---

## Collector Driver (`coverlet.collector`)

### Installation

```bash
dotnet add <TEST_PROJECT>.csproj package coverlet.collector
```

### Basic Command

```bash
dotnet test <TEST_PROJECT>.csproj --collect:"XPlat Code Coverage"
```

### Output Location

Coverage files are written to `TestResults/<guid>/coverage.cobertura.xml` by default.

### Common Options

```bash
# Specify output directory
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# With runsettings file
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

### Runsettings Configuration

Create `coverlet.runsettings`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura,opencover</Format>
          <Exclude>[*]*.Generated.*</Exclude>
          <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
          <ExcludeByFile>**/Migrations/*.cs</ExcludeByFile>
          <IncludeDirectory>../src/*</IncludeDirectory>
          <SingleHit>false</SingleHit>
          <UseSourceLink>true</UseSourceLink>
          <IncludeTestAssembly>false</IncludeTestAssembly>
          <SkipAutoProps>true</SkipAutoProps>
          <DeterministicReport>true</DeterministicReport>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

---

## MSBuild Driver (`coverlet.msbuild`)

### Installation

```bash
dotnet add <TEST_PROJECT>.csproj package coverlet.msbuild
```

### Basic Command

```bash
dotnet test <TEST_PROJECT>.csproj /p:CollectCoverage=true
```

### Output Formats

```bash
# Cobertura (default for CI)
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# OpenCover format
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Multiple formats
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=\"opencover,cobertura\"

# JSON format
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json

# LCOV format
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
```

### Output Location

```bash
# Custom output file
dotnet test /p:CollectCoverage=true /p:CoverletOutput=./coverage/

# With specific filename
dotnet test /p:CollectCoverage=true /p:CoverletOutput=./coverage/coverage.xml
```

### MSBuild Properties

| Property | Description | Default |
|----------|-------------|---------|
| `CollectCoverage` | Enable coverage collection | `false` |
| `CoverletOutputFormat` | Output format(s) | `json` |
| `CoverletOutput` | Output path | `./` |
| `Threshold` | Minimum coverage percentage | none |
| `ThresholdType` | `line`, `branch`, `method` | `line` |
| `ThresholdStat` | `minimum`, `total`, `average` | `minimum` |
| `ExcludeByFile` | Files to exclude | none |
| `ExcludeByAttribute` | Attributes to exclude | none |
| `Exclude` | Filter expressions | none |
| `Include` | Filter expressions | none |
| `IncludeDirectory` | Source directories | none |
| `UseSourceLink` | Use SourceLink URIs | `false` |
| `SingleHit` | Count hits as 0 or 1 | `false` |
| `IncludeTestAssembly` | Include test assembly | `false` |
| `SkipAutoProps` | Skip auto-properties | `false` |
| `DeterministicReport` | Deterministic output | `false` |
| `MergeWith` | Merge with existing file | none |

### Coverage Thresholds

```bash
# Fail if line coverage below 80%
dotnet test /p:CollectCoverage=true /p:Threshold=80

# Fail if branch coverage below 70%
dotnet test /p:CollectCoverage=true /p:Threshold=70 /p:ThresholdType=branch

# Multiple threshold types
dotnet test /p:CollectCoverage=true /p:Threshold=\"80,70,90\" /p:ThresholdType=\"line,branch,method\"

# Average instead of minimum
dotnet test /p:CollectCoverage=true /p:Threshold=80 /p:ThresholdStat=average
```

### Merging Coverage

```bash
# Merge with existing coverage file
dotnet test /p:CollectCoverage=true /p:MergeWith="./coverage/previous.json"

# Typical multi-project merge workflow
dotnet test Project1.Tests.csproj /p:CollectCoverage=true /p:CoverletOutput=./coverage/
dotnet test Project2.Tests.csproj /p:CollectCoverage=true /p:CoverletOutput=./coverage/ /p:MergeWith="./coverage/coverage.json"
```

### Directory.Build.props Configuration

Set defaults across all test projects:

```xml
<Project>
  <PropertyGroup Condition="'$(CollectCoverage)' == 'true'">
    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
    <CoverletOutput>$(MSBuildThisFileDirectory)coverage/</CoverletOutput>
    <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludeFromCodeCoverage</ExcludeByAttribute>
    <ExcludeByFile>**/Migrations/*.cs</ExcludeByFile>
    <SkipAutoProps>true</SkipAutoProps>
    <DeterministicReport>true</DeterministicReport>
  </PropertyGroup>
</Project>
```

---

## Console Tool (`coverlet.console`)

### Installation

```bash
# As local tool (recommended)
dotnet new tool-manifest  # if not present
dotnet tool install coverlet.console

# As global tool
dotnet tool install --global coverlet.console
```

### Basic Command

```bash
# Local tool
dotnet tool run coverlet <TEST_ASSEMBLY>.dll --target "dotnet" --targetargs "test <TEST_PROJECT>.csproj --no-build"

# Global tool
coverlet <TEST_ASSEMBLY>.dll --target "dotnet" --targetargs "test <TEST_PROJECT>.csproj --no-build"
```

### Common Options

```bash
coverlet <TEST_ASSEMBLY>.dll \
  --target "dotnet" \
  --targetargs "test <TEST_PROJECT>.csproj --no-build" \
  --output ./coverage/ \
  --format cobertura \
  --exclude "[*]*.Migrations.*" \
  --exclude-by-attribute "Obsolete,GeneratedCode" \
  --threshold 80 \
  --threshold-type line
```

### Console Tool Arguments

| Argument | Description |
|----------|-------------|
| `--target` | Executable to run tests |
| `--targetargs` | Arguments for target executable |
| `--output` | Output path |
| `--format` | Output format(s) |
| `--threshold` | Minimum coverage |
| `--threshold-type` | `line`, `branch`, `method` |
| `--threshold-stat` | `minimum`, `total`, `average` |
| `--exclude` | Filter expressions |
| `--include` | Filter expressions |
| `--exclude-by-file` | File patterns to exclude |
| `--exclude-by-attribute` | Attributes to exclude |
| `--include-directory` | Source directories |
| `--use-source-link` | Use SourceLink |
| `--single-hit` | Count hits as 0 or 1 |
| `--include-test-assembly` | Include test assembly |
| `--skip-auto-props` | Skip auto-properties |
| `--merge-with` | Merge with existing file |
| `--verbosity` | `quiet`, `minimal`, `normal`, `detailed` |

---

## CI Integration Examples

### GitHub Actions

```yaml
- name: Run tests with coverage
  run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

- name: Upload coverage
  uses: codecov/codecov-action@v4
  with:
    directory: ./coverage
    files: "**/*.cobertura.xml"
```

### Azure Pipelines

```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: test
    arguments: '--collect:"XPlat Code Coverage" --results-directory $(Build.SourcesDirectory)/coverage'

- task: PublishCodeCoverageResults@2
  inputs:
    codeCoverageTool: Cobertura
    summaryFileLocation: '$(Build.SourcesDirectory)/coverage/**/coverage.cobertura.xml'
```

### GitLab CI

```yaml
test:
  script:
    - dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
  artifacts:
    reports:
      coverage_report:
        coverage_format: cobertura
        path: coverage/**/coverage.cobertura.xml
```

---

## ReportGenerator Integration

Generate HTML reports from coverage files:

```bash
# Install
dotnet tool install dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"./coverage/**/coverage.cobertura.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:Html

# Multiple formats
reportgenerator \
  -reports:"./coverage/**/coverage.cobertura.xml" \
  -targetdir:"./coverage/report" \
  -reporttypes:"Html;Badges;TextSummary"
```
