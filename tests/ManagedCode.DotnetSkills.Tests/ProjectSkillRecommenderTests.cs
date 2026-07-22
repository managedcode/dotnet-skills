using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class ProjectSkillRecommenderTests
{
    [Fact]
    public void Analyze_DetectsFrameworkAndPackageSignals()
    {
        using var tempDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(tempDirectory.Path, "src", "App"));

        var projectPath = System.IO.Path.Combine(tempDirectory.Path, "src", "App", "App.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
                <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
                <PackageReference Include="coverlet.collector" Version="6.0.4" />
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
            </Project>
            """);

        var recommendations = new ProjectSkillRecommender(TestCatalog.Load())
            .Analyze(tempDirectory.Path)
            .Recommendations
            .Select(item => item.Skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("aspnet-core", recommendations);
        Assert.Contains("entity-framework-core", recommendations);
        Assert.Contains("mvvm", recommendations);
        Assert.Contains("coverlet", recommendations);
        Assert.Contains("xunit", recommendations);
        Assert.Contains("microsoft-extensions", recommendations);
    }

    [Fact]
    public void Analyze_FlagsAutoInstallCandidates_OnlyForStrongProjectSignals()
    {
        using var tempDirectory = new TemporaryDirectory();
        var projectPath = System.IO.Path.Combine(tempDirectory.Path, "App.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
            </Project>
            """);

        var recommendations = new ProjectSkillRecommender(TestCatalog.Load())
            .Analyze(tempDirectory.Path)
            .Recommendations
            .ToDictionary(item => item.Skill.Name, StringComparer.OrdinalIgnoreCase);

        Assert.False(recommendations["dotnet"].IsAutoInstallCandidate);
        Assert.False(recommendations["modern-csharp"].IsAutoInstallCandidate);
        Assert.True(recommendations["aspnet-core"].IsAutoInstallCandidate);
        Assert.True(recommendations["xunit"].IsAutoInstallCandidate);
    }

    [Fact]
    public void Analyze_SkipsMalformedProjectFiles()
    {
        using var tempDirectory = new TemporaryDirectory();

        File.WriteAllText(
            System.IO.Path.Combine(tempDirectory.Path, "Broken.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0
            """);

        File.WriteAllText(
            System.IO.Path.Combine(tempDirectory.Path, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
            </Project>
            """);

        var recommendations = new ProjectSkillRecommender(TestCatalog.Load())
            .Analyze(tempDirectory.Path)
            .Recommendations
            .Select(item => item.Skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("aspnet-core", recommendations);
        Assert.Contains("xunit", recommendations);
    }

    [Fact]
    public void Analyze_DetectsAstroAndVisualTestingWithoutDotNetProject()
    {
        using var tempDirectory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "src", "pages"));
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "package.json"),
            """
            {
              "name": "astro-site",
              "dependencies": {
                "astro": "^7.1.3"
              }
            }
            """);
        File.WriteAllText(Path.Combine(tempDirectory.Path, "src", "pages", "index.astro"), "<h1>Hello</h1>");

        var scan = new ProjectSkillRecommender(TestCatalog.Load()).Analyze(tempDirectory.Path);
        var recommendations = scan.Recommendations.ToDictionary(item => item.Skill.Name, StringComparer.OrdinalIgnoreCase);

        Assert.Empty(scan.ProjectFiles);
        Assert.Equal(1, scan.FrontendManifestCount);
        Assert.Contains("Astro", scan.FrontendFrameworks);
        Assert.Contains("astro-developer", recommendations);
        Assert.Contains("playwright-visual-testing", recommendations);
        Assert.True(recommendations["astro-developer"].IsAutoInstallCandidate);
        Assert.True(recommendations["playwright-visual-testing"].IsAutoInstallCandidate);
        Assert.DoesNotContain("dotnet", recommendations);
    }

    [Fact]
    public void Analyze_DetectsReactAsBrowserUiWithoutRecommendingAstro()
    {
        using var tempDirectory = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "package.json"),
            """
            {
              "name": "react-site",
              "dependencies": {
                "react": "^19.0.0",
                "react-dom": "^19.0.0"
              }
            }
            """);

        var scan = new ProjectSkillRecommender(TestCatalog.Load()).Analyze(tempDirectory.Path);
        var recommendations = scan.Recommendations.ToDictionary(item => item.Skill.Name, StringComparer.OrdinalIgnoreCase);

        Assert.Contains("React", scan.FrontendFrameworks);
        Assert.Contains("playwright-visual-testing", recommendations);
        Assert.DoesNotContain("astro-developer", recommendations);
    }

    [Fact]
    public void Analyze_DetectsBlazorAndStaticBrowserUi_ButSkipsApiOnlyWebProject()
    {
        using var blazorDirectory = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(blazorDirectory.Path, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        Directory.CreateDirectory(Path.Combine(blazorDirectory.Path, "Components"));
        File.WriteAllText(Path.Combine(blazorDirectory.Path, "Components", "Home.razor"), "<h1>Hello</h1>");

        var blazorRecommendations = new ProjectSkillRecommender(TestCatalog.Load())
            .Analyze(blazorDirectory.Path)
            .Recommendations
            .Select(item => item.Skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("blazor", blazorRecommendations);
        Assert.Contains("playwright-visual-testing", blazorRecommendations);

        using var staticUiDirectory = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(staticUiDirectory.Path, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        Directory.CreateDirectory(Path.Combine(staticUiDirectory.Path, "wwwroot"));
        File.WriteAllText(Path.Combine(staticUiDirectory.Path, "wwwroot", "index.html"), "<main>Hello</main>");

        var staticUiRecommendations = new ProjectSkillRecommender(TestCatalog.Load())
            .Analyze(staticUiDirectory.Path)
            .Recommendations
            .Select(item => item.Skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("playwright-visual-testing", staticUiRecommendations);

        using var apiDirectory = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(apiDirectory.Path, "Api.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var apiRecommendations = new ProjectSkillRecommender(TestCatalog.Load())
            .Analyze(apiDirectory.Path)
            .Recommendations
            .Select(item => item.Skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("playwright-visual-testing", apiRecommendations);
    }

    [Fact]
    public void Analyze_IgnoresVendoredFrontendDependencies()
    {
        using var tempDirectory = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "node_modules", "sample"));
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "node_modules", "sample", "package.json"),
            """
            {
              "dependencies": {
                "react": "^19.0.0"
              }
            }
            """);

        var recommendations = new ProjectSkillRecommender(TestCatalog.Load())
            .Analyze(tempDirectory.Path)
            .Recommendations
            .Select(item => item.Skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("playwright-visual-testing", recommendations);
    }
}
