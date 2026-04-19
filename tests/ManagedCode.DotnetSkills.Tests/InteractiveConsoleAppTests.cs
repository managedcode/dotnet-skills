using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class InteractiveConsoleAppTests
{
    [Fact]
    public void BuildPromptDisplayLabel_EscapesBundleLabelsWithBracketedNetAreas()
    {
        var label = "dotnet-base [.NET Foundations / Foundations] (4 skills)";

        var escaped = CommandCenterInteractivePrompts.BuildPromptDisplayLabel(label);

        Assert.Equal("dotnet-base [[.NET Foundations / Foundations]] (4 skills)", escaped);
    }

    [Fact]
    public async Task RunAsync_ReturnsZero_WhenUserExitsImmediately()
    {
        var prompts = new FakeInteractivePrompts("Exit");
        var app = CreateApp(prompts);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_CanChangePlatformAndScope()
    {
        var prompts = new FakeInteractivePrompts(
            "Workspace",
            "Install destination",
            "Platform",
            "Codex",
            "Scope",
            "Global",
            "Back",
            "Back",
            "Exit");

        var app = CreateApp(prompts);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(AgentPlatform.Codex, app.Session.Agent);
        Assert.Equal(InstallScope.Global, app.Session.Scope);
    }

    [Fact]
    public async Task RunAsync_CanInstallSkillFromInteractiveCatalog()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var aspireSkill = catalog.Skills.Single(skill => string.Equals(skill.Name, "dotnet-aspire", StringComparison.Ordinal));
        var prompts = new FakeInteractivePrompts(
            "Collections",
            "Browse a collection",
            "Aspire",
            "Install from a lane",
            "Frameworks",
            new[] { aspireSkill },
            true,
            "Back",
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", aspireSkill.Name)));
    }

    [Fact]
    public async Task RunAsync_CanInstallSkillFromDirectSkillsBrowser()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var aspireSkill = catalog.Skills.Single(skill => string.Equals(skill.Name, "dotnet-aspire", StringComparison.Ordinal));
        var prompts = new FakeInteractivePrompts(
            "Skills",
            "Install selected skills",
            new[] { aspireSkill },
            true,
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", aspireSkill.Name)));
    }

    [Fact]
    public async Task RunAsync_CanInspectBundle_WhenAreaLabelContainsBracketedNetText()
    {
        var prompts = new FakeInteractivePrompts(
            "Bundles",
            "Inspect a focused bundle",
            "dotnet-base",
            "Back",
            "Back",
            "Exit");

        var app = CreateApp(prompts);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_CanOpenCatalogAnalysisTree()
    {
        var prompts = new FakeInteractivePrompts(
            "Analysis",
            "View full skill tree",
            "Back",
            "Back",
            "Exit");

        var app = CreateApp(prompts);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_CanCopyInstalledSkillToAnotherTarget()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var aspireSkill = catalog.Skills.Single(skill => string.Equals(skill.Name, "dotnet-aspire", StringComparison.Ordinal));
        var sourceLayout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        new SkillInstaller(catalog).Install([aspireSkill], sourceLayout, force: true);

        var prompts = new FakeInteractivePrompts(
            "Installed",
            "Copy or move skills to another target",
            new[] { $"aspire ({aspireSkill.Version})" },
            "Claude",
            "Project",
            false,
            true,
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", aspireSkill.Name)));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory.Path, ".claude", "skills", aspireSkill.Name)));
    }

    [Fact]
    public async Task RunAsync_CanCopyInstalledAgentToAnotherTarget()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var agentCatalog = TestCatalog.LoadAgents();
        var agent = agentCatalog.Agents.OrderBy(entry => entry.Name, StringComparer.Ordinal).First();
        var agentLabel = agent.Name.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase)
            ? agent.Name["dotnet-".Length..]
            : agent.Name;
        var sourceLayout = AgentInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        new AgentInstaller(agentCatalog).Install([agent], sourceLayout, force: true);

        var prompts = new FakeInteractivePrompts(
            "Agents",
            "Copy or move agents to another target",
            new[] { agentLabel },
            "Claude",
            "Project",
            false,
            true,
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();
        var targetLayout = AgentInstallTarget.Resolve(null, AgentPlatform.Claude, InstallScope.Project, projectDirectory.Path);

        Assert.Equal(0, exitCode);
        Assert.True(new AgentInstaller(agentCatalog).IsInstalled(agent, sourceLayout));
        Assert.True(new AgentInstaller(agentCatalog).IsInstalled(agent, targetLayout));
    }

    [Fact]
    public async Task RunAsync_CanReviewInstalledSet_AndRemoveDeselectedSkills()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var installed = installer.SelectSkills(["aspire", "orleans"], installAll: false);
        var layout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        installer.Install(installed, layout, force: true);

        var prompts = new FakeInteractivePrompts(
            "Installed",
            "Review installed set",
            new[] { "aspire [" },
            true,
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-aspire")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-orleans")));
    }

    [Fact]
    public async Task RunAsync_CanRemoveAllInstalledSkillsFromInstalledSurface()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var installed = installer.SelectSkills(["aspire", "orleans"], installAll: false);
        var layout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        installer.Install(installed, layout, force: true);

        var prompts = new FakeInteractivePrompts(
            "Installed",
            "Remove all skills",
            true,
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-aspire")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-orleans")));
    }

    [Fact]
    public async Task RunAsync_CanBackOutOfRemoveInstalledSkillsSelection()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var installed = installer.SelectSkills(["aspire", "orleans"], installAll: false);
        var layout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        installer.Install(installed, layout, force: true);

        var prompts = new FakeInteractivePrompts(
            "Installed",
            "Remove selected installed skills",
            "Back",
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-aspire")));
        Assert.True(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-orleans")));
    }

    [Fact]
    public async Task RunAsync_CanUpdateAllOutdatedSkillsFromHomeScreen()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var aspireSkill = catalog.Skills.Single(skill => string.Equals(skill.Name, "dotnet-aspire", StringComparison.Ordinal));
        var layout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        installer.Install([aspireSkill], layout, force: true);

        var installedManifestPath = Path.Combine(projectDirectory.Path, ".codex", "skills", aspireSkill.Name, "manifest.json");
        var installedManifest = JsonNode.Parse(await File.ReadAllTextAsync(installedManifestPath))!.AsObject();
        installedManifest["version"] = "0.0.1";
        await File.WriteAllTextAsync(
            installedManifestPath,
            installedManifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var prompts = new FakeInteractivePrompts(
            "Update all skills",
            true,
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();
        var updatedRecord = installer.GetInstalledSkills(layout)
            .Single(record => string.Equals(record.Skill.Name, aspireSkill.Name, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(0, exitCode);
        Assert.True(updatedRecord.IsCurrent);
        Assert.Equal(aspireSkill.Version, updatedRecord.InstalledVersion);
    }

    [Fact]
    public async Task RunAsync_ExposesUpdateAllSkillsFromHomeScreen_WhenEverythingIsCurrent()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var aspireSkill = catalog.Skills.Single(skill => string.Equals(skill.Name, "dotnet-aspire", StringComparison.Ordinal));
        var layout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        installer.Install([aspireSkill], layout, force: true);

        var prompts = new FakeInteractivePrompts(
            "Update all skills",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();
        var installedRecord = installer.GetInstalledSkills(layout)
            .Single(record => string.Equals(record.Skill.Name, aspireSkill.Name, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(0, exitCode);
        Assert.True(installedRecord.IsCurrent);
        Assert.Equal(aspireSkill.Version, installedRecord.InstalledVersion);
    }

    [Fact]
    public async Task RunAsync_CanBackOutOfReviewOutdatedSkillsSelection()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var aspireSkill = catalog.Skills.Single(skill => string.Equals(skill.Name, "dotnet-aspire", StringComparison.Ordinal));
        var layout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        installer.Install([aspireSkill], layout, force: true);

        var installedManifestPath = Path.Combine(projectDirectory.Path, ".codex", "skills", aspireSkill.Name, "manifest.json");
        var installedManifest = JsonNode.Parse(await File.ReadAllTextAsync(installedManifestPath))!.AsObject();
        installedManifest["version"] = "0.0.1";
        await File.WriteAllTextAsync(
            installedManifestPath,
            installedManifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var prompts = new FakeInteractivePrompts(
            "Installed",
            "Review outdated skills",
            "Back",
            "Back",
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();
        var installedRecord = installer.GetInstalledSkills(layout)
            .Single(record => string.Equals(record.Skill.Name, aspireSkill.Name, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(0, exitCode);
        Assert.False(installedRecord.IsCurrent);
        Assert.Equal("0.0.1", installedRecord.InstalledVersion);
    }

    [Fact]
    public async Task RunAsync_CanRemoveAllInstalledSkillsFromHomeScreen()
    {
        using var projectDirectory = new TemporaryDirectory();
        var catalog = TestCatalog.Load();
        var installer = new SkillInstaller(catalog);
        var installed = installer.SelectSkills(["aspire", "orleans"], installAll: false);
        var layout = SkillInstallTarget.Resolve(null, AgentPlatform.Codex, InstallScope.Project, projectDirectory.Path);
        installer.Install(installed, layout, force: true);

        var prompts = new FakeInteractivePrompts(
            "Remove all skills",
            true,
            "Exit");

        var app = CreateApp(
            prompts,
            catalog,
            initialAgent: AgentPlatform.Codex,
            initialScope: InstallScope.Project,
            projectDirectory: projectDirectory.Path);

        var exitCode = await app.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-aspire")));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory.Path, ".codex", "skills", "dotnet-orleans")));
    }

    private static InteractiveConsoleApp CreateApp(
        FakeInteractivePrompts prompts,
        SkillCatalogPackage? catalog = null,
        AgentPlatform initialAgent = AgentPlatform.Auto,
        InstallScope initialScope = InstallScope.Project,
        string? projectDirectory = null)
    {
        catalog ??= TestCatalog.Load();
        var agentCatalog = TestCatalog.LoadAgents();

        return new InteractiveConsoleApp(
            prompts: prompts,
            loadSkillCatalogAsync: (_, _, _, _) => Task.FromResult(catalog),
            loadAgentCatalog: () => agentCatalog,
            getToolUpdateStatusAsync: _ => Task.FromResult<ToolUpdateStatusInfo?>(null),
            initialAgent: initialAgent,
            initialScope: initialScope,
            projectDirectory: projectDirectory);
    }
}

internal sealed class FakeInteractivePrompts(params object[] responses) : IInteractivePrompts
{
    private readonly Queue<object> queuedResponses = new(responses);

    public HomeActionView SelectHomeAction(IReadOnlyList<HomeActionView> choices)
    {
        var response = Dequeue("Home");

        if (response is HomeActionView action)
        {
            return action;
        }

        if (response is string label)
        {
            var exact = choices.Where(choice => string.Equals(choice.Label, label, StringComparison.Ordinal)).ToArray();
            if (exact.Length == 1)
            {
                return exact[0];
            }

            var hotKey = choices.Where(choice => string.Equals(choice.HotKey.ToString(), label, StringComparison.Ordinal)).ToArray();
            if (hotKey.Length == 1)
            {
                return hotKey[0];
            }

            var prefix = choices.Where(choice => choice.Label.StartsWith(label, StringComparison.Ordinal)).ToArray();
            if (prefix.Length == 1)
            {
                return prefix[0];
            }
        }

        throw new InvalidOperationException($"Unsupported home action response: {response.GetType().FullName}");
    }

    public T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        var response = Dequeue(title);

        if (response is T typedChoice)
        {
            return typedChoice;
        }

        if (response is string label)
        {
            var exact = choices.Where(choice => string.Equals(formatter(choice), label, StringComparison.Ordinal)).ToArray();
            if (exact.Length == 1)
            {
                return exact[0];
            }

            var prefix = choices.Where(choice => formatter(choice).StartsWith(label, StringComparison.Ordinal)).ToArray();
            if (prefix.Length == 1)
            {
                return prefix[0];
            }
        }

        throw new InvalidOperationException($"Unsupported select response for {title}: {response.GetType().FullName}");
    }

    public IReadOnlyList<T>? MultiSelect<T>(
        string title,
        IReadOnlyList<T> choices,
        Func<T, string> formatter,
        IReadOnlyList<T>? initiallySelected = null,
        string? backLabel = null) where T : notnull
    {
        var response = Dequeue(title);

        if (backLabel is not null
            && response is string backResponse
            && string.Equals(backResponse, backLabel, StringComparison.Ordinal))
        {
            return null;
        }

        if (response is IEnumerable<T> typedChoices)
        {
            return typedChoices.ToArray();
        }

        if (response is IEnumerable<string> labels)
        {
            return labels
                .Select(label =>
                {
                    var exact = choices.Where(choice => string.Equals(formatter(choice), label, StringComparison.Ordinal)).ToArray();
                    if (exact.Length == 1)
                    {
                        return exact[0];
                    }

                    var prefix = choices.Where(choice => formatter(choice).StartsWith(label, StringComparison.Ordinal)).ToArray();
                    if (prefix.Length == 1)
                    {
                        return prefix[0];
                    }

                    throw new InvalidOperationException($"Could not match multi-select response '{label}' for {title}.");
                })
                .ToArray();
        }

        throw new InvalidOperationException($"Unsupported multi-select response for {title}: {response.GetType().FullName}");
    }

    public bool Confirm(string title, bool defaultValue)
    {
        var response = Dequeue(title);
        return response switch
        {
            bool value => value,
            _ => throw new InvalidOperationException($"Unsupported confirm response for {title}: {response.GetType().FullName}"),
        };
    }

    public void Pause(string title)
    {
    }

    private object Dequeue(string title)
    {
        if (!queuedResponses.TryDequeue(out var response))
        {
            throw new InvalidOperationException($"No queued response was available for {title}.");
        }

        return response;
    }
}
