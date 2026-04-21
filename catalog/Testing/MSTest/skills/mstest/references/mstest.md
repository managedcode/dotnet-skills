# MSTest in MCAF

## Open/Free Status

- open source
- free to use

## Install

Project template:

```bash
dotnet new mstest
```

Or add the current MSTest meta-package to an existing test project:

```bash
dotnet add package MSTest
```

## Verify First

Before adding packages, check which MSTest model the repo already uses:

```bash
rg -n "MSTest\\.Sdk|UseVSTest|PackageReference Include=\"MSTest\"|Microsoft\\.NET\\.Test\\.Sdk|TestingPlatformDotnetTestSupport" -g '*.csproj' .
```

Use this reference when the repository already chose MSTest and you need framework-specific commands, package checks, or CI guardrails.

## Detect the MSTest Model

Use the project file as the source of truth:

```bash
rg -n "MSTest\\.Sdk|UseVSTest|PackageReference Include=\"MSTest\"|Microsoft\\.NET\\.Test\\.Sdk|TestingPlatformDotnetTestSupport" -g '*.csproj' .
```

Typical markers:

- `MSTest.Sdk`: modern MSTest project SDK, Microsoft.Testing.Platform by default
- `UseVSTest=true`: explicit VSTest fallback
- `MSTest` meta-package: framework, adapter, analyzers, and runner-related packages packaged together

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

New-project template:

The current `dotnet new mstest` template can target either `VSTest` or `MSTest` as the runner, and `MSTest.Sdk` uses Microsoft.Testing.Platform by default.

## CI Notes

- Document the runner choice in `AGENTS.md`.
- If the project uses `MSTest.Sdk`, assume Microsoft.Testing.Platform unless the project opts back into VSTest.
- Keep coverage driver aligned with the runner:
  - VSTest: `--collect:"XPlat Code Coverage"` or `coverlet.collector`
  - Microsoft.Testing.Platform: MSTest SDK coverage extension or `coverlet.MTP`
- Keep MSTest analyzers enabled.

## Good Defaults

- prefer `[DataRow]` or `DynamicData` over duplicated test methods
- keep `ClassInitialize`, `ClassCleanup`, and similar hooks short and deterministic
- avoid runner migrations and behavior changes in the same PR unless the current runner setup is already broken

## Sources

- [Get started with MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-getting-started)
- [MSTest overview](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-intro)
- [MSTest SDK configuration](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-sdk)
- [Microsoft.Testing.Platform overview](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro)
