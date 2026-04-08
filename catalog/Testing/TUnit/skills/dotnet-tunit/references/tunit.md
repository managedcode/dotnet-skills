# TUnit in MCAF

## Open/Free Status

- open source
- free to use

## Install

Template-based start:

```bash
dotnet new install TUnit.Templates
dotnet new TUnit -n MyTestProject
```

## Verify First

Before adding packages or templates, check whether the repo already uses TUnit:

```bash
rg -n "PackageReference Include=\"TUnit\"|\\[Test\\]|\\[Arguments\\]|ParallelLimiter|DependsOn" -g '*.csproj' -g '*.cs' .
```

Use this reference when the repository already chose TUnit and you need the right commands, expectations, and CI integration points.

For shared AppHost fixtures, `WebApplicationFactory`, Playwright UI harnesses, and log/artifact capture, load `integration-testing.md`.

## Detect TUnit

Look for the package and its common attributes:

```bash
rg -n "PackageReference Include=\"TUnit\"|\\[Test\\]|\\[Arguments\\]|ParallelLimiter|DependsOn" -g '*.csproj' -g '*.cs' .
```

TUnit is built on Microsoft.Testing.Platform, uses source generation for test discovery, and runs tests in parallel by default.

## Common Commands

Start with the repo's `test` command from `AGENTS.md`. If the repo has not documented one yet, these are the safe defaults:

```bash
dotnet test MySolution.sln
dotnet test tests/MyProject.Tests/MyProject.Tests.csproj
dotnet test tests/MyProject.Tests/MyProject.Tests.csproj -- --treenode-filter "/*/*/MyTestClass/*"
dotnet test MySolution.sln -- --coverage --coverage-output coverage.cobertura.xml --coverage-output-format cobertura
dotnet run --project tests/MyProject.Tests/MyProject.Tests.csproj -- --help
```

New-project quick start from the official template:

```bash
cd MyTestProject
dotnet run
```

## CI Notes

- Assume concurrency unless the repo has explicitly limited it.
- Do not share mutable static state, temp paths, or fixed ports across tests.
- Build once, then re-run focused projects with `--no-build` where the repo supports it.
- For coverage on Microsoft.Testing.Platform, prefer the repo's documented MTP coverage switches such as `--coverage --coverage-output ...`.
- Publish human-readable reports separately with ReportGenerator if the pipeline needs HTML or Markdown summaries.
- For integration/UI suites, capture first-failure evidence: host log dumps, screenshots, and HTML artifacts.

## Good Defaults

- use `[Arguments]` for stable parameterized cases
- use `[DependsOn]` sparingly and only for real orchestration constraints
- use `[ParallelLimiter]` locally when a boundary genuinely cannot run at full concurrency
- keep hooks small and explicit so parallel failures stay diagnosable

## Sources

- [TUnit GitHub repository](https://github.com/thomhurst/TUnit)
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro)
