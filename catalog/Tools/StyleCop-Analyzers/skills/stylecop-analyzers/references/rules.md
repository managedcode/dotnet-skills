# StyleCop Analyzers Rules Reference

## Rule Categories

StyleCop Analyzers organizes rules into these categories:

| Prefix | Category | Description |
|--------|----------|-------------|
| SA0xxx | Special Rules | Analyzer configuration and suppression |
| SA1xxx | Spacing Rules | Whitespace and spacing conventions |
| SA12xx | Readability Rules | Code readability and clarity |
| SA13xx | Ordering Rules | Element and using directive ordering |
| SA14xx | Maintainability Rules | Code maintainability patterns |
| SA15xx | Layout Rules | File and element layout conventions |
| SA16xx | Documentation Rules | XML documentation requirements |
| SA17xx | Naming Rules | Naming conventions |
| SX1xxx | Alternative Rules | Alternative versions of existing rules |

## Commonly Adjusted Rules

### Spacing Rules (SA1xxx)

| Rule | Description | Common Setting |
|------|-------------|----------------|
| SA1000 | Keywords should be spaced correctly | warning |
| SA1001 | Commas should be spaced correctly | warning |
| SA1005 | Single-line comments should begin with space | warning |
| SA1009 | Closing parenthesis should be spaced correctly | warning |
| SA1011 | Closing square brackets should be spaced correctly | warning |
| SA1028 | Code should not contain trailing whitespace | warning |

### Readability Rules (SA12xx)

| Rule | Description | Common Setting |
|------|-------------|----------------|
| SA1101 | Prefix local calls with this | none (often disabled) |
| SA1116 | Split parameters should start on line after declaration | warning |
| SA1117 | Parameters should be on same line or separate lines | warning |
| SA1118 | Parameter should not span multiple lines | suggestion |
| SA1122 | Use string.Empty for empty strings | warning |
| SA1127 | Generic type constraints should be on their own line | warning |
| SA1128 | Put constructor initializers on their own line | warning |
| SA1133 | Do not combine attributes | suggestion |

### Ordering Rules (SA13xx)

| Rule | Description | Common Setting |
|------|-------------|----------------|
| SA1200 | Using directives should be placed correctly | warning |
| SA1201 | Elements should appear in the correct order | warning |
| SA1202 | Elements should be ordered by access | warning |
| SA1203 | Constants should appear before fields | warning |
| SA1204 | Static elements should appear before instance elements | warning |
| SA1208 | System using directives should be placed before other usings | warning |
| SA1210 | Using directives should be ordered alphabetically | suggestion |
| SA1211 | Using alias directives should be ordered alphabetically | suggestion |

### Maintainability Rules (SA14xx)

| Rule | Description | Common Setting |
|------|-------------|----------------|
| SA1400 | Access modifier should be declared | warning |
| SA1401 | Fields should be private | warning |
| SA1402 | File may only contain a single type | warning |
| SA1403 | File may only contain a single namespace | warning |
| SA1404 | Code analysis suppression should have justification | warning |
| SA1405 | Debug.Assert should provide message text | suggestion |
| SA1413 | Use trailing comma in multi-line initializers | none (often disabled) |

### Layout Rules (SA15xx)

| Rule | Description | Common Setting |
|------|-------------|----------------|
| SA1500 | Braces for multi-line statements should not share line | warning |
| SA1501 | Statement should not be on a single line | warning |
| SA1502 | Element should not be on a single line | suggestion |
| SA1503 | Braces should not be omitted | warning |
| SA1504 | All accessors should be single-line or multi-line | warning |
| SA1505 | Opening braces should not be followed by blank line | warning |
| SA1508 | Closing braces should not be preceded by blank line | warning |
| SA1512 | Single-line comments should not be followed by blank line | none (often disabled) |
| SA1515 | Single-line comment should be preceded by blank line | none (often disabled) |
| SA1516 | Elements should be separated by blank line | warning |
| SA1519 | Braces should not be omitted from multi-line child statement | warning |

### Documentation Rules (SA16xx)

