# Analyzer Configuration for Complexity Rules

## Configuration Layers

Complexity analyzers can be configured at multiple levels:

1. **EditorConfig** - rule severity
2. **CodeMetricsConfig.txt** - numeric thresholds
3. **Project/Directory.Build.props** - analyzer package inclusion

## EditorConfig Setup

### Minimal Configuration

Add to root `.editorconfig`:

```ini
root = true

[*.cs]
dotnet_diagnostic.CA1502.severity = warning
dotnet_diagnostic.CA1505.severity = warning
dotnet_diagnostic.CA1506.severity = warning
dotnet_diagnostic.CA1501.severity = suggestion
```

### Severity Levels

| Severity   | Build Behavior                     | Use Case                           |
|------------|------------------------------------|------------------------------------|
| error      | Fails the build                    | Hard enforcement in CI             |
| warning    | Shows warning, build succeeds      | Recommended default                |
| suggestion | Shows message in IDE only          | Informational tracking             |
| silent     | Runs but produces no diagnostics   | Metric collection without noise    |
| none       | Completely disabled                | Intentionally ignored              |

### Per-File Overrides

To relax rules for generated or legacy code:

```ini
[*.generated.cs]
dotnet_diagnostic.CA1502.severity = none
dotnet_diagnostic.CA1505.severity = none
dotnet_diagnostic.CA1506.severity = none

[**/Migrations/*.cs]
dotnet_diagnostic.CA1502.severity = silent
```

## CodeMetricsConfig.txt

### File Format

The `CodeMetricsConfig.txt` file specifies numeric thresholds:

```text
# Cyclomatic complexity per method
CA1502: 20

# Cyclomatic complexity average per type
CA1502(Type): 6

# Maintainability index minimum
CA1505: 10

# Class coupling per method
CA1506: 30

# Class coupling per type
CA1506(Type): 80

# Inheritance depth maximum
CA1501: 6
```

### Scope Suffixes

| Suffix       | Meaning                                |
|--------------|----------------------------------------|
| (none)       | Per-method threshold                   |
| `(Type)`     | Per-type or type-average threshold     |
| `(Assembly)` | Assembly-wide threshold (where applicable) |

### Threshold Recommendations

**Conservative (new projects):**

```text
CA1502: 15
CA1502(Type): 5
CA1505: 20
CA1506: 20
CA1506(Type): 50
CA1501: 4
```

**Moderate (established projects):**

```text
CA1502: 20
CA1502(Type): 6
CA1505: 10
CA1506: 30
CA1506(Type): 80
CA1501: 6
```

**Relaxed (legacy migration):**

```text
CA1502: 30
CA1502(Type): 10
CA1505: 5
CA1506: 50
CA1506(Type): 120
CA1501: 8
```

## Project Integration

### Including CodeMetricsConfig.txt

Add to `Directory.Build.props` for solution-wide application:

```xml
<Project>
  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)CodeMetricsConfig.txt"
                     Condition="Exists('$(MSBuildThisFileDirectory)CodeMetricsConfig.txt')" />
  </ItemGroup>
</Project>
```

Or add to individual `.csproj` files:

```xml
<ItemGroup>
  <AdditionalFiles Include="../../CodeMetricsConfig.txt" />
</ItemGroup>
```

### Analyzer Package Reference

The maintainability rules require the .NET analyzers. For SDK-style projects targeting .NET 5+, these are included by default.

For older projects or explicit inclusion:

```xml
<PropertyGroup>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>
  <AnalysisLevel>latest</AnalysisLevel>
  <AnalysisMode>Recommended</AnalysisMode>
</PropertyGroup>
```

Or reference the package directly:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.*">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

## Suppression Strategies

### Attribute-Based Suppression

For intentional violations:

```csharp
[SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity",
    Justification = "State machine requires explicit transitions")]
public void ProcessState() { ... }
```

### Global Suppressions

Add to `GlobalSuppressions.cs`:

```csharp
[assembly: SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
    Scope = "type", Target = "~T:MyNamespace.CompositionRoot",
    Justification = "Composition root is expected to have high coupling")]
```

### Pragma Suppression

For localized suppression:

```csharp
#pragma warning disable CA1502
public void LegacyMethod()
{
    // Complex legacy code pending refactoring
}
#pragma warning restore CA1502
```

## CI Integration

### Build Command

```bash
dotnet build -warnaserror
```

### Selective Error Promotion

To fail only on complexity rules:

```xml
<PropertyGroup>
  <WarningsAsErrors>CA1502;CA1505;CA1506</WarningsAsErrors>
</PropertyGroup>
```

### Baseline Workflow

For existing codebases:

1. Set all rules to `suggestion`
2. Run `dotnet build` and capture baseline counts
3. Add `CodeMetricsConfig.txt` with current thresholds
4. Gradually tighten thresholds as code improves
5. Promote to `warning` when baseline is acceptable
6. Promote to `error` when enforcement is stable

## Sources

- [Configure code analysis rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options)
- [EditorConfig settings](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options)
- [Suppress code analysis warnings](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/suppress-warnings)
