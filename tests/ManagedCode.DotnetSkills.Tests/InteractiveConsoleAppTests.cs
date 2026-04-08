using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class InteractiveConsoleAppTests
{
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
            "Settings",
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
            "Install skills",  // home menu
            "Install skills",  // catalog action
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
            maybeShowToolUpdateAsync: _ => Task.CompletedTask,
            initialAgent: initialAgent,
            initialScope: initialScope,
            projectDirectory: projectDirectory);
    }
}

internal sealed class FakeInteractivePrompts(params object[] responses) : IInteractivePrompts
{
    private readonly Queue<object> queuedResponses = new(responses);

    public T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        var response = Dequeue(title);

        if (response is T typedChoice)
        {
            return typedChoice;
        }

        if (response is string label)
        {
            return choices.Single(choice => string.Equals(formatter(choice), label, StringComparison.Ordinal));
        }

        throw new InvalidOperationException($"Unsupported select response for {title}: {response.GetType().FullName}");
    }

    public IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        var response = Dequeue(title);

        if (response is IEnumerable<T> typedChoices)
        {
            return typedChoices.ToArray();
        }

        if (response is IEnumerable<string> labels)
        {
            var selected = labels.ToHashSet(StringComparer.Ordinal);
            return choices.Where(choice => selected.Contains(formatter(choice))).ToArray();
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
