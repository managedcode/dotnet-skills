# CodeQL GitHub Actions Setup for .NET

## Basic Workflow

Create `.github/workflows/codeql.yml`:

```yaml
name: "CodeQL"

on:
  push:
    branches: [main, master]
  pull_request:
    branches: [main, master]
  schedule:
    - cron: '30 5 * * 1'  # Weekly Monday 5:30 AM UTC

jobs:
  analyze:
    name: Analyze
    runs-on: ubuntu-latest
    permissions:
      actions: read
      contents: read
      security-events: write

    strategy:
      fail-fast: false
      matrix:
        language: ['csharp']

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: ${{ matrix.language }}

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build
        run: dotnet build --configuration Release

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3
        with:
          category: "/language:${{ matrix.language }}"
```

## Build Modes

### Manual Build (Recommended for .NET)

Explicit control over the build process:

```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    build-mode: manual

- name: Build
  run: |
    dotnet restore
    dotnet build --no-restore --configuration Release
```

### Autobuild

Let CodeQL detect and run the build:

```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    build-mode: autobuild
```

Autobuild limitations:

- May not find all projects in complex solutions
- Custom build steps are not executed
- May miss conditional compilation

### None (Interpreted Languages Only)

Not applicable for C#/.NET compiled code.

## Advanced Configuration

### Custom Query Suite

Create `.github/codeql/codeql-config.yml`:

```yaml
name: "Custom CodeQL Config"

queries:
  - uses: security-extended
  - uses: security-and-quality

paths:
  - src

paths-ignore:
  - "**/Tests/**"
  - "**/*.Designer.cs"
  - "**/Migrations/**"
  - "**/obj/**"
  - "**/bin/**"
```

Reference in workflow:

```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    config-file: .github/codeql/codeql-config.yml
```

### Multi-Project Solutions

For solutions with multiple projects:

```yaml
- name: Build Solution
  run: |
    dotnet restore MySolution.sln
    dotnet build MySolution.sln --no-restore -c Release

# Or build specific projects
- name: Build Projects
  run: |
    dotnet build src/Api/Api.csproj -c Release
    dotnet build src/Core/Core.csproj -c Release
```

### .NET Framework Projects

For legacy .NET Framework:

```yaml
jobs:
  analyze:
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v4

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: csharp
          build-mode: manual

      - name: Setup MSBuild
        uses: microsoft/setup-msbuild@v2

      - name: Setup NuGet
        uses: NuGet/setup-nuget@v2

      - name: Restore NuGet packages
        run: nuget restore MySolution.sln

      - name: Build
        run: msbuild MySolution.sln /p:Configuration=Release

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3
```

## Workflow Triggers

### On Pull Request

```yaml
on:
  pull_request:
    branches: [main]
    paths:
      - '**.cs'
      - '**.csproj'
      - '**.sln'
```

### Scheduled Scans

```yaml
on:
  schedule:
    # Daily at 2 AM UTC
    - cron: '0 2 * * *'
```

### Manual Trigger

```yaml
on:
  workflow_dispatch:
    inputs:
      query-suite:
        description: 'Query suite to use'
        required: false
        default: 'security-extended'
```

## SARIF Upload and Results

### Upload to GitHub Security Tab

Automatic with `github/codeql-action/analyze`:

```yaml
- name: Perform CodeQL Analysis
  uses: github/codeql-action/analyze@v3
  with:
    category: "/language:csharp"
    output: sarif-results
    upload: always  # or 'failure-only' or 'never'
```

### Upload Custom SARIF

```yaml
- name: Upload SARIF
  uses: github/codeql-action/upload-sarif@v3
  with:
    sarif_file: results.sarif
    category: "custom-analysis"
```

### Artifact Storage

```yaml
- name: Upload SARIF as artifact
  uses: actions/upload-artifact@v7
  with:
    name: sarif-results
    path: sarif-results
    retention-days: 5
```

## Performance Optimization

### Caching Dependencies

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```

### Parallel Analysis

```yaml
strategy:
  fail-fast: false
  matrix:
    include:
      - project: src/Api/Api.csproj
        name: api
      - project: src/Web/Web.csproj
        name: web
```

### Timeout Configuration

```yaml
jobs:
  analyze:
    timeout-minutes: 60
```

## Security Permissions

### Minimum Required Permissions

```yaml
permissions:
  actions: read        # Required for workflow runs
  contents: read       # Required to checkout code
  security-events: write  # Required to upload SARIF
```

### For Pull Requests from Forks

```yaml
permissions:
  pull-requests: read
  security-events: write
```

## Troubleshooting

### Debug Logging

```yaml
- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    languages: csharp
    debug: true
```

### Build Failures

Check autobuild logs:

```yaml
- name: Autobuild
  uses: github/codeql-action/autobuild@v3
  continue-on-error: true

- name: Manual build fallback
  if: failure()
  run: dotnet build
```

### Database Verification

```yaml
- name: Check database
  run: |
    ls -la ${{ runner.temp }}/codeql_databases/
```

## Private Repository Licensing

For private repositories:

- GitHub Advanced Security license required for GitHub-hosted scanning
- Self-hosted runners with CodeQL CLI may have different licensing
- Verify licensing requirements with GitHub before enabling

## Sources

- [CodeQL Action documentation](https://github.com/github/codeql-action)
- [Configuring CodeQL scanning](https://docs.github.com/en/code-security/code-scanning/creating-an-advanced-setup-for-code-scanning/customizing-your-advanced-setup-for-code-scanning)
- [CodeQL CLI manual](https://codeql.github.com/docs/codeql-cli/)
