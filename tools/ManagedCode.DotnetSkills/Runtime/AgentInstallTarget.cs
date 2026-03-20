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
        AgentPlatform.Codex => "Restart Codex to pick up new agents.",
        AgentPlatform.Claude => "Restart Claude Code or run /agents to pick up new agents.",
        AgentPlatform.Copilot => "Restart Copilot CLI or your IDE agent session to pick up new agents.",
        AgentPlatform.Gemini => "Run /agents reload or restart Gemini CLI to pick up new agents.",
        AgentPlatform.Junie => "Restart Junie or reload the project to pick up new agents.",
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
    private const string MissingNativeTargetMessage = "No native agent platform detected for {0} scope. Create a native agent directory first or specify --agent/--target.";
    private const string ExplicitTargetRequiresAgentMessage = "Explicit agent targets require --agent because the installed file format depends on the target platform.";

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

        if (agent == AgentPlatform.Auto)
        {
            return ResolveDetected(context, scope).FirstOrDefault()
                ?? throw new InvalidOperationException(string.Format(MissingNativeTargetMessage, scope.ToString().ToLowerInvariant()));
        }

        var strategy = InstallPlatformRegistry.Get(agent);
        return strategy.CreateAgentLayout(scope, strategy.GetAgentRoot(context, scope), isExplicitTarget: false);
    }

    public static IReadOnlyList<AgentInstallLayout> ResolveAllDetected(string? projectDirectory, InstallScope scope)
    {
        return ResolveDetected(InstallPathContext.Create(projectDirectory), scope);
    }

    private static AgentInstallLayout ResolveExplicit(AgentPlatform agent, InstallScope scope, string explicitTargetPath)
    {
        if (agent == AgentPlatform.Auto)
        {
            throw new InvalidOperationException(ExplicitTargetRequiresAgentMessage);
        }

        var targetRoot = InstallPathContext.ResolveExplicitRoot(explicitTargetPath);
        return InstallPlatformRegistry.Get(agent).CreateAgentLayout(scope, targetRoot, isExplicitTarget: true);
    }

    private static IReadOnlyList<AgentInstallLayout> ResolveDetected(InstallPathContext context, InstallScope scope)
    {
        return ResolveNativeLayouts(context, scope);
    }

    private static IReadOnlyList<AgentInstallLayout> ResolveNativeLayouts(InstallPathContext context, InstallScope scope)
    {
        return InstallPlatformRegistry.StrategiesInDetectionOrder
            .Where(strategy => strategy.HasNativeRoot(context, scope))
            .Select(strategy => strategy.CreateAgentLayout(scope, strategy.GetAgentRoot(context, scope), isExplicitTarget: false))
            .DistinctBy(layout => layout.PrimaryRoot.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
