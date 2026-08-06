namespace ManagedCode.DotnetSkills.Runtime;

internal enum AgentPlatform
{
    Auto,
    Agents,
    Codex,
    Claude,
    Copilot,
    Gemini,
    Junie,
    Grok,
}

internal enum InstallScope
{
    Global,
    Project,
}

internal enum SkillInstallMode
{
    SkillDirectories,
}

internal sealed record SkillInstallLayout(
    AgentPlatform Agent,
    InstallScope Scope,
    SkillInstallMode Mode,
    DirectoryInfo PrimaryRoot,
    bool IsExplicitTarget)
{
    public string PrimaryPath => PrimaryRoot.FullName;

    public string ReloadHint => Agent switch
    {
        AgentPlatform.Auto => "Restart your agent session to pick up new skills.",
        AgentPlatform.Agents => "Restart your agent session to pick up shared Agent Skills.",
        AgentPlatform.Codex => "Restart Codex to pick up new skills.",
        AgentPlatform.Claude => "Restart Claude Code or start a new session to pick up new skills.",
        AgentPlatform.Copilot => "Restart Copilot CLI or your IDE agent session to pick up new skills.",
        AgentPlatform.Gemini => "Run /skills reload or restart Gemini CLI to pick up new skills.",
        AgentPlatform.Junie => "Restart Junie or reload the project to pick up new skills.",
        AgentPlatform.Grok => "Restart Grok Build to pick up new skills.",
        _ => "Restart your agent session to pick up new skills.",
    };
}

internal static class SkillInstallTarget
{
    public static SkillInstallLayout Resolve(
        string? explicitTargetPath,
        AgentPlatform agent,
        InstallScope scope,
        string? projectDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitTargetPath))
        {
            return ResolveExplicit(agent, scope, explicitTargetPath);
        }

        var context = InstallPathContext.Create(projectDirectory);

        var configuredRoot = context.ResolveConfiguredRoot(ToolIdentity.SkillsDefaultTargetEnvironmentVariable, scope);
        if (configuredRoot is not null)
        {
            var configuredPlatform = agent == AgentPlatform.Auto ? AgentPlatform.Agents : agent;
            return InstallPlatformRegistry.Get(configuredPlatform)
                .CreateSkillLayout(scope, configuredRoot, isExplicitTarget: false);
        }

        if (agent == AgentPlatform.Auto)
        {
            return ResolveDetected(context, scope)[0];
        }

        var strategy = InstallPlatformRegistry.Get(agent);
        return strategy.CreateSkillLayout(scope, strategy.GetSkillRoot(context, scope), isExplicitTarget: false);
    }

    public static AgentPlatform ParseAgent(string value) => value.ToLowerInvariant() switch
    {
        "auto" => AgentPlatform.Auto,
        "agents" => AgentPlatform.Agents,
        "shared" => AgentPlatform.Agents,
        "codex" => AgentPlatform.Codex,
        "openai" => AgentPlatform.Codex,
        "claude" => AgentPlatform.Claude,
        "anthropic" => AgentPlatform.Claude,
        "copilot" => AgentPlatform.Copilot,
        "github" => AgentPlatform.Copilot,
        "github-copilot" => AgentPlatform.Copilot,
        "gemini" => AgentPlatform.Gemini,
        "google" => AgentPlatform.Gemini,
        "google-gemini" => AgentPlatform.Gemini,
        "junie" => AgentPlatform.Junie,
        "jetbrains" => AgentPlatform.Junie,
        "grok" => AgentPlatform.Grok,
        "grok-build" => AgentPlatform.Grok,
        "xai" => AgentPlatform.Grok,
        _ => throw new InvalidOperationException("Unsupported agent: " + value + ". Expected auto, agents, shared, codex, openai, claude, anthropic, copilot, github-copilot, gemini, google-gemini, junie, jetbrains, grok, grok-build, or xai."),
    };

    public static InstallScope ParseScope(string value) => value.ToLowerInvariant() switch
    {
        "global" => InstallScope.Global,
        "project" => InstallScope.Project,
        _ => throw new InvalidOperationException($"Unsupported scope: {value}. Expected global or project."),
    };

    public static IReadOnlyList<SkillInstallLayout> ResolveAllDetected(string? projectDirectory, InstallScope scope)
    {
        var context = InstallPathContext.Create(projectDirectory);
        var configuredRoot = context.ResolveConfiguredRoot(ToolIdentity.SkillsDefaultTargetEnvironmentVariable, scope);
        if (configuredRoot is not null)
        {
            return
            [
                InstallPlatformRegistry.Get(AgentPlatform.Agents)
                    .CreateSkillLayout(scope, configuredRoot, isExplicitTarget: false),
            ];
        }

        return ResolveDetected(context, scope);
    }

    private static SkillInstallLayout ResolveExplicit(AgentPlatform agent, InstallScope scope, string explicitTargetPath)
    {
        var targetRoot = InstallPathContext.ResolveExplicitRoot(explicitTargetPath);
        if (agent == AgentPlatform.Auto)
        {
            return new SkillInstallLayout(AgentPlatform.Auto, scope, SkillInstallMode.SkillDirectories, targetRoot, IsExplicitTarget: true);
        }

        return InstallPlatformRegistry.Get(agent).CreateSkillLayout(scope, targetRoot, isExplicitTarget: true);
    }

    private static IReadOnlyList<SkillInstallLayout> ResolveDetected(InstallPathContext context, InstallScope scope)
    {
        var layouts = ResolveNativeLayouts(context, scope);

        if (layouts.Count > 0)
        {
            return layouts;
        }

        return [CreateDefaultFallbackLayout(context, scope)];
    }

    private static IReadOnlyList<SkillInstallLayout> ResolveNativeLayouts(InstallPathContext context, InstallScope scope)
    {
        var sharedStrategy = InstallPlatformRegistry.Get(AgentPlatform.Agents);
        if (sharedStrategy.HasNativeRoot(context, scope))
        {
            return [sharedStrategy.CreateSkillLayout(scope, sharedStrategy.GetSkillRoot(context, scope), isExplicitTarget: false)];
        }

        return InstallPlatformRegistry.StrategiesInDetectionOrder
            .Where(strategy => strategy.Platform != AgentPlatform.Agents)
            .Where(strategy => strategy.HasNativeRoot(context, scope))
            .Select(strategy => strategy.CreateSkillLayout(scope, strategy.GetSkillRoot(context, scope), isExplicitTarget: false))
            .DistinctBy(layout => layout.PrimaryRoot.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SkillInstallLayout CreateDefaultFallbackLayout(InstallPathContext context, InstallScope scope)
    {
        var sharedStrategy = InstallPlatformRegistry.Get(AgentPlatform.Agents);
        return sharedStrategy.CreateSkillLayout(
            scope,
            sharedStrategy.GetSkillRoot(context, scope),
            isExplicitTarget: false);
    }
}
