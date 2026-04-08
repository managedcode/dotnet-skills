# .NET EditorConfig and CI Ownership

Use this reference when the repository needs one durable source of truth for formatting, naming, style, and analyzer severity.

## Ownership Rules

- repo-root lowercase `.editorconfig` is the default source of truth for .NET formatting, naming, code-style, and analyzer severity.
- nested `.editorconfig` files are allowed when they serve a clear subtree-specific purpose.
- `.globalconfig` is an exceptional fallback, not the normal repo setup.
- `Directory.Build.props`, `Directory.Build.targets`, or project files should own bulk switches such as:
  - `EnableNETAnalyzers`
  - `AnalysisLevel`
  - `TreatWarningsAsErrors`
  - runner selection such as `UseVSTest`
- the root `.editorconfig` should own per-rule severity and code-style detail.

## CI Rules

- `dotnet format` reads `.editorconfig`; use `dotnet format --verify-no-changes` in CI.
- `dotnet build -warnaserror` or the repo's equivalent should enforce analyzer severity.
- Coverage driver must match the runner:
  - VSTest: `coverlet.collector` or `--collect:"XPlat Code Coverage"`
  - Microsoft.Testing.Platform: `coverlet.MTP` or MSTest SDK coverage extensions
- Report generation is a separate step. Use ReportGenerator after coverage collection if humans need HTML, Markdown, or badges.

## Conflict Rules

- Do not let IDE-only settings, CI-only flags, and repo config disagree about the same rule.
- Do not write inline comments in `.editorconfig` values; they are not part of the current EditorConfig spec.
- Do not let `CSharpier` and `dotnet format` both own formatting unless the split is explicit and documented.
- Do not mix VSTest-only switches, filters, or `.runsettings` assumptions into Microsoft.Testing.Platform jobs.
- Keep `stylecop.json` for StyleCop behavior only; do not treat it as the repo-wide severity owner.

## Good AGENTS.md Signals for .NET

- exact `build`, `test`, `format`, `analyze`, and `coverage` commands
- exact test runner model: `VSTest` or `Microsoft.Testing.Platform`
- exact framework skill: `dotnet-xunit`, `dotnet-tunit`, or `dotnet-mstest`
- explicit statement that the repo-root `.editorconfig` is the analyzer source of truth

## Sources

- [dotnet format command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
- [Configuration files for code analysis rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
- [Overview of .NET source code analysis](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview)
