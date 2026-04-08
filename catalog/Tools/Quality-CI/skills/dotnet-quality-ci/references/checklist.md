# .NET CI Quality Checklist

Use this checklist when setting up or auditing quality gates for a .NET repository.

## Foundation Checklist

### Repository Configuration

- [ ] Repo-root `.editorconfig` exists and owns formatting, naming, and analyzer severity
- [ ] `Directory.Build.props` exists for shared MSBuild properties
- [ ] `EnableNETAnalyzers` is explicitly set to `true`
- [ ] `AnalysisLevel` is set to `latest` or a specific version
- [ ] `TreatWarningsAsErrors` policy is documented and applied consistently
- [ ] `.config/dotnet-tools.json` tracks local tool versions if tools are used

### Build Configuration

- [ ] Solution builds with `dotnet build -warnaserror`
- [ ] Build uses deterministic output settings for reproducibility
- [ ] SDK version is pinned in `global.json` if consistency is required
- [ ] Build logs no analyzer suppressions without documented reason

## Formatting and Style

### dotnet format

- [ ] `dotnet format --verify-no-changes` runs in CI
- [ ] Format failures block PR merge
- [ ] Format rules are in `.editorconfig`, not scattered across IDE settings
- [ ] Whitespace, indentation, and naming rules are explicit

### Optional Formatter

- [ ] If CSharpier is used, ownership split with `dotnet format` is documented
- [ ] Formatter choice is documented in `AGENTS.md`
- [ ] Pre-commit hooks or CI enforce the chosen formatter

## Static Analysis

### SDK Analyzers

- [ ] `Microsoft.CodeAnalysis.NetAnalyzers` is enabled (default in .NET 5+)
- [ ] CA rules are configured in `.editorconfig` with explicit severity
- [ ] `CA1502` (cyclomatic complexity) threshold is set if complexity matters
- [ ] `CA1505` (maintainability index) is enabled for maintainability gates
- [ ] `CA1506` (class coupling) is enabled for architecture guidance

### Third-Party Analyzers

- [ ] Analyzer packages serve distinct purposes (no redundant overlap)
- [ ] `StyleCopAnalyzers` configured if naming/documentation rules needed
- [ ] `Roslynator` configured if broader Roslyn rules needed
- [ ] `Meziantou.Analyzer` configured if security/performance rules needed
- [ ] Framework-specific analyzers installed (xUnit, MSTest, or TUnit)

### Analyzer Configuration

- [ ] All analyzer rules have explicit severity in `.editorconfig`
- [ ] Suppressed rules are documented with reason
- [ ] `stylecop.json` is minimal and does not duplicate `.editorconfig`
- [ ] No analyzer warnings are silently ignored

## Testing

### Test Runner

- [ ] Test runner model is documented: VSTest or Microsoft.Testing.Platform
- [ ] Test command is explicit in `AGENTS.md` and CI
- [ ] Tests pass locally and in CI with same command
- [ ] Test filters work correctly for focused and broad test runs

### Test Quality

- [ ] Unit tests exist for business logic
- [ ] Integration tests exist for external dependencies
- [ ] Test naming follows consistent convention
- [ ] Test organization matches production code structure

## Code Coverage

### Coverage Collection

- [ ] Coverage driver matches test runner:
  - VSTest: `coverlet.collector` or `--collect:"XPlat Code Coverage"`
  - MTP: `coverlet.MTP` or MSTest SDK coverage extensions
- [ ] Coverage output format is specified (Cobertura recommended)
- [ ] Coverage artifacts are generated in CI

### Coverage Reporting

- [ ] ReportGenerator or equivalent produces human-readable reports
- [ ] Coverage report is uploaded as CI artifact
- [ ] Coverage thresholds are documented if enforced
- [ ] Coverage badge is generated if displayed in README

## Architecture and Security

### Architecture Tests

- [ ] Architecture test project exists if layer boundaries matter
- [ ] NetArchTest or ArchUnitNET is configured
- [ ] Dependency rules are explicit and tested
- [ ] Architecture tests run in CI

