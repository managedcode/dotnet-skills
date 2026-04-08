using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class ProjectSkillAutoSyncServiceTests
{
    [Fact]
    public void BuildPlan_PrunesStaleAutoManagedSkills_ButKeepsProtectedOnes()
    {
        using var tempDirectory = new TemporaryDirectory();
        var projectPath = Path.Combine(tempDirectory.Path, "App.csproj");
        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var layout = SkillInstallTarget.Resolve(
            explicitTargetPath: null,
            agent: AgentPlatform.Codex,
            scope: InstallScope.Project,
            projectDirectory: tempDirectory.Path);
        var selected = installer.SelectSkills(["aspire", "xunit", "graphify-dotnet"], installAll: false);

        installer.Install(selected, layout, force: false);

        var stateStore = new AutoManagedSkillStateStore();
        stateStore.Save(layout.PrimaryRoot, tempDirectory.Path, selected.Select(skill => skill.Name));

        var service = new ProjectSkillAutoSyncService(catalog);
        var plan = service.BuildPlan(tempDirectory.Path, layout, installer, prune: true);

        Assert.True(plan.MatchedPreviousProject);
        Assert.Contains(plan.SkillsToRemove, skill => skill.Name == "dotnet-aspire");
        Assert.DoesNotContain(plan.SkillsToRemove, skill => skill.Name == "dotnet-xunit");
        Assert.DoesNotContain(plan.SkillsToRemove, skill => skill.Name == "dotnet-graphify-dotnet");
        Assert.Contains(plan.ProtectedStaleSkills, skill => skill.Name == "dotnet-xunit");
        Assert.Contains(plan.ProtectedStaleSkills, skill => skill.Name == "dotnet-graphify-dotnet");
    }
}
