# xUnit in MCAF

## Open/Free Status

- open source
- free to use

## Install

xUnit v3 package setup:

```bash
dotnet add package xunit.v3
```

VSTest compatibility package when the repo intentionally uses that runner:

```bash
dotnet add package xunit.runner.visualstudio
```

## Verify First

Before adding packages, check what the repo already references:

```bash
rg -n "xunit(\\.v3)?|xunit\\.runner\\.visualstudio|TestingPlatformDotnetTestSupport|UseMicrosoftTestingPlatformRunner" -g '*.csproj' .
```

Use this reference when the repository already chose xUnit and you need framework-specific commands, package checks, or CI guardrails.

## Detect the xUnit Model

Use the project file as the source of truth:

```bash
rg -n "xunit\\.v3|xunit.runner.visualstudio|Microsoft\\.NET\\.Test\\.Sdk|TestingPlatformDotnetTestSupport|UseMicrosoftTestingPlatformRunner" -g '*.csproj' .
```

Typical markers:

- xUnit v2: `xunit`
- xUnit v3: `xunit.v3`
- VSTest compatibility: `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`
- Microsoft.Testing.Platform support: `TestingPlatformDotnetTestSupport` or `UseMicrosoftTestingPlatformRunner`

## Common Commands

Start with the repo's `test` command from `AGENTS.md`. If the repo has not documented one yet, these are the safe defaults:

```bash
dotnet test MySolution.sln
dotnet test tests/MyProject.Tests/MyProject.Tests.csproj
dotnet test tests/MyProject.Tests/MyProject.Tests.csproj --no-build
```

Focused VSTest-style run:

```bash
dotnet test tests/MyProject.Tests/MyProject.Tests.csproj --filter "FullyQualifiedName~Namespace.TypeName"
```

xUnit v3 standalone runner:

```bash
dotnet run --project tests/MyProject.Tests/MyProject.Tests.csproj
```

xUnit v3 with Microsoft.Testing.Platform-style class filtering:

```bash
dotnet run --project tests/MyProject.Tests/MyProject.Tests.csproj -- --filter-class Namespace.TypeName
```

If the project enables `TestingPlatformDotnetTestSupport`, `dotnet test` can forward into Microsoft.Testing.Platform. Keep those switches consistent with the runner the project actually uses.

## CI Notes

- Use one runner model per project. Do not mix VSTest-only flags and Microsoft.Testing.Platform flags in the same command.
- Build first, then use `--no-build` for repeat test runs.
- Keep coverage driver aligned with the runner:
  - VSTest: `coverlet.collector` or `--collect:"XPlat Code Coverage"`
  - Microsoft.Testing.Platform: `coverlet.MTP`
- Keep xUnit analyzers on:
  - xUnit v2 2.3+ usually brings them through the main `xunit` package
  - xUnit v3 brings analyzer guidance through the main package set unless the repo split packages explicitly

## Good Defaults

- prefer `[Theory]` plus stable inline or member data for variant-heavy behavior
- use traits only when the repo already relies on them for filtering
- avoid runner rewrites in the same change as behavior work unless the current command is already broken

## Sources

- [xUnit.net v3 getting started](https://xunit.net/docs/getting-started/v3/getting-started)
- [xUnit.net v3 Microsoft Testing Platform support](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
- [xunit/xunit.analyzers](https://github.com/xunit/xunit.analyzers)
