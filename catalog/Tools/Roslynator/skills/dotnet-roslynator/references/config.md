# Roslynator EditorConfig Configuration

This document provides `.editorconfig` settings for configuring Roslynator analyzers.

## Basic Setup

Roslynator respects standard `.editorconfig` files. Place configuration in your solution's root `.editorconfig` or in nested `.editorconfig` files for directory-specific settings.

## Rule Severity Configuration

Configure rule severity using the standard diagnostic severity format:

```ini
[*.cs]
# Syntax: dotnet_diagnostic.<rule_id>.severity = <severity>
# Severities: none, silent, suggestion, warning, error

# Example: Disable a rule
dotnet_diagnostic.RCS1090.severity = none

# Example: Elevate to warning
dotnet_diagnostic.RCS1163.severity = warning

# Example: Treat as error
dotnet_diagnostic.RCS1155.severity = error
```

## Roslynator-Specific Options

Roslynator provides additional configuration options beyond severity:

```ini
[*.cs]
# Configure specific rule behavior
roslynator_accessibility_modifiers = explicit
roslynator_accessor_braces_style = single_line_when_expression_is_on_single_line
roslynator_array_creation_type_style = implicit_when_type_is_obvious
roslynator_arrow_token_new_line = before
roslynator_binary_operator_new_line = before
roslynator_blank_line_after_file_scoped_namespace_declaration = true
roslynator_blank_line_between_closing_brace_and_switch_section = false
roslynator_blank_line_between_single_line_accessors = false
roslynator_blank_line_between_using_directives = never
roslynator_block_braces_style = single_line_when_empty
roslynator_body_style = expression
roslynator_conditional_operator_condition_parentheses_style = include
roslynator_conditional_operator_new_line = before
roslynator_configure_await = true
roslynator_doc_comment_summary_style = multi_line
roslynator_empty_string_style = literal
roslynator_enum_flag_value_style = decimal_number
roslynator_enum_has_flag_style = method
roslynator_equals_token_new_line = before
roslynator_infinite_loop_style = while
roslynator_max_line_length = 140
roslynator_new_line_at_end_of_file = true
roslynator_new_line_before_while_in_do_statement = true
roslynator_null_check_style = pattern_matching
roslynator_null_conditional_operator_new_line = before
roslynator_object_creation_parentheses_style = include
roslynator_object_creation_type_style = implicit_when_type_is_obvious
roslynator_prefix_field_identifier_with_underscore = true
roslynator_suppress_unity_script_methods = true
roslynator_tab_length = 4
roslynator_trailing_comma_style = omit_when_single_line
roslynator_unity_code_analysis.enabled = false
roslynator_use_anonymous_function_or_method_group = method_group
roslynator_use_block_body_when_declaration_spans_over_multiple_lines = true
roslynator_use_block_body_when_expression_spans_over_multiple_lines = true
roslynator_use_collection_expression = true
roslynator_use_var = when_type_is_obvious
roslynator_use_var_instead_of_implicit_object_creation = false
```

## Refactoring Configuration

Enable or disable specific refactorings:

```ini
[*.cs]
# Syntax: roslynator_refactoring.<id>.enabled = true|false

# Example: Disable "Add braces" refactoring
roslynator_refactoring.RR0002.enabled = false

# Example: Enable "Extract method" refactoring
roslynator_refactoring.RR0047.enabled = true
```

## Compiler Diagnostic Configuration

Configure standard compiler diagnostics alongside Roslynator:

```ini
[*.cs]
# Syntax: roslynator_compiler_diagnostic.<id>.enabled = true|false

# Example: Include specific compiler diagnostics in analysis
roslynator_compiler_diagnostic.CS0162.enabled = true
```

## Complete Configuration Templates

### Minimal Strict Configuration

A minimal configuration that enforces important code quality rules:

```ini
# .editorconfig
root = true

[*.cs]
# Core code quality rules - elevate to warning
dotnet_diagnostic.RCS1155.severity = warning  # Use StringComparison
dotnet_diagnostic.RCS1163.severity = warning  # Unused parameter
dotnet_diagnostic.RCS1169.severity = warning  # Make field read-only
dotnet_diagnostic.RCS1213.severity = warning  # Remove unused member
dotnet_diagnostic.RCS1225.severity = warning  # Make class sealed
dotnet_diagnostic.RCS1210.severity = warning  # Return completed task

# Performance rules
dotnet_diagnostic.RCS1077.severity = warning  # Optimize LINQ
dotnet_diagnostic.RCS1080.severity = warning  # Use Count/Length

# Architecture rules
dotnet_diagnostic.RCS1102.severity = warning  # Make class static
dotnet_diagnostic.RCS1110.severity = warning  # Type inside namespace

# Disable noisy rules
dotnet_diagnostic.RCS1090.severity = none     # ConfigureAwait (if not library)
dotnet_diagnostic.RCS1036.severity = none     # Remove blank line
```

### Library/Package Configuration

Configuration suitable for libraries distributed via NuGet:

