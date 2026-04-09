using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class AgentInstallerTests
{
    [Fact]
    public void Install_CodexRoleFiles_WritesTomlRole()
    {
        var catalog = TestCatalog.LoadAgents();
        var installer = new AgentInstaller(catalog);
        using var tempDirectory = new TemporaryDirectory();
        var layout = AgentInstallTarget.Resolve(tempDirectory.Path, AgentPlatform.Codex, InstallScope.Project, projectDirectory: null);
        var selected = installer.SelectAgents(["router"], installAll: false);

        var summary = installer.Install(selected, layout, force: false);
        var installedPath = Path.Combine(tempDirectory.Path, "dotnet-router.toml");
        var contents = File.ReadAllText(installedPath);

        Assert.Equal(1, summary.InstalledCount);
        Assert.True(File.Exists(installedPath));
        Assert.Contains("name = \"dotnet-router\"", contents, StringComparison.Ordinal);
        Assert.Contains("description = ", contents, StringComparison.Ordinal);
        Assert.Contains("developer_instructions = ", contents, StringComparison.Ordinal);
        Assert.DoesNotContain("model = \"inherit\"", contents, StringComparison.Ordinal);

        var removeSummary = installer.Remove(selected, layout);

        Assert.Equal(1, removeSummary.RemovedCount);
        Assert.False(File.Exists(installedPath));
    }

    [Fact]
    public void Install_CopilotAgentFiles_WritesAgentMarkdown()
    {
        var catalog = TestCatalog.LoadAgents();
        var installer = new AgentInstaller(catalog);
        using var tempDirectory = new TemporaryDirectory();
        var layout = AgentInstallTarget.Resolve(tempDirectory.Path, AgentPlatform.Copilot, InstallScope.Project, projectDirectory: null);
        var selected = installer.SelectAgents(["router"], installAll: false);

        installer.Install(selected, layout, force: false);

        var installedPath = Path.Combine(tempDirectory.Path, "dotnet-router.agent.md");
        var contents = File.ReadAllText(installedPath);

        Assert.True(File.Exists(installedPath));
        Assert.Contains("tools:", contents, StringComparison.Ordinal);
        Assert.Contains("  - read", contents, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("# .NET Router", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void Install_MarkdownAgentFiles_CopiesAgentMarkdown()
    {
        var catalog = TestCatalog.LoadAgents();
        var installer = new AgentInstaller(catalog);
        using var tempDirectory = new TemporaryDirectory();
        var layout = AgentInstallTarget.Resolve(tempDirectory.Path, AgentPlatform.Claude, InstallScope.Project, projectDirectory: null);
        var selected = installer.SelectAgents(["router"], installAll: false);

        installer.Install(selected, layout, force: false);

        var installedPath = Path.Combine(tempDirectory.Path, "dotnet-router.md");
        var contents = File.ReadAllText(installedPath);

        Assert.True(File.Exists(installedPath));
        Assert.StartsWith("---", contents, StringComparison.Ordinal);
        Assert.Contains("name: dotnet-router", contents, StringComparison.Ordinal);
        Assert.Contains("# .NET Router", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void Install_RejectsAgentNameThatEscapesLayoutRoot()
    {
        using var tempDirectory = new TemporaryDirectory();
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "catalog", "Frameworks", "Aspire", "agents", "dotnet-safe-agent"));
        File.WriteAllText(
            Path.Combine(sourceDirectory.FullName, "AGENT.md"),
            """
            ---
            name: dotnet-safe-agent
            description: "safe"
            ---

            # Safe agent
            """);

        var manifest = new AgentManifest
        {
            Agents =
            [
                new AgentEntry
                {
                    Name = "../escape",
                    Title = "Escape",
                    Description = "Escape test",
                    Path = "catalog/Frameworks/Aspire/agents/dotnet-safe-agent"
                },
            ],
        };

        var catalog = AgentCatalogPackage.LoadFromManifest(new DirectoryInfo(tempDirectory.Path), manifest, "test payload");
        var installer = new AgentInstaller(catalog);
        using var installDirectory = new TemporaryDirectory();
        var layout = new AgentInstallLayout(AgentPlatform.Claude, InstallScope.Project, AgentInstallMode.MarkdownAgentFiles, new DirectoryInfo(installDirectory.Path), false);

        var exception = Assert.Throws<InvalidOperationException>(() => installer.Install(catalog.Agents, layout, force: false));

        Assert.Contains("must stay within", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "escape.md")));
    }
}
