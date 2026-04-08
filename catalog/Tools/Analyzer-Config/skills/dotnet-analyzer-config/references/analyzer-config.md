# .NET Root .editorconfig

## Open/Free Status

- built-in .NET configuration system
- free to use

## Default Placement

The default MCAF path is:

- one repo-root lowercase `.editorconfig`
- `root = true` at the top
- optional nested `.editorconfig` files when a subtree has a clear scoped purpose

## Verify First

Before adding a new config file, check whether the repo already has one:

```bash
rg --files -g '.editorconfig' -g '.globalconfig'
```

## File Format

`EditorConfig` is an INI-style format:

- section names are path globs like `[*.cs]` or `[src/**/*.cs]`
- files are read from the current file directory upward
- `root = true` stops the search
- closer matching `.editorconfig` files override earlier parent rules
- later matching sections in the same file win
- comments must be on their own line and start with `#` or `;`
- inline comments are not part of the format
- the file should be UTF-8 with `LF` or `CRLF` line separators
- forward slashes are the path separator in section globs
- keys and values are case-insensitive
- `unset` removes the effect of a previously set property

## Core Rules That Matter for .NET

- only `root` belongs in the preamble for effect outside sections
- all other effective pairs belong under a matching section such as `[*.cs]`
- unknown keys are allowed by the EditorConfig format, which is why `.NET`-specific keys like `dotnet_diagnostic.*`, `dotnet_style_*`, and `csharp_*` can live in `.editorconfig`
- unsupported keys or values may be ignored by a given plugin or tool, so keep repo expectations tied to tools that actually honor them

## Glob Rules That People Commonly Get Wrong

- `*.cs` can match at any level below the directory that contains the `.editorconfig`
- `src/*.cs` is relative to the directory that contains that `.editorconfig`
- if a glob contains `/`, use `/`, never `\\`
- a section ending with `/` does not match a file
- a leading `/` does not add extra meaning if the glob already contains a `/`

## Comments

Good:

```ini
# C# rules
[*.cs]
dotnet_diagnostic.CA1502.severity = warning
```

Bad:

```ini
[*.cs]
dotnet_diagnostic.CA1502.severity = warning # inline comment
```

In the bad example, the trailing `# inline comment` is not a comment under the current specification.

## Recommended Root Layout

Start with one root file like this:

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_style = space
indent_size = 4
dotnet_analyzer_diagnostic.severity = warning
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_event = false:suggestion

[*.{csproj,props,targets}]
indent_style = space
indent_size = 2
```

Then add rule-specific severity in the same root file:

```ini
root = true

[*.cs]
dotnet_diagnostic.CA1000.severity = warning
dotnet_diagnostic.CA1502.severity = warning
dotnet_diagnostic.SA1200.severity = warning
dotnet_diagnostic.RCS1036.severity = error
```

## Scoped Nested .editorconfig

Add a nested `.editorconfig` when a subtree has a real local purpose, for example:

- tighter rules in a core domain
- relaxed rules for generated code
- different test-project conventions
- legacy-code containment during gradual cleanup

Example:

```ini
# src/LegacyModule/.editorconfig
[*.cs]
dotnet_diagnostic.CA1502.severity = error
```

## CI Fit

- `dotnet format` and .NET analyzers both read `.editorconfig`
- repo-root `.editorconfig` should be versioned and treated as the default source of truth
- keep MSBuild switches such as `EnableNETAnalyzers` and `AnalysisLevel` in project files or `Directory.Build.props`
- keep the file lowercase as `.editorconfig`

## Exceptional Use of .globalconfig

Use `.globalconfig` only when you have a real reason, for example:

- analyzer config distributed by package or SDK conventions
- special project layouts where file-tree inheritance is not the right model

That is not the default MCAF recommendation for normal .NET repos.

## When Not To Use

- when the repo is trying to store analyzer policy only in IDE profiles or ephemeral local settings

## Sources

- [EditorConfig home and file location rules](https://editorconfig.org/)
- [EditorConfig formal specification](https://spec.editorconfig.org/)
- [Configuration files for code analysis rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
- [Code style rule options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options)
