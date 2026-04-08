# Project Structure Patterns

## Solution Layout

### Recommended Directory Structure

```text
<repo-root>/
├── .config/
│   └── dotnet-tools.json
├── src/
│   ├── <ProductName>.Core/
│   ├── <ProductName>.Api/
│   └── <ProductName>.Web/
├── tests/
│   ├── <ProductName>.Core.Tests/
│   └── <ProductName>.Api.Tests/
├── samples/
│   └── <ProductName>.Sample/
├── docs/
├── Directory.Build.props
├── Directory.Build.targets
├── Directory.Packages.props
├── global.json
├── nuget.config
├── <SolutionName>.sln
└── README.md
```

### Project Naming Conventions

| Project Type | Pattern | Example |
|--------------|---------|---------|
| Core library | `<ProductName>.Core` | `Contoso.Orders.Core` |
| Domain layer | `<ProductName>.Domain` | `Contoso.Orders.Domain` |
| Application layer | `<ProductName>.Application` | `Contoso.Orders.Application` |
| Infrastructure | `<ProductName>.Infrastructure` | `Contoso.Orders.Infrastructure` |
| Web API | `<ProductName>.Api` | `Contoso.Orders.Api` |
| Web frontend | `<ProductName>.Web` | `Contoso.Orders.Web` |
| Worker service | `<ProductName>.Worker` | `Contoso.Orders.Worker` |
| Unit tests | `<ProjectName>.Tests` | `Contoso.Orders.Core.Tests` |
| Integration tests | `<ProjectName>.IntegrationTests` | `Contoso.Orders.Api.IntegrationTests` |

---

## Directory.Build.props

`Directory.Build.props` is automatically imported by MSBuild for all projects in its directory and subdirectories.

### Basic Template

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>

  <!-- Package metadata for libraries -->
  <PropertyGroup>
    <Authors>Your Name or Organization</Authors>
    <Company>Your Company</Company>
    <Copyright>Copyright (c) $(Company) $([System.DateTime]::Now.Year)</Copyright>
    <RepositoryUrl>https://github.com/your-org/your-repo</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <!-- Deterministic builds for CI -->
  <PropertyGroup Condition="'$(CI)' == 'true'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

### Conditional Properties by Project Type

```xml
<Project>
  <!-- Shared defaults -->
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <!-- Test project defaults -->
  <PropertyGroup Condition="$(MSBuildProjectName.EndsWith('.Tests'))">
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <!-- Library defaults -->
  <PropertyGroup Condition="!$(MSBuildProjectName.EndsWith('.Tests')) AND !$(MSBuildProjectName.EndsWith('.Api')) AND !$(MSBuildProjectName.EndsWith('.Web'))">
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

### Nested Directory.Build.props

Child directories can extend the parent by importing it explicitly:

```xml
<!-- tests/Directory.Build.props -->
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
</Project>
```

---

## Directory.Build.targets

Use `Directory.Build.targets` for logic that runs after the project file is fully evaluated.

```xml
<Project>
  <!-- Run after project evaluation -->
  <Target Name="PrintBuildInfo" BeforeTargets="Build">
    <Message Importance="High" Text="Building $(MSBuildProjectName) for $(TargetFramework)" />
  </Target>

  <!-- Enforce test naming convention -->
  <Target Name="ValidateTestProjectNaming" BeforeTargets="Build" Condition="'$(IsTestProject)' == 'true'">
    <Error Condition="!$(MSBuildProjectName.EndsWith('.Tests')) AND !$(MSBuildProjectName.EndsWith('.IntegrationTests'))"
           Text="Test projects must end with .Tests or .IntegrationTests" />
  </Target>
