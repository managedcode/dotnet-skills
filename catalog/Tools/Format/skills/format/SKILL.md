---
name: format
description: "Format or verify trusted .NET projects with the SDK-provided `dotnet format` command. USE FOR: applying or checking `.editorconfig`-driven whitespace, code-style, or analyzer fixes; adding a `--verify-no-changes` CI gate; diagnosing formatter scope or load failures. DO NOT USE FOR: repositories where another formatter exclusively owns the affected files; analyzer policy with no formatter work; mutating files when the user requested only diagnosis."
---

# dotnet format

Use the formatter already shipped with the selected .NET SDK. Preserve the repository's formatter ownership, existing changes, and configured style instead of introducing new preferences.

## Workflow

1. Read the nearest `AGENTS.md`, `global.json`, solution/project files, `.editorconfig`, and current Git status.
2. Confirm the exact trusted workspace to load. `dotnet format` may restore, compile, and run analyzers from that workspace.
3. Determine whether the request is read-only verification or permission to apply fixes. Do not run a mutating command for a review, explanation, or diagnosis request.
4. Preserve the current diff before formatting. When scope is uncertain, begin with `--verify-no-changes` or a narrow `--include` list.
5. Choose the smallest formatter surface that matches the request:
   - `whitespace` for indentation, spacing, and line-ending rules;
   - `style` for built-in .NET code-style diagnostics;
   - `analyzers` for fixable non-style analyzer diagnostics;
   - the command without a subcommand only when all applicable surfaces are intended.
6. Treat `--include` and `--exclude` values as workspace-relative file or directory paths, not shell globs. Use `--diagnostics` to narrow style or analyzer fixes by rule ID.
7. After a mutating run, inspect `git diff --stat`, representative diffs, line endings, and every changed file. If scope is unexpectedly broad, stop and narrow the command; never discard pre-existing user changes.
8. Rerun the matching command with `--verify-no-changes`. When analyzer fixes were applied, also build and run the tests relevant to the changed behavior.

## Invariants

- `.editorconfig` and existing MSBuild analyzer configuration are the source of truth. Do not add an arbitrary style template unless the user asks for one.
- Use `--no-restore` only after dependencies have already been restored successfully.
- Generated files stay excluded unless the repository explicitly owns and formats them.
- A successful formatter process does not prove that every diagnostic has an automatic fix. Review its output or JSON report and use build/analyzer results as the final evidence.
- Keep formatter responsibilities explicit when CSharpier, ReSharper cleanup, generated-code tools, or other formatters coexist.

## References

- Read [references/commands.md](references/commands.md) for precise local, CI, filtering, and troubleshooting commands.
- Read [references/config.md](references/config.md) when changing `.editorconfig`, analyzer severity, or formatter ownership.
