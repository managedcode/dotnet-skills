# ReSharper DotSettings Configuration

## Settings Layer Hierarchy

ReSharper uses a layered settings system. From lowest to highest priority:

| Layer | File Pattern | Scope | Commit to VCS |
|-------|--------------|-------|---------------|
| Global | `%APPDATA%\JetBrains\...` | User machine | No |
| Solution Team-Shared | `SolutionName.sln.DotSettings` | Team/repo | Yes |
| Solution Personal | `SolutionName.sln.DotSettings.user` | User only | No |
| Project Team-Shared | `ProjectName.csproj.DotSettings` | Project team | Yes |
| Project Personal | `ProjectName.csproj.DotSettings.user` | User only | No |

For repository policy, use the **Solution Team-Shared** layer: `YourSolution.sln.DotSettings`.

---

## File Structure

`.DotSettings` files are XML with a specific namespace:

```xml
<wpf:ResourceDictionary xml:space="preserve"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:s="clr-namespace:System;assembly=mscorlib"
    xmlns:ss="urn:shemas-jetbrains-com:settings-storage-xaml"
    xmlns:wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation">

    <!-- Settings go here -->

</wpf:ResourceDictionary>
```

---

## Common Settings Categories

### Inspection Severity

Control the severity level of specific inspections:

```xml
<!-- Disable an inspection -->
<s:String x:Key="/Default/CodeInspection/Highlighting/InspectionSeverities/=UnusedMember_002EGlobal/@EntryIndexedValue">DO_NOT_SHOW</s:String>

<!-- Set to warning -->
<s:String x:Key="/Default/CodeInspection/Highlighting/InspectionSeverities/=ConvertToAutoProperty/@EntryIndexedValue">WARNING</s:String>

<!-- Set to error -->
<s:String x:Key="/Default/CodeInspection/Highlighting/InspectionSeverities/=PossibleNullReferenceException/@EntryIndexedValue">ERROR</s:String>
```

Severity values:

| Value | Meaning |
|-------|---------|
| `DO_NOT_SHOW` | Disabled |
| `HINT` | Hint (lowest) |
| `SUGGESTION` | Suggestion |
| `WARNING` | Warning |
| `ERROR` | Error (highest) |

### Code Formatting

Indentation and braces:

```xml
<!-- Use tabs instead of spaces -->
<s:String x:Key="/Default/CodeStyle/CodeFormatting/CSharpFormat/INDENT_STYLE/@EntryValue">TAB</s:String>

<!-- Indent size -->
<s:Int64 x:Key="/Default/CodeStyle/CodeFormatting/CSharpFormat/INDENT_SIZE/@EntryValue">4</s:Int64>

<!-- Brace style -->
<s:String x:Key="/Default/CodeStyle/CodeFormatting/CSharpFormat/BRACES_FOR_IFELSE/@EntryValue">REQUIRED_FOR_MULTILINE</s:String>
```

### Naming Rules

```xml
<!-- Private field naming: _camelCase -->
<s:String x:Key="/Default/CodeStyle/Naming/CSharpNaming/Abbreviations/=IO/@EntryIndexedValue">IO</s:String>

<!-- Type naming conventions -->
<s:String x:Key="/Default/CodeStyle/Naming/CSharpNaming/PredefinedNamingRules/=PrivateInstanceFields/@EntryIndexedValue">&lt;Policy Inspect="True" Prefix="_" Suffix="" Style="aaBb" /&gt;</s:String>
```

### File Header Template

```xml
<s:String x:Key="/Default/CodeStyle/FileHeader/FileHeaderText/@EntryValue">// Copyright (c) MyCompany. All rights reserved.
// Licensed under the MIT License.</s:String>
```

---

## Custom Cleanup Profiles

Define custom cleanup profiles in `.DotSettings`:

```xml
<!-- Define a custom profile -->
<s:String x:Key="/Default/CodeCleanup/Profiles/=MyCustomProfile/@EntryIndexedValue">&lt;Profile name="MyCustomProfile"&gt;
  &lt;CSReformatCode&gt;True&lt;/CSReformatCode&gt;
  &lt;CSOptimizeUsings&gt;
    &lt;OptimizeUsings&gt;True&lt;/OptimizeUsings&gt;
    &lt;EmbraceInRegion&gt;False&lt;/EmbraceInRegion&gt;
    &lt;RegionName&gt;&lt;/RegionName&gt;
  &lt;/CSOptimizeUsings&gt;
  &lt;CSShortenReferences&gt;True&lt;/CSShortenReferences&gt;
  &lt;CSReorderTypeMembers&gt;False&lt;/CSReorderTypeMembers&gt;
&lt;/Profile&gt;</s:String>
```

