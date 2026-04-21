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
}
