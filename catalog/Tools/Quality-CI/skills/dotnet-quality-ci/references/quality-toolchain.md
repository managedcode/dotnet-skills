# Recommended OSS .NET Quality Toolchain

Use this reference when a .NET repository needs an explicit, open-source-first quality stack for CI.
Each listed tool now has a dedicated skill folder, so use this page for selection and use the matching tool skill for installation and detailed setup.

Open/free policy for this catalog:

- everything listed here is open source or free to adopt locally
- `CodeQL` stays in the catalog with an explicit caveat because the hosted GitHub private-repo experience is not universally free

Install policy for this catalog:

- always verify first whether the tool is already present in `*.csproj`, `Directory.Build.*`, `.config/dotnet-tools.json`, or CI workflows
- prefer the repo's existing installation path over adding a second package, tool, or runner

## Baseline Stack

Use this baseline for most .NET repositories before adding anything exotic:

| Area | Tool | Skill | Why |
| --- | --- | --- | --- |
| Format and style | `dotnet format` | `dotnet-format` | Native CLI support, reads `.editorconfig`, supports verify-only mode in CI |
| Built-in static analysis | SDK analyzers / `Microsoft.CodeAnalysis.NetAnalyzers` | `dotnet-code-analysis` | First-party CA and IDE rules, enabled by default on modern .NET |
| Complexity and maintainability | `CA1502`/`CA1505`/`CA1506` | `dotnet-complexity` | Finds overly complex methods, low maintainability index, and excessive coupling |
| Analyzer config | repo-root `.editorconfig` | `dotnet-analyzer-config` | One durable source of truth for style and rule severity |
| Style and conventions | `StyleCopAnalyzers` | `dotnet-stylecop-analyzers` | Strong naming, layout, documentation, and consistency checks |
| General code quality | `Roslynator` | `dotnet-roslynator` | Broad Roslyn analyzer set and optional CLI |
| General code quality | `Meziantou.Analyzer` | `dotnet-meziantou-analyzer` | Design, usage, security, performance, and style rules |
| Coverage collection | `coverlet` | `dotnet-coverlet` | Cross-platform line, branch, and method coverage |
| Coverage reporting | `ReportGenerator` | `dotnet-reportgenerator` | Converts coverage artifacts into HTML, Markdown, Cobertura, badges, and more |

## Framework-Specific Additions

Pick the test-framework add-on that matches the repo:

| Framework | Tool | Skill | Why |
| --- | --- | --- | --- |
| xUnit | `xunit.analyzers` | `dotnet-xunit` | xUnit-specific correctness and usage rules |
| TUnit | built-in TUnit analyzers | `dotnet-tunit` | Compile-time guidance for signatures, attributes, and usage |
| MSTest | `MSTest.Analyzers` via `MSTest` / `MSTest.Sdk` | `dotnet-mstest` | MSTest-specific correctness and usage rules |

## High-Value Optional Gates

These tools are worth adding once the baseline is stable:

| Area | Tool | Skill | Why | Notes |
| --- | --- | --- | --- | --- |
| Mutation testing | `Stryker.NET` | `dotnet-stryker` | Verifies that tests actually catch faults | Best on libraries and critical domains, not every PR path |
| Architecture tests | `NetArchTest.Rules` | `dotnet-netarchtest` | Simple, fluent architectural rules in tests | Good for layered or clean architecture policies |
| Architecture tests | `ArchUnitNET` | `dotnet-archunitnet` | Richer architecture assertions across xUnit, MSTest, and TUnit | Heavier than NetArchTest but more expressive |
| Deep inspections and cleanup | `JetBrains ReSharper Command Line Tools` | `dotnet-resharper-clt` | Powerful ReSharper inspections plus cleanup profiles in CI or local runs | Free official JetBrains CLI package; keep shared policy in solution `.DotSettings` |
| Security scanning | `CodeQL` | `dotnet-codeql` | Deep GitHub-native query-based analysis | Open ecosystem with private-repo hosting caveats |
| Opinionated formatter | `CSharpier` | `dotnet-csharpier` | Fast one-style formatter for C# and XML | Use only if the repo wants a formatter owner beyond `dotnet format` |

## Complexity Strategy

There is no dominant open-source, CI-native .NET complexity suite that cleanly replaces NDepend.

For OSS-first repos, use a composite gate:

1. `CA1502` with an explicit threshold for excessive cyclomatic complexity.
2. Maintainability limits in `AGENTS.md`:
   - `file_max_loc`
   - `type_max_loc`
   - `function_max_loc`
   - `max_nesting_depth`
3. Architecture tests with NetArchTest or ArchUnitNET.
4. Coverage and mutation testing on critical paths.

For complex methods specifically, the primary built-in analyzer is `CA1502`.

If the repo later chooses a commercial metric product, treat it as an additional gate, not a replacement for the design and analyzer baseline.

## Suggested CI Order

```bash
dotnet restore
dotnet build MySolution.sln -warnaserror
dotnet test MySolution.sln --no-build
dotnet format MySolution.sln --verify-no-changes
```

## Agent Post-Change Flow

When an agent writes or refactors `.NET` code, do not stop after `dotnet test`.

Use the exact commands from `AGENTS.md`. The usual checked-in flow is:

1. `format`
2. `build`
3. `analyze`
4. focused `test`
5. broader `test`
6. `coverage` and report generation when configured
7. extra configured gates such as Roslynator, StyleCop, Meziantou, ReSharper CLT, architecture tests, CodeQL, CSharpier, or Stryker

Run only the gates the repo actually enabled.

Then add the runner-specific extras:

- VSTest coverage:

```bash
dotnet test MySolution.sln --collect:"XPlat Code Coverage"
```

- Microsoft.Testing.Platform coverage:

```bash
dotnet test MySolution.sln --coverlet
```

- Report generation:

```bash
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"artifacts/coverage" -reporttypes:"HtmlSummary;Cobertura"
```

- Mutation testing:

```bash
dotnet stryker
```

## Sources

- [dotnet format command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [Overview of .NET source code analysis](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
- [Configuration files for code analysis rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
- [CA1502: Avoid excessive complexity](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1502)
- [Code metrics values](https://learn.microsoft.com/en-us/visualstudio/code-quality/code-metrics-values?view=vs-2022)
- [StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [Roslynator](https://github.com/JosefPihrt/Roslynator)
- [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer)
- [Coverlet](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- [Stryker.NET](https://github.com/stryker-mutator/stryker-net)
- [NetArchTest](https://github.com/BenMorris/NetArchTest)
- [ArchUnitNET](https://github.com/TNG/ArchUnitNET)
- [ReSharper command line tools](https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html)
- [CleanupCode](https://www.jetbrains.com/help/resharper/CleanupCode.html)
- [InspectCode](https://www.jetbrains.com/help/resharper/InspectCode.html)
- [CodeQL code scanning](https://docs.github.com/en/code-security/code-scanning/introduction-to-code-scanning/about-code-scanning-with-codeql)
- [CSharpier](https://github.com/belav/csharpier)
