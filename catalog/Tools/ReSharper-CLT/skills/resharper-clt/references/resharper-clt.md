# JetBrains ReSharper Command Line Tools

## What This Skill Uses

This skill standardizes on the free official JetBrains package `JetBrains.ReSharper.GlobalTools`, exposed through the `jb` command.

Primary commands:

- `jb inspectcode`
- `jb cleanupcode`

Use this skill when the repo intentionally wants stronger ReSharper inspections and cleanup than the default SDK analyzers alone.

## Official Docs

- ReSharper Command Line Tools:
  - https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html
- CleanupCode:
  - https://www.jetbrains.com/help/resharper/CleanupCode.html
- InspectCode:
  - https://www.jetbrains.com/help/resharper/InspectCode.html

## Installation Paths

Preferred repo-local install for reproducible CI:

```bash
dotnet new tool-manifest
dotnet tool install JetBrains.ReSharper.GlobalTools
dotnet tool restore
```

Global fallback:

```bash
dotnet tool install --global JetBrains.ReSharper.GlobalTools
```

Verify the commands:

```bash
jb inspectcode --help
jb cleanupcode --help
```

## Shared Settings

If the repo uses ReSharper CLI as a real gate, keep durable settings in the solution team-shared layer and commit the resulting file:

- `YourSolution.sln.DotSettings`

This is where cleanup profiles and many inspection settings become durable for the rest of the team.

Do not rely on user-specific settings files for repo policy.

## InspectCode

Use `InspectCode` to surface problems that must then be fixed.

Basic run:

```bash
jb inspectcode YourSolution.sln -o=artifacts/inspectcode.sarif
```

Important details from the official docs:

- starting with ReSharper 2024.1, SARIF is the default output format
- XML is still available through `-f="xml"`
- output path is controlled through `-o` or `--output`

Examples:

```bash
jb inspectcode YourSolution.sln -o=artifacts/inspectcode.sarif
jb inspectcode YourSolution.sln -f=Html -o=artifacts/inspectcode.html
jb inspectcode YourSolution.sln -f=Xml -o=artifacts/inspectcode.xml
```

Use `InspectCode` before and after cleanup or code fixes so you can prove the issue count actually moved in the right direction.

## CleanupCode

Use `CleanupCode` to apply a selected cleanup profile over a solution, project, or bounded file set.

Solution-wide run:

```bash
jb cleanupcode YourSolution.sln --profile="Built-in: Full Cleanup"
```

Focused reformat-only run:

```bash
jb cleanupcode YourSolution.sln --profile="Built-in: Reformat Code"
```

Useful profile options from the official docs:

- `Built-in: Full Cleanup`
- `Built-in: Reformat Code`
- `Built-in: Reformat & Apply Syntax Style`

Useful scope controls:

- `--include`
- `--exclude`

Example:

```bash
jb cleanupcode YourSolution.sln --profile="Built-in: Reformat & Apply Syntax Style" --include="src/**/*.cs"
```

Important official note:

- build the solution first when running solution-wide cleanup, otherwise binary references may not resolve correctly

## Recommended Flow

1. Build the solution in `Release`.
2. Run `jb inspectcode`.
3. Fix or clean up the surfaced issues.
4. Run `jb cleanupcode` with an explicit profile.
5. Run `jb inspectcode` again.
6. Run the repo's analyzers and tests.

## Gate Policy

If the repo enables ReSharper CLI as a quality gate:

- surfaced issues are not informational only
- the task is not done while the agreed blocking issues remain
- cleanup must be followed by tests and broader verification

Do not stop at generating a SARIF file.