Use the custom profile:

```bash
jb cleanupcode MySolution.sln --profile="MyCustomProfile"
```

---

## Excluding Files and Folders

### Generated Code

Mark paths as generated code (skipped by many inspections):

```xml
<s:Boolean x:Key="/Default/CodeInspection/GeneratedCode/GeneratedFileMasks/=*_002Eg_002Ecs/@EntryIndexedValue">True</s:Boolean>
<s:Boolean x:Key="/Default/CodeInspection/GeneratedCode/GeneratedFileMasks/=*_002EDesigner_002Ecs/@EntryIndexedValue">True</s:Boolean>
```

### Skip Entire Folders

```xml
<s:String x:Key="/Default/CodeInspection/ExcludedFiles/FilesAndFoldersToSkip2/=7020124F_002D9FFC_002D4AC3_002D8F3D_002DAAB8E0240759_002Ff_003AGeneratedCode_002Ecs/@EntryIndexedValue">ExplicitlyExcluded</s:String>
```

### Suppress Warnings in Code

In-code suppression (add to the generated `.DotSettings` when appropriate):

```csharp
// ReSharper disable once UnusedMember.Global
public void SomeMethod() { }

// ReSharper disable UnusedMember.Global
// ... multiple members
// ReSharper restore UnusedMember.Global
```

---

## Complete Example

Minimal team-shared settings file:

```xml
<wpf:ResourceDictionary xml:space="preserve"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:s="clr-namespace:System;assembly=mscorlib"
    xmlns:ss="urn:shemas-jetbrains-com:settings-storage-xaml"
    xmlns:wpf="http://schemas.microsoft.com/winfx/2006/xaml/presentation">

    <!-- File header -->
    <s:String x:Key="/Default/CodeStyle/FileHeader/FileHeaderText/@EntryValue">// Copyright (c) MyCompany. All rights reserved.</s:String>

    <!-- Treat possible null reference as error -->
    <s:String x:Key="/Default/CodeInspection/Highlighting/InspectionSeverities/=PossibleNullReferenceException/@EntryIndexedValue">ERROR</s:String>

    <!-- Mark generated files -->
    <s:Boolean x:Key="/Default/CodeInspection/GeneratedCode/GeneratedFileMasks/=*_002Eg_002Ecs/@EntryIndexedValue">True</s:Boolean>
    <s:Boolean x:Key="/Default/CodeInspection/GeneratedCode/GeneratedFileMasks/=*_002EDesigner_002Ecs/@EntryIndexedValue">True</s:Boolean>

    <!-- Private field prefix -->
    <s:String x:Key="/Default/CodeStyle/Naming/CSharpNaming/PredefinedNamingRules/=PrivateInstanceFields/@EntryIndexedValue">&lt;Policy Inspect="True" Prefix="_" Suffix="" Style="aaBb" /&gt;</s:String>

</wpf:ResourceDictionary>
```

---

## EditorConfig Integration

ReSharper respects `.editorconfig` for many settings. Prefer `.editorconfig` for settings that should apply to all tools:

```ini
# .editorconfig
root = true

[*.cs]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# ReSharper-specific
resharper_csharp_braces_for_ifelse = required_for_multiline
resharper_csharp_braces_for_for = required_for_multiline
```

Use `.DotSettings` for settings not covered by `.editorconfig` or for ReSharper-specific features like inspection severities and custom cleanup profiles.

---

## CLI Settings Override

Override settings from the command line:

```bash
# Use a custom settings file
jb inspectcode MySolution.sln --dotSettings=ci-strict.DotSettings -o=artifacts/inspectcode.sarif

# Disable specific settings layers
jb cleanupcode MySolution.sln --profile="Built-in: Full Cleanup" --disable-settings-layers=SolutionPersonal
```

---

## Best Practices

1. Commit `YourSolution.sln.DotSettings` to version control.
2. Never commit `.DotSettings.user` files (add to `.gitignore`).
3. Keep cleanup profiles in the team-shared layer.
4. Document the expected profile in `AGENTS.md`.
5. Use `.editorconfig` for cross-tool settings.
6. Use `.DotSettings` for ReSharper-specific inspection and cleanup configuration.
7. Review settings changes in pull requests like any other code.