### Security Scanning

- [ ] CodeQL is configured for GitHub repositories (with hosting caveat noted)
- [ ] Security advisories are monitored via Dependabot or equivalent
- [ ] Secrets scanning is enabled
- [ ] Container scanning if Docker is used

## Advanced Quality Gates

### Mutation Testing

- [ ] Stryker.NET is configured for critical libraries
- [ ] Mutation testing runs on schedule (not every PR)
- [ ] Mutation score threshold is documented
- [ ] Mutation reports are reviewed periodically

### Complexity Management

- [ ] `CA1502` threshold is set for max cyclomatic complexity
- [ ] `AGENTS.md` documents:
  - `file_max_loc`
  - `type_max_loc`
  - `function_max_loc`
  - `max_nesting_depth`
- [ ] Complex methods are flagged in code review
- [ ] Refactoring is prioritized for high-complexity code

## CI Workflow

### Workflow Structure

- [ ] Quality workflow triggers on push to main and PRs
- [ ] Jobs are ordered logically: restore, build, test, format, analyze
- [ ] Independent jobs run in parallel where possible
- [ ] Dependent jobs use `needs` to express dependencies
- [ ] Workflow uses caching for NuGet packages

### Workflow Reliability

- [ ] Workflow uses pinned action versions (not `@main` or `@latest`)
- [ ] .NET SDK version is explicit in `setup-dotnet`
- [ ] Environment variables reduce CLI noise (`DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`)
- [ ] Timeouts are set for long-running jobs
- [ ] Artifacts are uploaded for reports and coverage

### Workflow Maintenance

- [ ] Workflow files are reviewed in PRs
- [ ] Workflow changes are tested in feature branches
- [ ] Deprecated actions are updated promptly
- [ ] Workflow documentation exists in `AGENTS.md` or `CONTRIBUTING.md`

## Documentation

### AGENTS.md Quality Section

- [ ] Exact `build` command is documented
- [ ] Exact `test` command is documented
- [ ] Exact `format` command is documented
- [ ] Exact `analyze` command is documented
- [ ] Exact `coverage` command is documented (if enabled)
- [ ] Test runner model is stated: VSTest or Microsoft.Testing.Platform
- [ ] Quality gate ownership is clear for each category

### Contributor Documentation

- [ ] Quality gate setup is documented in `CONTRIBUTING.md`
- [ ] Local quality check commands are documented
- [ ] Pre-commit hook setup is documented (if used)
- [ ] IDE configuration recommendations exist

## Verification Commands

Run these commands to verify quality gate setup:

```bash
# Verify formatting
dotnet format --verify-no-changes

# Verify build with warnings as errors
dotnet build -warnaserror

# Verify tests pass
dotnet test

# Verify coverage collection (VSTest)
dotnet test --collect:"XPlat Code Coverage"

# Verify analyzers run
dotnet build -p:EnforceCodeStyleInBuild=true

# Verify architecture tests (if configured)
dotnet test --filter "Category=Architecture"
```

## Common Issues Checklist

- [ ] No conflicting ownership between `dotnet format` and CSharpier
- [ ] No VSTest switches in Microsoft.Testing.Platform projects
- [ ] No `.runsettings` assumptions that break on MTP
- [ ] No analyzer packages that duplicate SDK analyzer rules
- [ ] No coverage drivers mismatched with test runner
- [ ] No hand-edited generated files that drift from source
- [ ] No quality gates that only run locally and skip CI
- [ ] No suppressed warnings without documented justification

## Sources

- [Overview of .NET source code analysis](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
- [Configuration files for code analysis rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
- [dotnet format command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [Code metrics values](https://learn.microsoft.com/en-us/visualstudio/code-quality/code-metrics-values?view=vs-2022)
- [GitHub Actions for .NET](https://docs.github.com/en/actions/use-cases-and-examples/building-and-testing/building-and-testing-net)
