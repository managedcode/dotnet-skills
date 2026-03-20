namespace ManagedCode.DotnetSkills.Runtime;

internal enum AgentPlatform
{
    Auto,
    Codex,
    Claude,
    Copilot,
    Gemini,
    Junie,
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
        AgentPlatform.Codex => "Restart Codex to pick up new skills.",
        AgentPlatform.Claude => "Restart Claude Code or start a new session to pick up new skills.",
        AgentPlatform.Copilot => "Restart Copilot CLI or your IDE agent session to pick up new skills.",
        AgentPlatform.Gemini => "Run /skills reload or restart Gemini CLI to pick up new skills.",
        AgentPlatform.Junie => "Restart Junie or reload the project to pick up new skills.",
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
        _ => throw new InvalidOperationException("Unsupported agent: " + value + ". Expected auto, codex, openai, claude, anthropic, copilot, github-copilot, gemini, google-gemini, junie, or jetbrains."),
    };

    public static InstallScope ParseScope(string value) => value.ToLowerInvariant() switch
    {
        "global" => InstallScope.Global,
        "project" => InstallScope.Project,
        _ => throw new InvalidOperationException($"Unsupported scope: {value}. Expected global or project."),
    };

    public static IReadOnlyList<SkillInstallLayout> ResolveAllDetected(string? projectDirectory, InstallScope scope)
    {
        return ResolveDetected(InstallPathContext.Create(projectDirectory), scope);
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
        return InstallPlatformRegistry.StrategiesInDetectionOrder
            .Where(strategy => strategy.HasNativeRoot(context, scope))
            .Select(strategy => strategy.CreateSkillLayout(scope, strategy.GetSkillRoot(context, scope), isExplicitTarget: false))
            .DistinctBy(layout => layout.PrimaryRoot.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SkillInstallLayout CreateDefaultFallbackLayout(InstallPathContext context, InstallScope scope)
    {
        var root = scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".agents", "skills"))
            : new DirectoryInfo(Path.Combine(context.UserHome.FullName, ".agents", "skills"));

        return new SkillInstallLayout(
            AgentPlatform.Auto,
            scope,
            SkillInstallMode.SkillDirectories,
            root,
            IsExplicitTarget: false);
    }
}
