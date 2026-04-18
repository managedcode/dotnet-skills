# Project Detection Patterns

This reference defines detection patterns for identifying .NET project characteristics including SDK types, target frameworks, workloads, and app models.

## SDK Detection

### Project SDK Types

Detect the SDK from the `<Project Sdk="...">` attribute in `.csproj`/`.fsproj`/`.vbproj` files:

| SDK | Indicates |
|-----|-----------|
| `Microsoft.NET.Sdk` | Class library or console app |
| `Microsoft.NET.Sdk.Web` | ASP.NET Core web app |
| `Microsoft.NET.Sdk.BlazorWebAssembly` | Blazor WebAssembly app |
| `Microsoft.NET.Sdk.Razor` | Razor class library |
| `Microsoft.NET.Sdk.Worker` | Worker service |
| `Microsoft.NET.Sdk.WindowsDesktop` | WPF or WinForms (legacy SDK style) |
| `Aspire.AppHost.Sdk` | Aspire app host |
| `Aspire.ServiceDefaults.Sdk` | Aspire service defaults |

### SDK Version Detection

Check `global.json` for SDK version constraints:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor"
  }
}
```

Key SDK version ranges:
- `10.0.x` - .NET 10 (LTS, supported until November 2028)
- `9.0.x` - .NET 9 (STS, supported until November 2026)
- `8.0.x` - .NET 8 (LTS, supported until November 2026)
- `7.0.x` - .NET 7 (end of support)
- `6.0.x` - .NET 6 (LTS)
- `5.0.x` - .NET 5 (end of support)
- `3.1.x` - .NET Core 3.1 (end of support)

## Target Framework Detection

### Target Framework Monikers (TFM)

Detect from `<TargetFramework>` or `<TargetFrameworks>` in project files:

| TFM Pattern | Platform |
|-------------|----------|
| `net10.0`, `net9.0`, `net8.0`, `net7.0`, `net6.0` | Modern .NET |
| `net10.0-windows`, `net9.0-windows`, `net8.0-windows` | Windows-specific workload |
| `net10.0-ios`, `net10.0-android`, `net10.0-maccatalyst`, `net10.0-browser` | MAUI/mobile or browser workloads |
| `netstandard2.0`, `netstandard2.1` | .NET Standard library |
| `netcoreapp3.1`, `netcoreapp2.1` | Legacy .NET Core |
| `net48`, `net472`, `net461` | .NET Framework |

### Multi-Targeting

Projects with `<TargetFrameworks>` (plural) target multiple platforms:

```xml
<TargetFrameworks>net10.0;net10.0-windows;net10.0-ios;net10.0-android</TargetFrameworks>
```

## Workload Detection

### Installed Workloads

Check installed workloads via:
```bash
dotnet workload list
```

### Project-Implied Workloads

| Indicator | Workload |
|-----------|----------|
| TFM contains `-ios`, `-android`, `-maccatalyst` | `maui` or individual platform workloads |
| TFM contains `-browser` | `wasm-tools` |
| Package `Aspire.Hosting` | `aspire` |
| Package `Microsoft.WindowsAppSDK` | `windowsdesktop` |

### Workload Manifest Files

Check for workload requirements in:
- `.config/dotnet-tools.json` - local tools
- `Directory.Build.props` - workload version pins
- `NuGet.config` - custom feeds for preview workloads

## Language Version Detection

### LangVersion Property

Detect from `<LangVersion>` in project files or `Directory.Build.props`:

| Value | Language Version |
|-------|------------------|
| `latest` | Highest version for TFM |
| `preview` | Preview features enabled |
| `14.0`, `13.0`, `12.0`, `11.0` | Explicit version |
| `default` | SDK default |

### Implicit Language Version

When `<LangVersion>` is not specified, the SDK sets it based on TFM:
- `net10.0` implies C# 14
- `net9.0` implies C# 13
- `net8.0` implies C# 12
- `net7.0` implies C# 11
- `net6.0` implies C# 10

## App Model Detection

### Web Apps

Look for these indicators:

**ASP.NET Core general:**
- `Program.cs` with `WebApplication.CreateBuilder`
- `Startup.cs` with `ConfigureServices`/`Configure`
- `appsettings.json`, `appsettings.Development.json`
- `wwwroot/` folder

**Blazor:**
- `.razor` files
- `@page` directive
- `RenderModeInteractiveServer`, `RenderModeInteractiveWebAssembly`
- `AddInteractiveServerComponents()`, `AddInteractiveWebAssemblyComponents()`
- `_Imports.razor`

**Minimal APIs:**
- `app.MapGet()`, `app.MapPost()`, etc.
- No `[ApiController]` classes
- Route handlers as lambdas or delegates

**MVC/Web API:**
- `Controllers/` folder
- Classes inheriting `Controller` or `ControllerBase`
- `[ApiController]`, `[Route]`, `[HttpGet]` attributes

**SignalR:**
- Classes inheriting `Hub` or `Hub<T>`
- `IHubContext<T>` injection
- `MapHub<T>()` calls

**gRPC:**
- `.proto` files
- `Grpc.AspNetCore` package
- `MapGrpcService<T>()` calls

### Desktop Apps

**MAUI:**
- `MauiProgram.cs`
- `MauiApp.CreateBuilder()`
- `.maui` file extensions
- `Microsoft.Maui.*` namespaces

**WPF:**
- `<UseWPF>true</UseWPF>` in project
- `.xaml` files with WPF namespaces
- `PresentationFramework` reference
- `App.xaml`, `MainWindow.xaml`

**WinForms:**
- `<UseWindowsForms>true</UseWindowsForms>` in project
- `System.Windows.Forms` namespace
- `Form` class inheritance
- `.Designer.cs` files

**WinUI 3:**
- `Microsoft.WindowsAppSDK` package
- `Microsoft.WinUI` namespace
- `WinUIEx` patterns

**Uno Platform:**
- `Uno.WinUI` or `Uno.UI` packages
- Cross-platform XAML with Uno namespaces
- Platform head projects

### Worker and Background Services

- `<OutputType>Exe</OutputType>` with no web SDK
- `BackgroundService` inheritance
- `IHostedService` implementation
- `Host.CreateDefaultBuilder()` without web

### Azure Functions

- `[Function]` attribute
- `Microsoft.Azure.Functions.Worker` namespace
- `host.json` file
- `local.settings.json` file

### Aspire

**App Host:**
- `Aspire.AppHost.Sdk`
- `DistributedApplicationBuilder`
- `builder.AddProject<T>()`

**Service Defaults:**
- `Aspire.ServiceDefaults.Sdk`
- `AddServiceDefaults()` extension

**Component Usage:**
- `Aspire.*` packages
- `AddRedis()`, `AddPostgres()`, etc.

## Test Framework Detection

| Indicator | Framework |
|-----------|-----------|
| `Microsoft.NET.Test.Sdk` + `TUnit` | TUnit |
| `Microsoft.NET.Test.Sdk` + `xunit` | xUnit |
| `Microsoft.NET.Test.Sdk` + `MSTest.TestFramework` | MSTest |
| `[Fact]`, `[Theory]` attributes | xUnit |
| `[Test]`, `[TestCase]` attributes | TUnit |
| `[TestMethod]`, `[TestClass]` attributes | MSTest |

## Build and Quality Tooling Detection

### Analyzers

| Package | Tool |
|---------|------|
| `Microsoft.CodeAnalysis.NetAnalyzers` | Built-in analyzers |
| `Roslynator.Analyzers` | Roslynator |
| `StyleCop.Analyzers` | StyleCop |
| `Meziantou.Analyzer` | Meziantou rules |
| `SonarAnalyzer.CSharp` | SonarQube |

### Formatters

| Indicator | Tool |
|-----------|------|
| `.editorconfig` present | dotnet format |
| `CSharpier` package or `.csharpierrc` | CSharpier |
| `jb` CLI usage | ReSharper CLI |

### Coverage

| Package | Tool |
|---------|------|
| `coverlet.collector` | Coverlet |
| `coverlet.msbuild` | Coverlet (MSBuild) |
| `ReportGenerator` tool | Report generation |

### Architecture Testing

| Package | Tool |
|---------|------|
| `NetArchTest.Rules` | NetArchTest |
| `ArchUnitNET` | ArchUnitNET |

## Common File Locations

| File | Purpose |
|------|---------|
| `*.sln` | Solution file (find all projects) |
| `*.csproj`, `*.fsproj`, `*.vbproj` | Project files |
| `global.json` | SDK version pinning |
| `Directory.Build.props` | Shared MSBuild properties |
| `Directory.Build.targets` | Shared MSBuild targets |
| `Directory.Packages.props` | Central package management |
| `NuGet.config` | NuGet configuration |
| `.editorconfig` | Code style and analyzer settings |
| `*.ruleset` | Legacy analyzer rules |
