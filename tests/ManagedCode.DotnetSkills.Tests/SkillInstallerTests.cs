using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class SkillInstallerTests
{
    [Fact]
    public void SelectSkills_ResolvesShortAliases()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkills(["aspire", "orleans"], installAll: false);

        Assert.Equal(["aspire", "orleans"], selected.Select(skill => skill.Name).ToArray());
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
        Assert.Contains(installed, record => record.Skill.Name == "aspire");
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "aspire", "SKILL.md")));

        var removeSummary = installer.Remove([selected[0]], layout);
        var remaining = installer.GetInstalledSkills(layout);

        Assert.Equal(1, removeSummary.RemovedCount);
        Assert.Empty(removeSummary.MissingSkills);
        Assert.DoesNotContain(remaining, record => record.Skill.Name == "aspire");
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "orleans", "SKILL.md")));
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
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "aspire", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "aspire.md")));
    }

    [Fact]
    public void SelectSkillsFromPackages_ResolvesFocusedBundlesOnly()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromPackages(["orleans", "quality"]);

        Assert.Contains(selected, skill => skill.Name == "orleans");
        Assert.Contains(selected, skill => skill.Name == "managedcode-orleans-graph");
        Assert.Contains(selected, skill => skill.Name == "code-analysis");
        Assert.Contains(selected, skill => skill.Name == "complexity");
        Assert.Contains(selected, skill => skill.Name == "crap-score");
        Assert.Contains(selected, skill => skill.Name == "format");
        Assert.Equal(selected.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), selected.Count);
    }

    [Fact]
    public void SelectSkillsFromPackages_RejectsRemovedCategoryBundleAliases()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var exception = Assert.Throws<InvalidOperationException>(() => installer.SelectSkillsFromPackages(["codequality"]));

        Assert.Contains("Unknown bundle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectSkillsFromCollections_ResolvesCollectionAliases()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromCollections(["aspire", "azure-functions", "background-workers", "quality"]);

        Assert.Contains(selected, skill => skill.Name == "aspire");
        Assert.Contains(selected, skill => skill.Name == "azure-functions");
        Assert.Contains(selected, skill => skill.Name == "worker-services");
        Assert.DoesNotContain(selected, skill => skill.Name == "orleans");
        Assert.Contains(selected, skill => skill.Name == "code-analysis");
        Assert.Contains(selected, skill => skill.Name == "format");
        Assert.Equal(selected.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), selected.Count);
    }

    [Fact]
    public void SelectSkillsFromCollections_KeepsDistributedFocusedOnDistributedRuntimeSkills()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromCollections(["distributed"]);

        Assert.Contains(selected, skill => skill.Name == "orleans");
        Assert.Contains(selected, skill => skill.Name == "managedcode-orleans-graph");
        Assert.Contains(selected, skill => skill.Name == "managedcode-orleans-signalr");
        Assert.DoesNotContain(selected, skill => skill.Name == "azure-functions");
        Assert.DoesNotContain(selected, skill => skill.Name == "aspire");
        Assert.Equal(selected.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), selected.Count);
    }

    [Fact]
    public void SelectSkillsFromCollections_ResolvesMobileDeviceAliases_WithoutPullingDesktopOnlySkills()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromCollections(["mobile-device"]);

        Assert.Contains(selected, skill => skill.Name == "maui");
        Assert.Contains(selected, skill => skill.Name == "maui-doctor");
        Assert.DoesNotContain(selected, skill => skill.Name == "android-tombstone-symbolication");
        Assert.DoesNotContain(selected, skill => skill.Name == "mixed-reality");
        Assert.DoesNotContain(selected, skill => skill.Name == "winforms");
        Assert.DoesNotContain(selected, skill => skill.Name == "wpf");
        Assert.Equal(selected.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), selected.Count);
    }

    [Fact]
    public void SelectSkillsFromCollections_ResolvesXrSpatialAliases_WithoutPullingMobileOrAiSkills()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromCollections(["xr-spatial"]);

        Assert.Contains(selected, skill => skill.Name == "mixed-reality");
        Assert.DoesNotContain(selected, skill => skill.Name == "maui");
        Assert.DoesNotContain(selected, skill => skill.Name == "technology-selection");
        Assert.Equal(selected.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), selected.Count);
    }

    [Fact]
    public void SelectSkillsFromCollections_ResolvesTestingResearchAliases_WithoutPullingDefaultTestingFlow()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromCollections(["testing-research"]);

        Assert.Contains(selected, skill => skill.Name == "code-testing-agent");
        Assert.Contains(selected, skill => skill.Name == "stryker");
        Assert.Contains(selected, skill => skill.Name == "exp-test-gap-analysis");
        Assert.DoesNotContain(selected, skill => skill.Name == "xunit");
        Assert.DoesNotContain(selected, skill => skill.Name == "coverlet");
        Assert.Equal(selected.Select(skill => skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(), selected.Count);
    }

    [Fact]
    public void SelectSkillsFromCollections_ResolvesExplicitBuildSurfaces_WithoutMixingThemBackTogether()
    {
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);

        var selected = installer.SelectSkillsFromCollections(["msbuild", "nuget-publishing", "templates-scaffolding"]);

        Assert.Contains(selected, skill => skill.Name == "msbuild-modernization");
        Assert.Contains(selected, skill => skill.Name == "convert-to-cpm");
        Assert.Contains(selected, skill => skill.Name == "template-authoring");
        Assert.DoesNotContain(selected, skill => skill.Name == "csharp-scripts");
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
                    Path = "catalog/Frameworks/Aspire/skills/safe"
                },
            ],
        };

        var catalog = SkillCatalogPackage.LoadFromManifest(new DirectoryInfo(tempDirectory.Path), manifest, "test payload", "test");
        var installer = new SkillInstaller(catalog);
        using var installDirectory = new TemporaryDirectory();
        var layout = new SkillInstallLayout(AgentPlatform.Codex, InstallScope.Project, SkillInstallMode.SkillDirectories, new DirectoryInfo(installDirectory.Path), false);

        var exception = Assert.Throws<InvalidOperationException>(() => installer.Install(catalog.Skills, layout, force: false));

        Assert.Contains("Skill payload is missing for ../escape", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(tempDirectory.Path, "escape")));
    }
}
