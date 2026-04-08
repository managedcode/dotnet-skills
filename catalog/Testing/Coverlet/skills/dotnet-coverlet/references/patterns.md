# Coverlet Coverage Patterns Reference

## Filter Expression Syntax

Coverlet uses filter expressions to include or exclude assemblies and types from coverage.

### Basic Syntax

```
[<ASSEMBLY_FILTER>]<TYPE_FILTER>
```

- `*` matches any sequence of characters
- `?` matches any single character
- Assembly filter is enclosed in `[]`
- Type filter follows the assembly filter

### Examples

| Pattern | Description |
|---------|-------------|
| `[*]*` | All types in all assemblies |
| `[MyApp]*` | All types in MyApp assembly |
| `[MyApp]MyApp.Services.*` | All types in Services namespace |
| `[*]MyApp.Models.User` | Specific type in any assembly |
| `[MyApp.?]*` | Assemblies matching MyApp.* |
| `[MyApp.*]*.Tests.*` | Test types in MyApp.* assemblies |

---

## Common Exclusion Patterns

### Generated Code

```bash
# MSBuild
/p:Exclude="[*]*.Generated.*,[*]*.Designer.*,[*]*.g.cs"

# Runsettings
<Exclude>[*]*.Generated.*,[*]*.Designer.*</Exclude>

# Console
--exclude "[*]*.Generated.*" --exclude "[*]*.Designer.*"
```

### Entity Framework Migrations

```bash
# By file path
/p:ExcludeByFile="**/Migrations/*.cs"

# By namespace
/p:Exclude="[*]*.Migrations.*"
```

### Auto-Generated Properties

```bash
# Skip auto-properties (getters/setters)
/p:SkipAutoProps=true
```

### Test Assemblies

```bash
# Exclude test projects from coverage
/p:Exclude="[*.Tests]*,[*.UnitTests]*,[*.IntegrationTests]*"

# Do not include test assembly itself
/p:IncludeTestAssembly=false
```

### Third-Party Code

```bash
# Exclude specific vendor namespaces
/p:Exclude="[*]AutoMapper.*,[*]FluentValidation.*"
```

---

## Attribute-Based Exclusions

### Built-in .NET Attributes

```bash
/p:ExcludeByAttribute="Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute"
```

### ExcludeFromCodeCoverage Attribute

Apply `[ExcludeFromCodeCoverage]` to types or members:

```csharp
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public class GeneratedDto
{
    public string Name { get; set; }
}

public class Service
{
    [ExcludeFromCodeCoverage]
    public void DebugOnlyMethod()
    {
        // Not covered
    }
}
```

Then exclude:

```bash
/p:ExcludeByAttribute="ExcludeFromCodeCoverage"
```

### Custom Exclusion Attributes

Define a custom attribute:

```csharp
[AttributeUsage(AttributeTargets.All)]
public sealed class ExcludeFromCoverageAttribute : Attribute { }
```

Apply and exclude:

```bash
/p:ExcludeByAttribute="ExcludeFromCoverageAttribute"
```

---

## File-Based Exclusions

### ExcludeByFile Patterns

```bash
# Single pattern
/p:ExcludeByFile="**/Migrations/*.cs"

# Multiple patterns
/p:ExcludeByFile="**/Migrations/*.cs,**/Generated/*.cs,**/obj/**/*.cs"
```

### Common File Exclusions

| Pattern | Description |
|---------|-------------|
| `**/Migrations/*.cs` | EF Core migrations |
| `**/Generated/*.cs` | Generated code folders |
| `**/obj/**/*.cs` | Build output |
| `**/*.Designer.cs` | Designer files |
| `**/*.g.cs` | Source generators |
| `**/GlobalSuppressions.cs` | Analyzer suppressions |
| `**/AssemblyInfo.cs` | Assembly metadata |

---

## Include Patterns

### Limiting Coverage Scope