```ini
# .editorconfig
root = true

[*.cs]
# API design rules - enforce strictly
dotnet_diagnostic.RCS1110.severity = error    # Type inside namespace
dotnet_diagnostic.RCS1194.severity = error    # Exception constructors
dotnet_diagnostic.RCS1203.severity = error    # AttributeUsage
dotnet_diagnostic.RCS1160.severity = error    # Abstract public constructors
dotnet_diagnostic.RCS1193.severity = error    # params modifier

# Async rules - important for libraries
dotnet_diagnostic.RCS1090.severity = warning  # ConfigureAwait
dotnet_diagnostic.RCS1210.severity = error    # Return completed task
dotnet_diagnostic.RCS1046.severity = warning  # Async suffix
dotnet_diagnostic.RCS1047.severity = warning  # Non-async suffix

# Documentation
dotnet_diagnostic.RCS1138.severity = warning  # Summary required
dotnet_diagnostic.RCS1139.severity = warning  # Summary element
dotnet_diagnostic.RCS1141.severity = warning  # param element
dotnet_diagnostic.RCS1142.severity = warning  # typeparam element
dotnet_diagnostic.RCS1263.severity = error    # Invalid doc reference

# Code quality
dotnet_diagnostic.RCS1155.severity = error    # StringComparison
dotnet_diagnostic.RCS1163.severity = warning  # Unused parameter
dotnet_diagnostic.RCS1169.severity = warning  # Read-only field
dotnet_diagnostic.RCS1213.severity = warning  # Unused member

# Roslynator options
roslynator_configure_await = true
roslynator_accessibility_modifiers = explicit
roslynator_prefix_field_identifier_with_underscore = true
```

### Application Configuration

Configuration suitable for applications (not libraries):

```ini
# .editorconfig
root = true

[*.cs]
# Disable library-specific rules
dotnet_diagnostic.RCS1090.severity = none     # ConfigureAwait
dotnet_diagnostic.RCS1046.severity = none     # Async suffix

# Enable code quality
dotnet_diagnostic.RCS1155.severity = warning  # StringComparison
dotnet_diagnostic.RCS1163.severity = warning  # Unused parameter
dotnet_diagnostic.RCS1169.severity = warning  # Read-only field
dotnet_diagnostic.RCS1213.severity = warning  # Unused member
dotnet_diagnostic.RCS1225.severity = warning  # Sealed class
dotnet_diagnostic.RCS1102.severity = warning  # Static class

# Performance
dotnet_diagnostic.RCS1077.severity = warning  # Optimize LINQ
dotnet_diagnostic.RCS1080.severity = warning  # Count/Length
dotnet_diagnostic.RCS1197.severity = warning  # StringBuilder
dotnet_diagnostic.RCS1235.severity = warning  # Optimize method

# Simplification
dotnet_diagnostic.RCS1049.severity = suggestion
dotnet_diagnostic.RCS1058.severity = suggestion
dotnet_diagnostic.RCS1068.severity = suggestion
dotnet_diagnostic.RCS1084.severity = suggestion

# Roslynator options
roslynator_configure_await = false
roslynator_use_var = when_type_is_obvious
roslynator_object_creation_type_style = implicit_when_type_is_obvious
```

### Test Project Configuration

Configuration suitable for test projects:

```ini
# tests/.editorconfig
[*.cs]
# Relax some rules for test code
dotnet_diagnostic.RCS1163.severity = suggestion  # Unused parameter (common in tests)
dotnet_diagnostic.RCS1213.severity = suggestion  # Unused member (test helpers)
dotnet_diagnostic.RCS1225.severity = none        # Sealed (test classes often inherited)
dotnet_diagnostic.RCS1090.severity = none        # ConfigureAwait
dotnet_diagnostic.RCS1046.severity = none        # Async suffix

# Keep important rules
dotnet_diagnostic.RCS1155.severity = warning     # StringComparison
dotnet_diagnostic.RCS1077.severity = suggestion  # LINQ optimization

# Allow more flexibility
roslynator_use_var = always
```

## Global Suppression

To suppress rules project-wide via `GlobalSuppressions.cs`:

```csharp
// GlobalSuppressions.cs
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Roslynator", "RCS1090:Add call to 'ConfigureAwait'")]
[assembly: SuppressMessage("Roslynator", "RCS1036:Remove unnecessary blank line")]
```

## Inline Suppression

Suppress rules inline with pragma directives:

```csharp
#pragma warning disable RCS1163 // Unused parameter
public void Method(string unused)
#pragma warning restore RCS1163
{
    // ...
}
```

Or with attributes:

```csharp
[SuppressMessage("Roslynator", "RCS1163:Unused parameter")]
public void Method(string unused)
{
    // ...
}
```

## Combining with Other Analyzers

When using Roslynator alongside other analyzers (SDK analyzers, StyleCop, Meziantou), establish clear ownership:

```ini
[*.cs]
# Disable Roslynator formatting when using dotnet format or CSharpier
dotnet_diagnostic.RCS0001.severity = none
dotnet_diagnostic.RCS0002.severity = none
# ... other RCS0xxx rules

# Disable overlapping rules if StyleCop handles them
dotnet_diagnostic.RCS1018.severity = none  # Accessibility (SA1400)
dotnet_diagnostic.RCS1019.severity = none  # Modifier order (SA1206)

# Let SDK analyzers handle nullability
# Roslynator complements but doesn't replace CS8xxx rules
```

## CLI-Specific Configuration

When using Roslynator CLI, additional options can be passed:

```bash
# Analyze with specific severity minimum
roslynator analyze Solution.sln --severity-level warning

# Fix with specific rules
roslynator fix Solution.sln --diagnostic-ids RCS1163,RCS1213

# Find unused code
roslynator find-unused Solution.sln
```

The CLI respects `.editorconfig` settings for rule configuration.

## Further Reading

- [Roslynator Configuration Documentation](https://josefpihrt.github.io/docs/roslynator/configuration)
- [EditorConfig Specification](https://editorconfig.org/)
- [.NET Code Analysis Configuration](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files)
