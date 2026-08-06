namespace ManagedCode.DotnetSkills.Runtime;

internal enum AgentInstallMode
{
    MarkdownAgentFiles,
    CopilotAgentFiles,
    CodexRoleFiles,
}

internal sealed record AgentInstallLayout(
    AgentPlatform Agent,
    InstallScope Scope,
    AgentInstallMode Mode,
    DirectoryInfo PrimaryRoot,
    bool IsExplicitTarget)
{
    public string PrimaryPath => PrimaryRoot.FullName;

    public string ReloadHint => Agent switch
    {
        AgentPlatform.Auto => "Restart your agent session to pick up new agents.",
        AgentPlatform.Agents => "Restart your agent session to pick up shared agents.",
        AgentPlatform.Codex => "Restart Codex to pick up new agents.",
        AgentPlatform.Claude => "Restart Claude Code or run /agents to pick up new agents.",
        AgentPlatform.Copilot => "Restart Copilot CLI or your IDE agent session to pick up new agents.",
        AgentPlatform.Gemini => "Run /agents reload or restart Gemini CLI to pick up new agents.",
        AgentPlatform.Junie => "Restart Junie or reload the project to pick up new agents.",
        AgentPlatform.Grok => "Restart Grok Build to pick up new agents.",
        _ => "Restart your agent session to pick up new agents.",
    };

    public string FileExtension => Mode switch
    {
        AgentInstallMode.MarkdownAgentFiles => ".md",
        AgentInstallMode.CopilotAgentFiles => ".agent.md",
        AgentInstallMode.CodexRoleFiles => ".toml",
        _ => ".md",
    };
}

internal static class AgentInstallTarget
{
    public static AgentInstallLayout Resolve(
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

        var configuredRoot = context.ResolveConfiguredRoot(ToolIdentity.AgentsDefaultTargetEnvironmentVariable, scope);
        if (configuredRoot is not null)
        {
            var configuredPlatform = agent == AgentPlatform.Auto ? AgentPlatform.Agents : agent;
            return InstallPlatformRegistry.Get(configuredPlatform)
                .CreateAgentLayout(scope, configuredRoot, isExplicitTarget: false);
        }

        if (agent == AgentPlatform.Auto)
        {
            return ResolveDetected(context, scope)[0];
        }

        var strategy = InstallPlatformRegistry.Get(agent);
        return strategy.CreateAgentLayout(scope, strategy.GetAgentRoot(context, scope), isExplicitTarget: false);
    }

    public static IReadOnlyList<AgentInstallLayout> ResolveAllDetected(string? projectDirectory, InstallScope scope)
    {
        var context = InstallPathContext.Create(projectDirectory);
        var configuredRoot = context.ResolveConfiguredRoot(ToolIdentity.AgentsDefaultTargetEnvironmentVariable, scope);
        if (configuredRoot is not null)
        {
            return
            [
                InstallPlatformRegistry.Get(AgentPlatform.Agents)
                    .CreateAgentLayout(scope, configuredRoot, isExplicitTarget: false),
            ];
        }

        return ResolveDetected(context, scope);
    }

    private static AgentInstallLayout ResolveExplicit(AgentPlatform agent, InstallScope scope, string explicitTargetPath)
    {
        var targetRoot = InstallPathContext.ResolveExplicitRoot(explicitTargetPath);
        var explicitPlatform = agent == AgentPlatform.Auto ? AgentPlatform.Agents : agent;
        return InstallPlatformRegistry.Get(explicitPlatform).CreateAgentLayout(scope, targetRoot, isExplicitTarget: true);
    }

    private static IReadOnlyList<AgentInstallLayout> ResolveDetected(InstallPathContext context, InstallScope scope)
    {
        var layouts = ResolveNativeLayouts(context, scope);
        if (layouts.Count > 0)
        {
            return layouts;
        }

        var sharedStrategy = InstallPlatformRegistry.Get(AgentPlatform.Agents);
        return [sharedStrategy.CreateAgentLayout(scope, sharedStrategy.GetAgentRoot(context, scope), isExplicitTarget: false)];
    }

    private static IReadOnlyList<AgentInstallLayout> ResolveNativeLayouts(InstallPathContext context, InstallScope scope)
    {
        var sharedStrategy = InstallPlatformRegistry.Get(AgentPlatform.Agents);
        if (sharedStrategy.HasNativeRoot(context, scope))
        {
            return [sharedStrategy.CreateAgentLayout(scope, sharedStrategy.GetAgentRoot(context, scope), isExplicitTarget: false)];
        }

        return InstallPlatformRegistry.StrategiesInDetectionOrder
            .Where(strategy => strategy.Platform != AgentPlatform.Agents)
            .Where(strategy => strategy.HasNativeRoot(context, scope))
            .Select(strategy => strategy.CreateAgentLayout(scope, strategy.GetAgentRoot(context, scope), isExplicitTarget: false))
            .DistinctBy(layout => layout.PrimaryRoot.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
