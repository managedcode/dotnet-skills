using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class SkillCatalogPackageTests
{
    [Fact]
    public void LoadFromDirectory_AcceptsUppercaseSkillsFolder()
    {
        using var tempDirectory = new TemporaryDirectory();

        var skillsRoot = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "Skills"));
        var catalogRoot = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "catalog"));
        Directory.CreateDirectory(Path.Combine(skillsRoot.FullName, "dotnet-aspire"));

        File.WriteAllText(
            Path.Combine(catalogRoot.FullName, "skills.json"),
            """
            {
              "skills": [
                {
                  "name": "dotnet-aspire",
                  "title": ".NET Aspire",
                  "version": "1.0.0",
                  "category": "Cloud",
                  "description": "Use .NET Aspire.",
                  "compatibility": "codex,claude,copilot,gemini",
                  "path": "skills/dotnet-aspire"
                }
              ]
            }
            """);

        var package = SkillCatalogPackage.LoadFromDirectory(
            new DirectoryInfo(tempDirectory.Path),
            "test payload",
            "test");

        Assert.True(Directory.Exists(package.SkillsRoot.FullName));
        Assert.Equal("dotnet-aspire", package.Skills.Single().Name);
        Assert.Equal("dotnet-aspire", package.ResolveSkillSource("dotnet-aspire").Name);
    }
}
