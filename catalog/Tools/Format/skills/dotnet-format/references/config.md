# .editorconfig Settings for dotnet format

Use this reference for `.editorconfig` configuration that controls `dotnet format` behavior.

## File Structure

```text
repo-root/
  .editorconfig          # Root config, source of truth
  src/
    .editorconfig        # Optional: subtree overrides
  tests/
    .editorconfig        # Optional: relaxed rules for tests
```

## Root .editorconfig Template

```ini
# Root EditorConfig
root = true

# All files
[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# C# files
[*.cs]
indent_size = 4

# XML files
[*.{xml,csproj,props,targets}]
indent_size = 2

# JSON files
[*.json]
indent_size = 2

# YAML files
[*.{yml,yaml}]
indent_size = 2
```

## C# Code Style Options

### Using Directives

```ini
[*.cs]
# Sort System.* usings first
dotnet_sort_system_directives_first = true

# Place usings outside namespace
csharp_using_directive_placement = outside_namespace:warning
```

### Namespace Declarations

```ini
[*.cs]
# File-scoped namespaces (C# 10+)
csharp_style_namespace_declarations = file_scoped:warning

# Block-scoped namespaces (traditional)
# csharp_style_namespace_declarations = block_scoped:warning
```

### Expression-bodied Members

```ini
[*.cs]
# Methods
csharp_style_expression_bodied_methods = when_on_single_line:suggestion

# Constructors
csharp_style_expression_bodied_constructors = false:suggestion

# Properties
csharp_style_expression_bodied_properties = true:suggestion

# Accessors
csharp_style_expression_bodied_accessors = true:suggestion

# Lambdas
csharp_style_expression_bodied_lambdas = true:suggestion

# Local functions
csharp_style_expression_bodied_local_functions = when_on_single_line:suggestion
```

### Pattern Matching

```ini
[*.cs]
# Prefer pattern matching over is/as
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion

# Prefer not pattern
csharp_style_prefer_not_pattern = true:suggestion

# Prefer extended property pattern
csharp_style_prefer_extended_property_pattern = true:suggestion
```

### Null Checking

```ini
[*.cs]
# Prefer null-coalescing
csharp_style_coalesce_expression = true:suggestion

# Prefer null-conditional
csharp_style_conditional_delegate_call = true:suggestion

# Prefer throw expression
csharp_style_throw_expression = true:suggestion

# Prefer null-propagation
dotnet_style_null_propagation = true:suggestion
```

### var Preferences

```ini
[*.cs]
# Use var when type is apparent
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
```

### Braces

```ini
[*.cs]
# Prefer braces
csharp_prefer_braces = true:warning

# Allow single-line (when_multiline)
# csharp_prefer_braces = when_multiline:suggestion
```

### New Line Preferences

```ini
[*.cs]
# New line before open brace
csharp_new_line_before_open_brace = all

# New line before else
csharp_new_line_before_else = true

# New line before catch
csharp_new_line_before_catch = true

# New line before finally
csharp_new_line_before_finally = true

# New line before members in object initializers
csharp_new_line_before_members_in_object_initializers = true

# New line before members in anonymous types
csharp_new_line_before_members_in_anonymous_types = true

# New line between query expression clauses
csharp_new_line_between_query_expression_clauses = true
```

### Indentation Preferences

```ini
[*.cs]
# Indent case contents
csharp_indent_case_contents = true

# Indent switch labels
csharp_indent_switch_labels = true

# Indent block contents
csharp_indent_block_contents = true

# Indent braces
csharp_indent_braces = false

# Indent case contents when block
csharp_indent_case_contents_when_block = false
```

### Spacing Preferences

```ini
[*.cs]
# Space after cast
csharp_space_after_cast = false

# Space after keywords in control flow
csharp_space_after_keywords_in_control_flow_statements = true

# Space between method declaration parameter list parentheses
csharp_space_between_method_declaration_parameter_list_parentheses = false

# Space between method call parameter list parentheses
csharp_space_between_method_call_parameter_list_parentheses = false

# Space before colon in inheritance clause
csharp_space_before_colon_in_inheritance_clause = true

# Space after colon in inheritance clause
csharp_space_after_colon_in_inheritance_clause = true

# Space around binary operators
csharp_space_around_binary_operators = before_and_after
```