```bash
# Only specific assemblies
/p:Include="[MyApp.Core]*,[MyApp.Services]*"

# Only specific namespaces
/p:Include="[MyApp]*MyApp.Domain.*"
```

### Source Directory Inclusion

For projects with source code in non-standard locations:

```bash
/p:IncludeDirectory="../src/*,../lib/*"
```

---

## Combined Pattern Examples

### Typical Application Coverage

```bash
dotnet test /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:Exclude="[*.Tests]*,[*.IntegrationTests]*,[*]*.Migrations.*,[*]*.Generated.*" \
  /p:ExcludeByAttribute="Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludeFromCodeCoverage" \
  /p:ExcludeByFile="**/Migrations/*.cs,**/*.Designer.cs" \
  /p:SkipAutoProps=true
```

### Runsettings Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>

          <!-- Assembly and type exclusions -->
          <Exclude>
            [*.Tests]*,
            [*.IntegrationTests]*,
            [*]*.Migrations.*,
            [*]*.Generated.*
          </Exclude>

          <!-- Attribute exclusions -->
          <ExcludeByAttribute>
            Obsolete,
            GeneratedCodeAttribute,
            CompilerGeneratedAttribute,
            ExcludeFromCodeCoverage
          </ExcludeByAttribute>

          <!-- File path exclusions -->
          <ExcludeByFile>
            **/Migrations/*.cs,
            **/*.Designer.cs,
            **/*.g.cs,
            **/GlobalSuppressions.cs
          </ExcludeByFile>

          <!-- Additional options -->
          <SkipAutoProps>true</SkipAutoProps>
          <DeterministicReport>true</DeterministicReport>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Directory.Build.props Example

```xml
<Project>
  <PropertyGroup Condition="'$(CollectCoverage)' == 'true'">
    <CoverletOutputFormat>cobertura</CoverletOutputFormat>
    <CoverletOutput>$(MSBuildThisFileDirectory)coverage/</CoverletOutput>

    <!-- Exclusions -->
    <Exclude>[*.Tests]*,[*.IntegrationTests]*,[*]*.Migrations.*</Exclude>
    <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludeFromCodeCoverage</ExcludeByAttribute>
    <ExcludeByFile>**/Migrations/*.cs,**/*.Designer.cs</ExcludeByFile>

    <!-- Options -->
    <SkipAutoProps>true</SkipAutoProps>
    <DeterministicReport>true</DeterministicReport>
    <IncludeTestAssembly>false</IncludeTestAssembly>
  </PropertyGroup>
</Project>
```

---

## Framework-Specific Patterns

### ASP.NET Core

```bash
/p:Exclude="[*]*.Migrations.*,[*]Program,[*]Startup" \
/p:ExcludeByFile="**/Migrations/*.cs,**/wwwroot/**"
```

### Blazor

```bash
/p:Exclude="[*]*.Migrations.*,[*]App,[*]*.razor.g.cs" \
/p:ExcludeByFile="**/*.razor.g.cs,**/*.razor.css.g.cs"
```

### Worker Services

```bash
/p:Exclude="[*]Program,[*]*.Worker" \
/p:ExcludeByAttribute="ExcludeFromCodeCoverage"
```

---

## Troubleshooting Patterns

### Verify Exclusions Work

Run with verbose output to see what is being excluded:

```bash
# Console tool
coverlet ... --verbosity detailed

# Check generated coverage file for excluded types
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Pattern not matching | Check assembly name vs namespace |
| Wildcards not working | Ensure proper escaping in shell |
| Files still covered | Use absolute paths in ExcludeByFile |
| Attributes ignored | Check attribute full name |

### Debug Filter Expressions

Test patterns incrementally:

```bash
# Start broad
/p:Exclude="[*]*"  # Excludes everything

# Narrow down
/p:Exclude="[MyApp.Tests]*"  # Exclude specific assembly

# Verify with coverage report
```
