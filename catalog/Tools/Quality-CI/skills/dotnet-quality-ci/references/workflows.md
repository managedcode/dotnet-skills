# GitHub Actions Workflows for .NET Quality Gates

Use this reference when setting up or refining GitHub Actions CI pipelines for .NET quality enforcement.

## Minimal Quality Workflow

Start with this baseline workflow for most .NET repositories:

```yaml
name: .NET Quality

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  quality:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -warnaserror

      - name: Test
        run: dotnet test --no-build --verbosity normal

      - name: Format check
        run: dotnet format --verify-no-changes --verbosity diagnostic
```

## Full Quality Pipeline with Coverage

Extend the baseline with coverage collection and reporting:

```yaml
name: .NET Quality Full

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release -warnaserror

      - name: Test with coverage
        run: |
          dotnet test --no-build --configuration Release \
            --collect:"XPlat Code Coverage" \
            --results-directory ./coverage

      - name: Generate coverage report
        run: |
          dotnet tool install -g dotnet-reportgenerator-globaltool
          reportgenerator \
            -reports:"coverage/**/coverage.cobertura.xml" \
            -targetdir:"coverage/report" \
            -reporttypes:"HtmlSummary;Cobertura;MarkdownSummaryGithub"

      - name: Upload coverage report
        uses: actions/upload-artifact@v7
        with:
          name: coverage-report
          path: coverage/report

      - name: Format check
        run: dotnet format --verify-no-changes

  analyze:
    runs-on: ubuntu-latest
    needs: build-and-test
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Restore
        run: dotnet restore

      - name: Build with analysis
        run: |
          dotnet build --no-restore --configuration Release \
            -p:EnforceCodeStyleInBuild=true \
            -p:TreatWarningsAsErrors=true
```

## Separate Jobs Pattern

For larger repositories, split quality gates into parallel jobs:

```yaml
name: .NET Quality Gates

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  DOTNET_NOLOGO: true

jobs:
  format:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'
      - run: dotnet restore
      - run: dotnet format --verify-no-changes

  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -warnaserror

  test:
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build

  analyze:
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'
      - run: dotnet restore
      - name: Run analyzers
        run: |
          dotnet build --no-restore \
            -p:EnforceCodeStyleInBuild=true \
            -p:TreatWarningsAsErrors=true
```

## Coverage with Microsoft.Testing.Platform

For projects using Microsoft.Testing.Platform instead of VSTest:

```yaml
name: .NET MTP Coverage

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Test with MTP coverage
        run: |
          dotnet test --no-build \
            --coverlet-coverage \
            --coverlet-output-format cobertura \
            --coverlet-output ./coverage/

      - name: Generate report
        run: |
          dotnet tool install -g dotnet-reportgenerator-globaltool
          reportgenerator \
            -reports:"coverage/**/*.cobertura.xml" \
            -targetdir:"coverage/report" \
            -reporttypes:"HtmlSummary;Cobertura"

      - name: Upload coverage
        uses: actions/upload-artifact@v7
        with:
          name: coverage
          path: coverage/report
```

## Matrix Build for Multiple .NET Versions

Test against multiple .NET versions:

```yaml
name: .NET Multi-Version

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        dotnet-version: ['8.x', '9.x']
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET ${{ matrix.dotnet-version }}
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet-version }}

      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build
```

## CodeQL Security Scanning

Add GitHub CodeQL for security analysis:

```yaml
name: CodeQL

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    - cron: '0 6 * * 1'

jobs:
  analyze:
    runs-on: ubuntu-latest
    permissions:
      security-events: write
      actions: read
      contents: read

    steps:
      - uses: actions/checkout@v4

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: csharp

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Build
        run: |
          dotnet restore
          dotnet build --no-restore

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3
```

## Mutation Testing with Stryker

Add mutation testing for critical libraries:

```yaml
name: Mutation Testing

on:
  push:
    branches: [main]
  schedule:
    - cron: '0 4 * * 0'

jobs:
  mutate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Install Stryker
        run: dotnet tool install -g dotnet-stryker

      - name: Restore
        run: dotnet restore

      - name: Run Stryker
        run: dotnet stryker --reporter html --reporter progress
        working-directory: ./tests/MyProject.Tests

      - name: Upload mutation report
        uses: actions/upload-artifact@v7
        with:
          name: stryker-report
          path: '**/StrykerOutput/**'
```

## Architecture Tests Integration

Run architecture tests as part of the test job:

```yaml
name: Architecture Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  architecture:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Run architecture tests
        run: dotnet test --no-build --filter "Category=Architecture"
```

## ReSharper Command Line Tools

Use JetBrains InspectCode for deep analysis:

```yaml
name: ReSharper Inspection

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  inspect:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'

      - name: Install ReSharper CLT
        run: dotnet tool install -g JetBrains.ReSharper.GlobalTools

      - name: Restore
        run: dotnet restore

      - name: Run InspectCode
        run: |
          jb inspectcode MySolution.sln \
            --output=inspectcode-report.xml \
            --format=Xml \
            --severity=WARNING

      - name: Upload inspection report
        uses: actions/upload-artifact@v7
        with:
          name: inspectcode-report
          path: inspectcode-report.xml
```

## Caching Dependencies

Add caching to speed up workflow runs:

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/Directory.Build.props') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```

## Sources

- [actions/setup-dotnet](https://github.com/actions/setup-dotnet)
- [actions/cache](https://github.com/actions/cache)
- [github/codeql-action](https://github.com/github/codeql-action)
- [dotnet test command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
- [dotnet format command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- [Stryker.NET](https://github.com/stryker-mutator/stryker-net)
- [ReSharper command line tools](https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html)
