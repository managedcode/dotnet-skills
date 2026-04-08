# MSBuild Properties and .editorconfig for Code Analysis

## MSBuild Properties

Set in `Directory.Build.props` or project files.

### EnableNETAnalyzers

```xml
<EnableNETAnalyzers>true</EnableNETAnalyzers>
```
Default true in .NET 5+. Set explicitly to prevent accidental disabling.

### AnalysisLevel

Values: `5.0`–`10.0` (specific SDK), `latest`, `latest-recommended`, `latest-minimum`, `latest-all`, `preview`.

Combined syntax includes mode: `<AnalysisLevel>latest-recommended</AnalysisLevel>` equals `latest` + `Recommended` mode.

### AnalysisMode

Values: `None` (all off), `Default`, `Minimum` (critical only), `Recommended` (start here), `All`.

### Category-Specific AnalysisMode

```xml
<AnalysisLevel>latest-recommended</AnalysisLevel>
<AnalysisModeSecurity>All</AnalysisModeSecurity>
<AnalysisModeReliability>All</AnalysisModeReliability>
```
Categories: Design, Documentation, Globalization, Interoperability, Maintainability, Naming, Performance, Reliability, Security, Usage.

### TreatWarningsAsErrors

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```
All warnings fail the build. Use for new projects or clean codebases. Agent rule: never disable to make a build pass.

### WarningsAsErrors (selective — preferred for legacy)

```xml
<WarningsAsErrors>CS8019;CS0219;CS0168;CA2000;CA3001</WarningsAsErrors>
```
Promote specific IDs to errors. Preferred for gradual adoption: add IDs as you fix each batch. Agent rule: never remove IDs from this list to make a build pass.

### WarningsNotAsErrors

```xml
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<WarningsNotAsErrors>CA1707</WarningsNotAsErrors>
```
Explicit exceptions when using TreatWarningsAsErrors.

### NoWarn

```xml
<NoWarn>$(NoWarn);CA1062</NoWarn>
```
Disables warnings entirely. Use sparingly; prefer .editorconfig for visibility.

### EnforceCodeStyleInBuild

```xml
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```
Enables IDE code style rules during build (off by default for performance).

## .editorconfig

Place at repo root. Rules cascade to subdirectories.

### Severity per rule

```editorconfig
[*.cs]
dotnet_diagnostic.CA2000.severity = error
dotnet_diagnostic.CA1707.severity = none
```

### Severity per category (.NET 6+)

```editorconfig
[*.cs]
dotnet_analyzer_diagnostic.category-Security.severity = error
dotnet_analyzer_diagnostic.category-Performance.severity = warning
```

### Scope patterns

```editorconfig
[**/Tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1062.severity = none

[*.generated.cs]
generated_code = true
```

### Code Style Settings

```editorconfig
[*.cs]
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:warning
csharp_style_pattern_matching_over_is_with_cast_check = true:warning
csharp_style_prefer_null_check_over_type_check = true:suggestion
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
```

### Formatting Settings

```editorconfig
[*.cs]
indent_style = space
indent_size = 4
tab_width = 4
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
```

## Recommended Starting Configuration

### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### .editorconfig (root)

```editorconfig
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
dotnet_analyzer_diagnostic.category-Security.severity = error
dotnet_analyzer_diagnostic.category-Reliability.severity = warning

[**/Tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1062.severity = none
```

## Verification

```bash
dotnet build
dotnet build /p:ReportAnalyzer=true
```

## References

- [AnalysisLevel](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#analysislevel)
- [Configuration options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options)
- [EditorConfig](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
- [Suppress warnings](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/suppress-warnings)