| Rule | Description | Common Setting |
|------|-------------|----------------|
| SA1600 | Elements should be documented | none or suggestion |
| SA1601 | Partial elements should be documented | none |
| SA1602 | Enumeration items should be documented | none or suggestion |
| SA1604 | Element documentation should have summary | warning |
| SA1611 | Element parameters should be documented | suggestion |
| SA1614 | Element parameter documentation should have text | suggestion |
| SA1615 | Element return value should be documented | suggestion |
| SA1618 | Generic type parameters should be documented | suggestion |
| SA1623 | Property summary documentation should match accessors | warning |
| SA1629 | Documentation text should end with a period | suggestion |
| SA1633 | File should have header | none (often disabled) |
| SA1634 | File header should show copyright text | none |
| SA1649 | File name should match first type name | warning |

### Naming Rules (SA17xx)

| Rule | Description | Common Setting |
|------|-------------|----------------|
| SA1300 | Element should begin with upper-case letter | warning |
| SA1302 | Interface names should begin with I | warning |
| SA1303 | Const field names should begin with upper-case letter | warning |
| SA1304 | Non-private readonly fields should begin with upper-case letter | warning |
| SA1306 | Field names should begin with lower-case letter | warning |
| SA1307 | Accessible fields should begin with upper-case letter | warning |
| SA1309 | Field names should not begin with underscore | warning or none |
| SA1310 | Field names should not contain underscore | warning |
| SA1311 | Static readonly fields should begin with upper-case letter | warning |

## Alternative Rules (SXxxxx)

| Rule | Description | Use Case |
|------|-------------|----------|
| SX1101 | Do not prefix local calls with this | Alternative to SA1101 |
| SX1309 | Field names should begin with underscore | For `_field` convention |
| SX1309S | Static field names should begin with underscore | For `_staticField` convention |

## .editorconfig Configuration Examples

### Minimal Enforcement

```ini
[*.cs]
# Disable verbose documentation requirements
dotnet_diagnostic.SA1600.severity = none
dotnet_diagnostic.SA1601.severity = none
dotnet_diagnostic.SA1602.severity = none
dotnet_diagnostic.SA1633.severity = none

# Disable this-prefix requirement
dotnet_diagnostic.SA1101.severity = none

# Allow underscore prefix for fields
dotnet_diagnostic.SA1309.severity = none
```

### Strict Enforcement

```ini
[*.cs]
# Enable most rules as warnings
dotnet_analyzer_diagnostic.category-StyleCop.CSharp.DocumentationRules.severity = warning
dotnet_analyzer_diagnostic.category-StyleCop.CSharp.LayoutRules.severity = warning
dotnet_analyzer_diagnostic.category-StyleCop.CSharp.MaintainabilityRules.severity = warning
dotnet_analyzer_diagnostic.category-StyleCop.CSharp.NamingRules.severity = warning
dotnet_analyzer_diagnostic.category-StyleCop.CSharp.OrderingRules.severity = warning
dotnet_analyzer_diagnostic.category-StyleCop.CSharp.ReadabilityRules.severity = warning
dotnet_analyzer_diagnostic.category-StyleCop.CSharp.SpacingRules.severity = warning

# Exceptions
dotnet_diagnostic.SA1101.severity = none
dotnet_diagnostic.SA1633.severity = none
```

### Common Rule Combinations

For teams using underscore-prefixed private fields:

```ini
[*.cs]
dotnet_diagnostic.SA1309.severity = none
dotnet_diagnostic.SX1309.severity = warning
```

For teams not requiring file headers:

```ini
[*.cs]
dotnet_diagnostic.SA1633.severity = none
dotnet_diagnostic.SA1634.severity = none
dotnet_diagnostic.SA1635.severity = none
dotnet_diagnostic.SA1636.severity = none
dotnet_diagnostic.SA1637.severity = none
dotnet_diagnostic.SA1638.severity = none
dotnet_diagnostic.SA1640.severity = none
dotnet_diagnostic.SA1641.severity = none
```

## Rule Conflicts

### With SDK Analyzers

Some StyleCop rules overlap with SDK analyzers:

- IDE0003/IDE0009 vs SA1101: `this.` qualification
- IDE0055 vs SA1000-SA1028: Spacing rules
- IDE0161 vs SA1402: File-scoped namespaces

Resolution: Disable the less-preferred analyzer in `.editorconfig`.

### With Roslynator

- RCS1018 vs SA1400: Access modifiers
- RCS1090 vs SA1101: `this.` prefix
- Multiple formatting overlaps

Resolution: Choose one analyzer pack as the authority for each rule category.

## Sources

- [StyleCop Analyzers Rules](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/tree/master/documentation)
- [Rule Documentation Index](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/DOCUMENTATION.md)
