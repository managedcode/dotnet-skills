# WinForms Migration to Modern .NET

## Migration Overview

Migrating Windows Forms applications from .NET Framework to modern .NET (6, 7, 8, 9, 10) provides:
- Better performance and memory efficiency
- Access to modern C# language features
- Side-by-side deployment without system-wide runtime
- Continued support and security updates
- Access to new WinForms features

## Prerequisites Assessment

### Compatibility Analysis

Before migrating, analyze your application for compatibility:

```bash
# Install the .NET Upgrade Assistant
dotnet tool install -g upgrade-assistant

# Analyze project
upgrade-assistant analyze MyWinFormsApp.csproj

# Or run interactive upgrade
upgrade-assistant upgrade MyWinFormsApp.csproj
```

### Common Blockers

| Blocker | Impact | Mitigation |
|---------|--------|------------|
| WCF Client | Requires change | Use CoreWCF or gRPC |
| WCF Server | Not supported | Migrate to ASP.NET Core + gRPC |
| AppDomain | Limited support | Redesign with AssemblyLoadContext |
| Remoting | Not supported | Use gRPC or REST APIs |
| Code Access Security | Not supported | Remove or redesign |
| Windows Workflow Foundation | Not supported | Use Elsa or other workflow engine |
| Crystal Reports | May not work | Test or use alternative |

### Check for Deprecated APIs

```csharp
// These patterns indicate potential issues:

// App.config usage - may need migration
ConfigurationManager.AppSettings["MySetting"];

// System.Web references - not available
System.Web.HttpUtility.UrlEncode(value);

// Drawing.Common differences on non-Windows
System.Drawing.Image.FromFile(path);
```

## Project File Migration

### Before (.NET Framework)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{GUID-HERE}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <RootNamespace>MyWinFormsApp</RootNamespace>
    <AssemblyName>MyWinFormsApp</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Data" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <!-- Many more references -->
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Form1.cs">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include="Form1.Designer.cs">
      <DependentUpon>Form1.cs</DependentUpon>
    </Compile>
    <!-- Many more compile items -->
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

### After (Modern .NET SDK-Style)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
  </ItemGroup>
</Project>
```

## Step-by-Step Migration

### Step 1: Create New Project

```bash
# Create new WinForms project
dotnet new winforms -n MyWinFormsApp.Modern -f net9.0

# Or use specific template features
dotnet new winforms -n MyWinFormsApp.Modern --no-restore
```

### Step 2: Copy Source Files

Copy these files from the old project:
- All `.cs` files (forms, classes, controls)
- All `.resx` files (resources)
- All `.Designer.cs` files
- Assets (images, icons, etc.)

### Step 3: Update Program.cs

```csharp
// .NET Framework style
static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

// Modern .NET style
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

// Modern .NET with DI
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<MainForm>();
                services.AddTransient<ICustomerService, CustomerService>();
            })
            .Build();

        var mainForm = host.Services.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }
}
```

### Step 4: Update Configuration

Replace `app.config` with `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=...;"
  },
  "AppSettings": {
    "MaxRetries": 3,
    "TimeoutSeconds": 30
  }
}
```

```csharp
// Reading configuration
public class AppConfig
{
    private readonly IConfiguration _configuration;

    public AppConfig()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json", optional: true)
            .Build();
    }

    public string ConnectionString => _configuration.GetConnectionString("Default")!;
    public int MaxRetries => _configuration.GetValue<int>("AppSettings:MaxRetries");
}
```

### Step 5: Update NuGet References

Replace packages.config with PackageReference:

```xml
<!-- Old packages.config style -->
<packages>
  <package id="Newtonsoft.Json" version="13.0.1" targetFramework="net48" />
  <package id="Dapper" version="2.0.123" targetFramework="net48" />
</packages>

<!-- New PackageReference style in .csproj -->
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  <PackageReference Include="Dapper" Version="2.1.35" />
</ItemGroup>
```

### Step 6: Handle API Differences

```csharp
// BinaryFormatter - no longer recommended, use alternatives
// Old
var formatter = new BinaryFormatter();
formatter.Serialize(stream, obj);

// New - use System.Text.Json or other serializers
var json = JsonSerializer.Serialize(obj);
await File.WriteAllTextAsync(path, json);

// System.Drawing differences
// Old - worked everywhere
using var bitmap = new Bitmap(path);

// New - Windows-only by default, use SkiaSharp for cross-platform
// Or add package reference:
// <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
```

### Step 7: Update Assembly Info

Remove `AssemblyInfo.cs` and use project properties:

```xml
<PropertyGroup>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <Version>1.0.0</Version>
  <Company>My Company</Company>
  <Product>My WinForms App</Product>
  <Copyright>Copyright 2024</Copyright>
</PropertyGroup>
```

## Common Migration Issues

### Designer Issues

```csharp
// Issue: Designer fails to load after migration
// Solution: Ensure all dependencies are available and rebuild

// Issue: User controls not showing in toolbox
// Solution: Build solution, then refresh toolbox