</Project>
```

---

## Central Package Management (CPM)

Central Package Management consolidates package versions into a single `Directory.Packages.props` file.

### Enabling CPM

Add to `Directory.Packages.props` at the repository root:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <!-- Runtime packages -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options" Version="9.0.0" />
    <PackageVersion Include="System.Text.Json" Version="9.0.0" />

    <!-- Entity Framework Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />

    <!-- ASP.NET Core extras -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="6.6.2" />

    <!-- Testing -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="FluentAssertions" Version="6.12.1" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />

    <!-- Analyzers -->
    <PackageVersion Include="StyleCop.Analyzers" Version="1.2.0-beta.556" />
    <PackageVersion Include="Roslynator.Analyzers" Version="4.12.6" />
  </ItemGroup>
</Project>
```

### Project File References with CPM

When CPM is enabled, project files reference packages without versions:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" />
  <PackageReference Include="Microsoft.Extensions.Logging" />
</ItemGroup>
```

### Version Overrides

Use `VersionOverride` sparingly when a project requires a different version:

```xml
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json" VersionOverride="13.0.3" />
</ItemGroup>
```

---

## global.json

Pin the SDK version for reproducible builds:

```json
{
  "sdk": {
    "version": "9.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

### Roll-Forward Policies

| Value | Behavior |
|-------|----------|
| `patch` | Use specified or highest installed patch |
| `feature` | Use specified or highest installed feature band |
| `minor` | Use specified or highest installed minor |
| `major` | Use highest installed SDK |
| `latestPatch` | Use highest installed patch |
| `latestFeature` | Use highest installed feature band |
| `latestMinor` | Use highest installed minor |
| `latestMajor` | Use highest installed SDK |
| `disable` | Exact match only |

---

## nuget.config

Configure package sources and credentials:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <!-- Private feeds -->
    <add key="github" value="https://nuget.pkg.github.com/your-org/index.json" />
  </packageSources>

  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="github">
      <package pattern="YourOrg.*" />
    </packageSource>
  </packageSourceMapping>

  <!-- CI credentials via environment variables -->
  <packageSourceCredentials>
    <github>
      <add key="Username" value="%NUGET_USERNAME%" />
      <add key="ClearTextPassword" value="%NUGET_TOKEN%" />
    </github>
  </packageSourceCredentials>
</configuration>
```

---

## Analyzers and Code Style

### .editorconfig Basics

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,vb}]
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
csharp_prefer_braces = true:warning
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_constructors = false:suggestion

# IDE diagnostics
dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.IDE0055.severity = warning

[*.{json,yml,yaml}]
indent_size = 2
```

### Analyzer Packages in Directory.Build.props

```xml
<ItemGroup>
  <PackageReference Include="StyleCop.Analyzers" PrivateAssets="all" />
  <PackageReference Include="Roslynator.Analyzers" PrivateAssets="all" />
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" PrivateAssets="all" />
</ItemGroup>
```

---

## Multi-Targeting

### Single Project Multi-Targeting

```xml
<PropertyGroup>
  <TargetFrameworks>net9.0;net8.0;netstandard2.0</TargetFrameworks>
</PropertyGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'netstandard2.0'">
  <PackageReference Include="System.Text.Json" />
</ItemGroup>
```

### Conditional Compilation

```csharp
#if NET9_0_OR_GREATER
    // .NET 9+ specific code
    ArgumentNullException.ThrowIfNull(value);
#else
    // Fallback for older frameworks
    if (value is null)
        throw new ArgumentNullException(nameof(value));
#endif
```

---

## Source Link and Deterministic Builds

Enable source link for debugger integration:

```xml
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
</ItemGroup>
```

---

## Solution Filters

Create `.slnf` files for partial solution loading:

```json
{
  "solution": {
    "path": "MyProduct.sln",
    "projects": [
      "src\\MyProduct.Core\\MyProduct.Core.csproj",
      "src\\MyProduct.Api\\MyProduct.Api.csproj",
      "tests\\MyProduct.Core.Tests\\MyProduct.Core.Tests.csproj"
    ]
  }
}
```

Open with: `dotnet sln open MyProduct.Api.slnf`
