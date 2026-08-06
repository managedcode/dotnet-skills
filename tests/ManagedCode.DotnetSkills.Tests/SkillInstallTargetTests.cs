using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class SkillInstallTargetTests
{
    [Theory]
    [MemberData(nameof(ProjectCases))]
    public void ResolveAllDetectedProject_UsesNativeRootsOrSharedFallback(
        bool hasCodex,
        bool hasClaude,
        bool hasCopilot,
        bool hasGemini,
        bool hasJunie,
        bool hasGrok,
        bool hasSharedFallback)
    {
        using var tempDirectory = new TemporaryDirectory();
        CreatePlatformDirectories(tempDirectory.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback);

        var layouts = SkillInstallTarget.ResolveAllDetected(tempDirectory.Path, InstallScope.Project);
        var expected = BuildExpectedProjectLayouts(tempDirectory.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback);

        Assert.Equal(expected.Select(item => item.Platform).ToArray(), layouts.Select(layout => layout.Agent).ToArray());
        Assert.Equal(expected.Select(item => item.Path).ToArray(), layouts.Select(layout => layout.PrimaryRoot.FullName).ToArray());
        Assert.All(layouts, layout => Assert.Equal(SkillInstallMode.SkillDirectories, layout.Mode));
    }

    [Theory]
    [MemberData(nameof(ProjectCases))]
    public void ResolveAutoProject_SelectsFirstNativeRootOrSharedFallback(
        bool hasCodex,
        bool hasClaude,
        bool hasCopilot,
        bool hasGemini,
        bool hasJunie,
        bool hasGrok,
        bool hasSharedFallback)
    {
        using var tempDirectory = new TemporaryDirectory();
        CreatePlatformDirectories(tempDirectory.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback);

        var layout = SkillInstallTarget.Resolve(
            explicitTargetPath: null,
            agent: AgentPlatform.Auto,
            scope: InstallScope.Project,
            projectDirectory: tempDirectory.Path);

        var expected = BuildExpectedProjectLayouts(tempDirectory.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback)[0];

        Assert.Equal(expected.Platform, layout.Agent);
        Assert.Equal(expected.Path, layout.PrimaryRoot.FullName);
        Assert.Equal(SkillInstallMode.SkillDirectories, layout.Mode);
    }

    [Theory]
    [MemberData(nameof(GlobalCases))]
    public void ResolveAllDetectedGlobal_UsesNativeRootsOrSharedFallback(
        bool hasCodex,
        bool hasClaude,
        bool hasCopilot,
        bool hasGemini,
        bool hasJunie,
        bool hasGrok,
        bool hasSharedFallback)
    {
        using var tempHome = new TemporaryDirectory();

        lock (TestEnvironmentLocks.UserProfile)
        {
            var previousHome = SetEnvironment("HOME", tempHome.Path);
            var previousUserProfile = SetEnvironment("USERPROFILE", tempHome.Path);
            var previousCodexHome = SetEnvironment("CODEX_HOME", null);

            try
            {
                CreatePlatformDirectories(tempHome.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback);

                var layouts = SkillInstallTarget.ResolveAllDetected(tempHome.Path, InstallScope.Global);
                var expected = BuildExpectedGlobalLayouts(tempHome.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback);

                Assert.Equal(expected.Select(item => item.Platform).ToArray(), layouts.Select(layout => layout.Agent).ToArray());
                Assert.Equal(expected.Select(item => item.Path).ToArray(), layouts.Select(layout => layout.PrimaryRoot.FullName).ToArray());
                Assert.All(layouts, layout => Assert.Equal(SkillInstallMode.SkillDirectories, layout.Mode));
            }
            finally
            {
                SetEnvironment("HOME", previousHome);
                SetEnvironment("USERPROFILE", previousUserProfile);
                SetEnvironment("CODEX_HOME", previousCodexHome);
            }
        }
    }

    [Theory]
    [MemberData(nameof(GlobalCases))]
    public void ResolveAutoGlobal_SelectsFirstNativeRootOrSharedFallback(
        bool hasCodex,
        bool hasClaude,
        bool hasCopilot,
        bool hasGemini,
        bool hasJunie,
        bool hasGrok,
        bool hasSharedFallback)
    {
        using var tempHome = new TemporaryDirectory();

        lock (TestEnvironmentLocks.UserProfile)
        {
            var previousHome = SetEnvironment("HOME", tempHome.Path);
            var previousUserProfile = SetEnvironment("USERPROFILE", tempHome.Path);
            var previousCodexHome = SetEnvironment("CODEX_HOME", null);

            try
            {
                CreatePlatformDirectories(tempHome.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback);

                var layout = SkillInstallTarget.Resolve(
                    explicitTargetPath: null,
                    agent: AgentPlatform.Auto,
                    scope: InstallScope.Global,
                    projectDirectory: tempHome.Path);

                var expected = BuildExpectedGlobalLayouts(tempHome.Path, hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback)[0];

                Assert.Equal(expected.Platform, layout.Agent);
                Assert.Equal(expected.Path, layout.PrimaryRoot.FullName);
                Assert.Equal(SkillInstallMode.SkillDirectories, layout.Mode);
            }
            finally
            {
                SetEnvironment("HOME", previousHome);
                SetEnvironment("USERPROFILE", previousUserProfile);
                SetEnvironment("CODEX_HOME", previousCodexHome);
            }
        }
    }

    [Fact]
    public void ResolveAutoGlobal_UsesCodexHomeEnvironmentWhenSet()
    {
        using var tempHome = new TemporaryDirectory();
        using var codexHome = new TemporaryDirectory();

        lock (TestEnvironmentLocks.UserProfile)
        {
            var previousHome = SetEnvironment("HOME", tempHome.Path);
            var previousUserProfile = SetEnvironment("USERPROFILE", tempHome.Path);
            var previousCodexHome = SetEnvironment("CODEX_HOME", codexHome.Path);

            try
            {
                Directory.CreateDirectory(codexHome.Path);

                var layout = SkillInstallTarget.Resolve(
                    explicitTargetPath: null,
                    agent: AgentPlatform.Auto,
                    scope: InstallScope.Global,
                    projectDirectory: tempHome.Path);

                Assert.Equal(AgentPlatform.Codex, layout.Agent);
                Assert.Equal(Path.Combine(codexHome.Path, "skills"), layout.PrimaryRoot.FullName);
                Assert.Equal(SkillInstallMode.SkillDirectories, layout.Mode);
            }
            finally
            {
                SetEnvironment("HOME", previousHome);
                SetEnvironment("USERPROFILE", previousUserProfile);
                SetEnvironment("CODEX_HOME", previousCodexHome);
            }
        }
    }

    [Fact]
    public void ResolveAutoProject_UsesConfiguredDefaultTargetBeforeDetectedPlatforms()
    {
        using var tempDirectory = new TemporaryDirectory();

        lock (TestEnvironmentLocks.UserProfile)
        {
            var previousTarget = SetEnvironment("DOTNET_SKILLS_DEFAULT_TARGET", ".team/skills");

            try
            {
                Directory.CreateDirectory(Path.Combine(tempDirectory.Path, ".codex"));

                var layout = SkillInstallTarget.Resolve(
                    explicitTargetPath: null,
                    agent: AgentPlatform.Auto,
                    scope: InstallScope.Project,
                    projectDirectory: tempDirectory.Path);

                Assert.Equal(AgentPlatform.Agents, layout.Agent);
                Assert.Equal(Path.Combine(tempDirectory.Path, ".team", "skills"), layout.PrimaryRoot.FullName);
                Assert.False(layout.IsExplicitTarget);
            }
            finally
            {
                SetEnvironment("DOTNET_SKILLS_DEFAULT_TARGET", previousTarget);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ExplicitTargetCases))]
    public void Resolve_WithExplicitTarget_UsesProvidedPathAndDirectoryMode(object agent, object scope)
    {
        using var tempDirectory = new TemporaryDirectory();
        var explicitPath = Path.Combine(tempDirectory.Path, "explicit-target");

        var resolvedAgent = (AgentPlatform)agent;
        var resolvedScope = (InstallScope)scope;

        var layout = SkillInstallTarget.Resolve(
            explicitTargetPath: explicitPath,
            agent: resolvedAgent,
            scope: resolvedScope,
            projectDirectory: tempDirectory.Path);

        Assert.Equal(resolvedAgent, layout.Agent);
        Assert.Equal(resolvedScope, layout.Scope);
        Assert.Equal(SkillInstallMode.SkillDirectories, layout.Mode);
        Assert.True(layout.IsExplicitTarget);
        Assert.Equal(Path.GetFullPath(explicitPath), layout.PrimaryRoot.FullName);
    }

    [Theory]
    [MemberData(nameof(AgentAliasCases))]
    public void ParseAgent_RecognizesSharedAndGrokAliases(string value, object expected)
    {
        Assert.Equal((AgentPlatform)expected, SkillInstallTarget.ParseAgent(value));
    }

    public static IEnumerable<object[]> ProjectCases()
    {
        foreach (var hasCodex in new[] { false, true })
            foreach (var hasClaude in new[] { false, true })
                foreach (var hasCopilot in new[] { false, true })
                    foreach (var hasGemini in new[] { false, true })
                        foreach (var hasJunie in new[] { false, true })
                            foreach (var hasGrok in new[] { false, true })
                                foreach (var hasSharedFallback in new[] { false, true })
                                {
                                    yield return [hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback];
                                }
    }

    public static IEnumerable<object[]> GlobalCases()
    {
        foreach (var hasCodex in new[] { false, true })
            foreach (var hasClaude in new[] { false, true })
                foreach (var hasCopilot in new[] { false, true })
                    foreach (var hasGemini in new[] { false, true })
                        foreach (var hasJunie in new[] { false, true })
                            foreach (var hasGrok in new[] { false, true })
                                foreach (var hasSharedFallback in new[] { false, true })
                                {
                                    yield return [hasCodex, hasClaude, hasCopilot, hasGemini, hasJunie, hasGrok, hasSharedFallback];
                                }
    }

    public static IEnumerable<object[]> ExplicitTargetCases()
    {
        foreach (var agent in new[]
                 {
                     AgentPlatform.Auto,
                     AgentPlatform.Agents,
                     AgentPlatform.Codex,
                     AgentPlatform.Claude,
                     AgentPlatform.Copilot,
                     AgentPlatform.Gemini,
                     AgentPlatform.Junie,
                     AgentPlatform.Grok,
                 })
            foreach (var scope in new[]
                     {
                     InstallScope.Project,
                     InstallScope.Global,
                 })
            {
                yield return [agent, scope];
            }
    }

    public static IEnumerable<object[]> AgentAliasCases()
    {
        yield return ["agents", AgentPlatform.Agents];
        yield return ["shared", AgentPlatform.Agents];
        yield return ["grok", AgentPlatform.Grok];
        yield return ["grok-build", AgentPlatform.Grok];
        yield return ["xai", AgentPlatform.Grok];
    }

    private static IReadOnlyList<ResolvedLayout> BuildExpectedProjectLayouts(
        string rootPath,
        bool hasCodex,
        bool hasClaude,
        bool hasCopilot,
        bool hasGemini,
        bool hasJunie,
        bool hasGrok,
        bool hasSharedFallback)
    {
        if (hasSharedFallback)
        {
            return [new ResolvedLayout(AgentPlatform.Agents, Path.Combine(rootPath, ".agents", "skills"))];
        }

        var layouts = new List<ResolvedLayout>();

        if (hasCodex)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Codex, Path.Combine(rootPath, ".codex", "skills")));
        }

        if (hasClaude)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Claude, Path.Combine(rootPath, ".claude", "skills")));
        }

        if (hasCopilot)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Copilot, Path.Combine(rootPath, ".github", "skills")));
        }

        if (hasGemini)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Gemini, Path.Combine(rootPath, ".gemini", "skills")));
        }

        if (hasJunie)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Junie, Path.Combine(rootPath, ".junie", "skills")));
        }

        if (hasGrok)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Grok, Path.Combine(rootPath, ".grok", "skills")));
        }

        if (layouts.Count > 0)
        {
            return layouts;
        }

        return [new ResolvedLayout(AgentPlatform.Agents, Path.Combine(rootPath, ".agents", "skills"))];
    }

    private static IReadOnlyList<ResolvedLayout> BuildExpectedGlobalLayouts(
        string homePath,
        bool hasCodex,
        bool hasClaude,
        bool hasCopilot,
        bool hasGemini,
        bool hasJunie,
        bool hasGrok,
        bool hasSharedFallback)
    {
        if (hasSharedFallback)
        {
            return [new ResolvedLayout(AgentPlatform.Agents, Path.Combine(homePath, ".agents", "skills"))];
        }

        var layouts = new List<ResolvedLayout>();

        if (hasCodex)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Codex, Path.Combine(homePath, ".codex", "skills")));
        }

        if (hasClaude)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Claude, Path.Combine(homePath, ".claude", "skills")));
        }

        if (hasCopilot)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Copilot, Path.Combine(homePath, ".copilot", "skills")));
        }

        if (hasGemini)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Gemini, Path.Combine(homePath, ".gemini", "skills")));
        }

        if (hasJunie)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Junie, Path.Combine(homePath, ".junie", "skills")));
        }

        if (hasGrok)
        {
            layouts.Add(new ResolvedLayout(AgentPlatform.Grok, Path.Combine(homePath, ".grok", "skills")));
        }

        if (layouts.Count > 0)
        {
            return layouts;
        }

        return [new ResolvedLayout(AgentPlatform.Agents, Path.Combine(homePath, ".agents", "skills"))];
    }

    private static void CreatePlatformDirectories(
        string rootDirectory,
        bool hasCodex,
        bool hasClaude,
        bool hasCopilot,
        bool hasGemini,
        bool hasJunie,
        bool hasGrok,
        bool hasSharedFallback)
    {
        if (hasCodex)
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".codex"));
        }

        if (hasClaude)
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".claude"));
        }

        if (hasCopilot)
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".copilot"));
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".github"));
        }

        if (hasGemini)
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".gemini"));
        }

        if (hasJunie)
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".junie"));
        }

        if (hasGrok)
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".grok"));
        }

        if (hasSharedFallback)
        {
            Directory.CreateDirectory(Path.Combine(rootDirectory, ".agents"));
        }
    }

    private static string? SetEnvironment(string variable, string? value)
    {
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, value);
        return previous;
    }

    private sealed record ResolvedLayout(AgentPlatform Platform, string Path);
}