// Issue: Resources not loading
// Solution: Ensure .resx files have correct build action
```

```xml
<!-- Ensure resources are embedded -->
<ItemGroup>
  <EmbeddedResource Update="Form1.resx">
    <DependentUpon>Form1.cs</DependentUpon>
  </EmbeddedResource>
</ItemGroup>
```

### Third-Party Controls

```csharp
// Check compatibility before migration
// Many vendors provide .NET 6+ compatible versions

// DevExpress, Telerik, Infragistics, etc. - check vendor documentation
// Older/abandoned controls - may need replacement

// If control source is available, consider migrating it too
// Or replace with:
// - Built-in .NET controls
// - Open-source alternatives (be mindful of licensing)
// - Custom implementations
```

### Database Access

```csharp
// Entity Framework 6 to EF Core
// Old (EF6)
using (var context = new MyDbContext())
{
    var customers = context.Customers.Where(c => c.IsActive).ToList();
}

// New (EF Core)
await using var context = new MyDbContext();
var customers = await context.Customers
    .Where(c => c.IsActive)
    .ToListAsync();
```

### WCF Client Migration

```csharp
// Option 1: Use System.ServiceModel packages
// <PackageReference Include="System.ServiceModel.Http" Version="6.0.0" />

// Option 2: Generate new client
// dotnet-svcutil https://service.example.com/MyService?wsdl

// Option 3: Replace with HTTP client for REST services
public class MyServiceClient
{
    private readonly HttpClient _client;

    public async Task<Customer> GetCustomerAsync(int id)
    {
        var response = await _client.GetAsync($"api/customers/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Customer>();
    }
}
```

## High-DPI and Modern Features

### Enable High-DPI Support

```csharp
// In Program.cs (already included in ApplicationConfiguration.Initialize())
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
```

```xml
<!-- app.manifest -->
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
  </windowsSettings>
</application>
```

### Use New .NET 8/9 Features

```csharp
// Button commands (.NET 8+)
btnSave.Command = new RelayCommand(Save, CanSave);

// System icons (.NET 8+)
var icon = SystemIcons.GetStockIcon(StockIconId.Info);

// Improved data binding (.NET 9+)
// Better performance and memory usage

// FolderBrowserDialog improvements
using var dialog = new FolderBrowserDialog
{
    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    ShowNewFolderButton = true,
    UseDescriptionForTitle = true,
    Description = "Select output folder"
};
```

## Testing After Migration

### Functional Testing Checklist

- [ ] Application launches without errors
- [ ] All forms open correctly
- [ ] Designer loads all forms
- [ ] Data binding works correctly
- [ ] Validation behaves as expected
- [ ] Database operations work
- [ ] File operations work
- [ ] Printing works (if applicable)
- [ ] Third-party controls function
- [ ] Resources (images, icons) load
- [ ] Localization works (if applicable)
- [ ] High-DPI displays correctly
- [ ] Keyboard shortcuts work
- [ ] Tab order is correct

### Performance Testing

```csharp
// Basic startup timing
var sw = Stopwatch.StartNew();
Application.Run(new MainForm());
Console.WriteLine($"Startup: {sw.ElapsedMilliseconds}ms");

// Memory usage comparison
// Use dotnet-counters or Visual Studio diagnostics
// dotnet-counters monitor --process-id <PID>
```

## Deployment

### Framework-Dependent Deployment

```bash
# Requires .NET runtime on target machine
dotnet publish -c Release -r win-x64 --self-contained false
```

### Self-Contained Deployment

```bash
# Includes runtime, larger but no dependencies
dotnet publish -c Release -r win-x64 --self-contained true

# Single file (recommended for distribution)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Trimmed (smaller size, test thoroughly)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true
```

```xml
<!-- Project settings for publishing -->
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

## Gradual Migration Strategy

For large applications, consider incremental migration:

### 1. Shared Library Approach

```text
Solution/
├── MyApp.Core/                 # .NET Standard 2.0 - shared
│   ├── Models/
│   ├── Services/
│   └── Interfaces/
├── MyApp.WinForms.Legacy/      # .NET Framework 4.8 - old UI
│   └── References MyApp.Core
├── MyApp.WinForms.Modern/      # .NET 9 - new UI
│   └── References MyApp.Core
```

### 2. Feature-by-Feature Migration

1. Migrate shared business logic to .NET Standard
2. Create new modern .NET WinForms project
3. Migrate forms one at a time
4. Test each migrated form thoroughly
5. Retire old project when complete

### 3. Side-by-Side Development

```csharp
// Multi-targeting for shared code
<PropertyGroup>
  <TargetFrameworks>net48;net9.0-windows</TargetFrameworks>
</PropertyGroup>

// Conditional compilation when needed
#if NET48
    // .NET Framework specific code
#else
    // Modern .NET code
#endif
```

## Resources

- [Official Migration Guide](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/migration/)
- [.NET Upgrade Assistant](https://learn.microsoft.com/en-us/dotnet/core/porting/upgrade-assistant-overview)
- [Breaking Changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/winforms)
- [What's New in Windows Forms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new/)
