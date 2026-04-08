# StyleCop Analyzers Configuration Reference

## Configuration Files

StyleCop Analyzers uses two configuration sources:

| File | Purpose |
|------|---------|
| `.editorconfig` | Rule severity, enable/disable rules |
| `stylecop.json` | StyleCop-specific behavioral settings |

## stylecop.json Schema

Always include the schema reference for IDE support:

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    // Configuration sections go here
  }
}
```

## Configuration Sections

### documentationRules

Controls XML documentation behavior.

```json
{
  "settings": {
    "documentationRules": {
      "companyName": "Your Company",
      "copyrightText": "Copyright (c) {companyName}. All rights reserved.",
      "headerDecoration": "-----------------------------------------------------------------------",
      "documentExposedElements": true,
      "documentInternalElements": false,
      "documentPrivateElements": false,
      "documentInterfaces": true,
      "documentPrivateFields": false,
      "fileNamingConvention": "stylecop",
      "xmlHeader": false,
      "variables": {
        "customVariable": "value"
      },
      "excludeFromPunctuationCheck": ["seealso", "typeparam"]
    }
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| companyName | string | "PlaceholderCompany" | Company name for file headers |
| copyrightText | string | varies | Copyright text template |
| headerDecoration | string | null | Decoration line for headers |
| documentExposedElements | bool | true | Require docs on public members |
| documentInternalElements | bool | true | Require docs on internal members |
| documentPrivateElements | bool | false | Require docs on private members |
| documentInterfaces | bool | true | Require docs on interface members |
| documentPrivateFields | bool | false | Require docs on private fields |
| fileNamingConvention | string | "stylecop" | "stylecop" or "metadata" |
| xmlHeader | bool | true | Use XML-style file headers |
| variables | object | {} | Custom variables for templates |
| excludeFromPunctuationCheck | array | [] | Tags exempt from SA1629 |

### orderingRules

Controls using directive and element ordering.

```json
{
  "settings": {
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace",
      "systemUsingDirectivesFirst": true,
      "blankLinesBetweenUsingGroups": "require",
      "elementOrder": [
        "kind",
        "accessibility",
        "constant",
        "static",
        "readonly"
      ]
    }
  }
}
```

| Setting | Type | Values | Description |
|---------|------|--------|-------------|
| usingDirectivesPlacement | string | "insideNamespace", "outsideNamespace", "preserve" | Using directive location |
| systemUsingDirectivesFirst | bool | true/false | System usings first |
| blankLinesBetweenUsingGroups | string | "allow", "require", "omit" | Blank lines between groups |
| elementOrder | array | ordering specifiers | Member ordering within types |

### namingRules

Controls naming convention enforcement.

```json
{
  "settings": {
    "namingRules": {
      "allowCommonHungarianPrefixes": true,
      "allowedHungarianPrefixes": ["ui", "db"],
      "allowedNamespaceComponents": ["eBay", "iPhone"],
      "includeInferredTupleElementNames": false,
      "tupleElementNameCasing": "camelCase"
    }
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| allowCommonHungarianPrefixes | bool | true | Allow common prefixes like "is" |
| allowedHungarianPrefixes | array | [] | Additional allowed prefixes |
| allowedNamespaceComponents | array | [] | Namespace parts exempt from casing |
| includeInferredTupleElementNames | bool | false | Check inferred tuple names |
| tupleElementNameCasing | string | "camelCase" | Tuple element casing |

### layoutRules

Controls file and code layout.

```json
{
  "settings": {
    "layoutRules": {
      "newlineAtEndOfFile": "require",
      "allowConsecutiveUsings": true,
      "allowDoWhileOnClosingBrace": false
    }
  }
}
```

| Setting | Type | Values | Description |
|---------|------|--------|-------------|
| newlineAtEndOfFile | string | "allow", "require", "omit" | Trailing newline policy |
| allowConsecutiveUsings | bool | true/false | Allow `using` blocks without braces |
| allowDoWhileOnClosingBrace | bool | false/true | `while` on same line as `}` |

### readabilityRules

Controls readability enforcement.

```json
{
  "settings": {
    "readabilityRules": {
      "allowBuiltInTypeAliases": false
    }
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| allowBuiltInTypeAliases | bool | false | Allow `int` vs `Int32` mixing |

### indentation (deprecated)

Note: Indentation settings were deprecated. Use `.editorconfig` for indentation.

```json
{
  "settings": {
    "indentation": {
      "indentationSize": 4,
      "tabSize": 4,
      "useTabs": false
    }
  }
}
```

## Complete stylecop.json Examples

### Minimal Configuration

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace"
    },
    "layoutRules": {
      "newlineAtEndOfFile": "require"
    }
  }
}
```

### Open Source Project

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "companyName": "Project Contributors",
      "copyrightText": "Licensed under the MIT License. See LICENSE in the project root.",
      "documentExposedElements": true,
      "documentInternalElements": false,
      "xmlHeader": false
    },
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace",
      "systemUsingDirectivesFirst": true,
      "blankLinesBetweenUsingGroups": "require"
    },
    "layoutRules": {
      "newlineAtEndOfFile": "require"
    }
  }
}
```

### Enterprise Project

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "companyName": "Contoso Corporation",
      "copyrightText": "Copyright (c) {companyName}. All rights reserved.\nLicensed under the Contoso Enterprise License.",
      "headerDecoration": "-----------------------------------------------------------------------",
      "documentExposedElements": true,
      "documentInternalElements": true,
      "documentInterfaces": true,
      "xmlHeader": true
    },
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace",
      "systemUsingDirectivesFirst": true,
      "blankLinesBetweenUsingGroups": "require",
      "elementOrder": [
        "kind",
        "accessibility",
        "constant",
        "static",
        "readonly"
      ]
    },
    "namingRules": {
      "allowCommonHungarianPrefixes": false
    },
    "layoutRules": {
      "newlineAtEndOfFile": "require"
    }
  }
}
```

## Project Integration

### Add to Project File

Link `stylecop.json` as an additional file:

```xml
<ItemGroup>
  <AdditionalFiles Include="$(MSBuildThisFileDirectory)stylecop.json" Link="stylecop.json" />
</ItemGroup>
```

### Add to Directory.Build.props

For solution-wide configuration:

```xml
<Project>
  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)stylecop.json" Link="stylecop.json" />
  </ItemGroup>
</Project>
```

### Multiple Configuration Files

For project-specific overrides:

```xml
<!-- In a specific project -->
<ItemGroup>
  <AdditionalFiles Remove="$(MSBuildThisFileDirectory)stylecop.json" />
  <AdditionalFiles Include="stylecop.project.json" Link="stylecop.json" />
</ItemGroup>
```

## Configuration Precedence

1. Project-specific `stylecop.json`
2. Directory-level `stylecop.json` via Directory.Build.props
3. Root `stylecop.json`
4. Default StyleCop Analyzers settings

Rule severity always comes from `.editorconfig`, not `stylecop.json`.

## Validation

Validate configuration with schema:

```bash
# Check JSON syntax
python3 -c "import json; json.load(open('stylecop.json'))"
```

The schema URL provides IDE validation in VS Code, Visual Studio, and JetBrains Rider.

## Sources

- [StyleCop Analyzers Configuration](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/Configuration.md)
- [stylecop.json Schema](https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json)