## Naming Conventions

```ini
[*.cs]
# PascalCase for public members
dotnet_naming_rule.public_members_should_be_pascal_case.severity = warning
dotnet_naming_rule.public_members_should_be_pascal_case.symbols = public_symbols
dotnet_naming_rule.public_members_should_be_pascal_case.style = pascal_case_style

dotnet_naming_symbols.public_symbols.applicable_kinds = property,method,field,event,delegate
dotnet_naming_symbols.public_symbols.applicable_accessibilities = public

dotnet_naming_style.pascal_case_style.capitalization = pascal_case

# camelCase for private fields
dotnet_naming_rule.private_fields_should_be_camel_case.severity = warning
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_style

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_style.capitalization = camel_case

# _camelCase for private fields with underscore prefix
dotnet_naming_rule.private_fields_should_be_underscore_camel.severity = warning
dotnet_naming_rule.private_fields_should_be_underscore_camel.symbols = private_fields_underscore
dotnet_naming_rule.private_fields_should_be_underscore_camel.style = underscore_camel_style

dotnet_naming_symbols.private_fields_underscore.applicable_kinds = field
dotnet_naming_symbols.private_fields_underscore.applicable_accessibilities = private

dotnet_naming_style.underscore_camel_style.capitalization = camel_case
dotnet_naming_style.underscore_camel_style.required_prefix = _

# Interfaces should begin with I
dotnet_naming_rule.interface_should_begin_with_i.severity = warning
dotnet_naming_rule.interface_should_begin_with_i.symbols = interface_symbols
dotnet_naming_rule.interface_should_begin_with_i.style = interface_style

dotnet_naming_symbols.interface_symbols.applicable_kinds = interface
dotnet_naming_symbols.interface_symbols.applicable_accessibilities = *

dotnet_naming_style.interface_style.capitalization = pascal_case
dotnet_naming_style.interface_style.required_prefix = I
```

## Analyzer Severity Configuration

```ini
[*.cs]
# IDE0005: Remove unnecessary usings
dotnet_diagnostic.IDE0005.severity = warning

# IDE0051: Remove unused private members
dotnet_diagnostic.IDE0051.severity = warning

# IDE0052: Remove unread private members
dotnet_diagnostic.IDE0052.severity = warning

# IDE0055: Fix formatting
dotnet_diagnostic.IDE0055.severity = warning

# IDE0060: Remove unused parameter
dotnet_diagnostic.IDE0060.severity = suggestion

# IDE0161: Convert to file-scoped namespace
dotnet_diagnostic.IDE0161.severity = warning

# CA1848: Use LoggerMessage delegates
dotnet_diagnostic.CA1848.severity = suggestion

# CA2007: Consider calling ConfigureAwait
dotnet_diagnostic.CA2007.severity = none
```

## Test Project Relaxations

```ini
# tests/.editorconfig
[*.cs]
# Allow more flexibility in test naming
dotnet_naming_rule.public_members_should_be_pascal_case.severity = suggestion

# Relax unused parameter warnings for test methods
dotnet_diagnostic.IDE0060.severity = none

# Allow async void for test setup/teardown
dotnet_diagnostic.VSTHRD100.severity = none
```

## Generated Code Exclusions

```ini
# Exclude generated files
[*.Designer.cs]
generated_code = true

[*.g.cs]
generated_code = true

[**/obj/**/*.cs]
generated_code = true
```

## Severity Levels

| Level | Meaning |
|-------|---------|
| `none` | Disable rule |
| `silent` | Not shown in editor, not in build |
| `suggestion` | Show as dots/suggestion |
| `warning` | Show as warning |
| `error` | Show as error, fail build |

## Verification

```bash
# Verify editorconfig is applied
dotnet format --verify-no-changes --verbosity diagnostic

# Check specific style rules
dotnet format style --verify-no-changes

# Check specific analyzer rules
dotnet format analyzers --verify-no-changes
```

## Sources

- [EditorConfig settings](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options)
- [.NET code style rule options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/)
- [Naming rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules)
- [Configuration files for code analysis](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
