using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class SkillInstallerTests
{
    [Fact]
    public void SelectSkills_ResolvesShortAliases()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkills(["aspire", "dotnet-orleans"], installAll: false);

        Assert.Equal(["dotnet-aspire", "dotnet-orleans"], selected.Select(skill => skill.Name).ToArray());
    }

    [Fact]
    public void InstallAndRemove_SkillDirectories_TracksInstalledVersions()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        using var tempDirectory = new TemporaryDirectory();
        var layout = SkillInstallTarget.Resolve(tempDirectory.Path, AgentPlatform.Codex, InstallScope.Project, projectDirectory: null);
        var selected = installer.SelectSkills(["aspire", "orleans"], installAll: false);

        var installSummary = installer.Install(selected, layout, force: false);
        var installed = installer.GetInstalledSkills(layout);

        Assert.Equal(2, installSummary.InstalledCount);
        Assert.Equal(0, installSummary.GeneratedAdapters);
        Assert.Equal(2, installed.Count);
        Assert.All(installed, record => Assert.True(record.IsCurrent));
        Assert.Contains(installed, record => record.Skill.Name == "dotnet-aspire");
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "dotnet-aspire", "SKILL.md")));

        var removeSummary = installer.Remove([selected[0]], layout);
        var remaining = installer.GetInstalledSkills(layout);

        Assert.Equal(1, removeSummary.RemovedCount);
        Assert.Empty(removeSummary.MissingSkills);
        Assert.DoesNotContain(remaining, record => record.Skill.Name == "dotnet-aspire");
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "dotnet-orleans", "SKILL.md")));
    }

    [Fact]
    public void Install_ClaudeLayout_UsesNativeSkillDirectory()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        using var tempDirectory = new TemporaryDirectory();
        var layout = SkillInstallTarget.Resolve(tempDirectory.Path, AgentPlatform.Claude, InstallScope.Project, projectDirectory: null);
        var selected = installer.SelectSkills(["aspire"], installAll: false);

        installer.Install(selected, layout, force: false);

        Assert.Equal(SkillInstallMode.SkillDirectories, layout.Mode);
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "dotnet-aspire", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "dotnet-aspire.md")));
    }

    [Fact]
    public void SelectSkillsFromPackages_ResolvesCuratedAndCategoryPackages()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromPackages(["orleans", "codequality"]);

        Assert.Contains(selected, skill => skill.Name == "dotnet-orleans");
        Assert.Contains(selected, skill => skill.Name == "dotnet-managedcode-orleans-graph");
        Assert.Contains(selected, skill => skill.Name == "dotnet-code-analysis");
        Assert.Contains(selected, skill => skill.Name == "dotnet-format");
        Assert.Equal(selected.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), selected.Count);
    }

    [Fact]
    public void Install_RejectsSkillNameThatEscapesLayoutRoot()
    {
        using var tempDirectory = new TemporaryDirectory();
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "catalog", "Frameworks", "Aspire", "skills", "dotnet-safe"));
        File.WriteAllText(Path.Combine(sourceDirectory.FullName, "SKILL.md"), "# Safe");
        File.WriteAllText(Path.Combine(sourceDirectory.FullName, "manifest.json"), """{"version":"1.0.0"}""");

        var manifest = new SkillManifest
        {
            Skills =
            [
                new SkillEntry
                {
                    Name = "../escape",
                    Title = "Escape",
                    Version = "1.0.0",
                    Category = "Test",
                    Type = "Framework",
                    Package = "Aspire",
                    Description = "Escape test",
                    Compatibility = "codex",
                    Path = "catalog/Frameworks/Aspire/skills/dotnet-safe"
                },
            ],
        };

        var catalog = SkillCatalogPackage.LoadFromManifest(new DirectoryInfo(tempDirectory.Path), manifest, "test payload", "test");
        var installer = new SkillInstaller(catalog);
        using var installDirectory = new TemporaryDirectory();
        var layout = new SkillInstallLayout(AgentPlatform.Codex, InstallScope.Project, SkillInstallMode.SkillDirectories, new DirectoryInfo(installDirectory.Path), false);

        var exception = Assert.Throws<InvalidOperationException>(() => installer.Install(catalog.Skills, layout, force: false));

        Assert.Contains("must stay within", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(tempDirectory.Path, "escape")));
    }
}
