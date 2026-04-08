namespace ManagedCode.DotnetSkills.Runtime;

internal interface IInstallPlatformStrategy
{
    AgentPlatform Platform { get; }

    bool HasNativeRoot(InstallPathContext context, InstallScope scope);

    DirectoryInfo GetSkillRoot(InstallPathContext context, InstallScope scope);

    DirectoryInfo GetAgentRoot(InstallPathContext context, InstallScope scope);

    SkillInstallLayout CreateSkillLayout(InstallScope scope, DirectoryInfo targetRoot, bool isExplicitTarget);

    AgentInstallLayout CreateAgentLayout(InstallScope scope, DirectoryInfo targetRoot, bool isExplicitTarget);
}

internal abstract class InstallPlatformStrategyBase : IInstallPlatformStrategy
{
    public abstract AgentPlatform Platform { get; }

    protected virtual AgentInstallMode AgentMode => AgentInstallMode.MarkdownAgentFiles;

    public bool HasNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return Directory.Exists(GetNativeRoot(context, scope).FullName);
    }

    public virtual DirectoryInfo GetSkillRoot(InstallPathContext context, InstallScope scope)
    {
        return new DirectoryInfo(Path.Combine(GetNativeRoot(context, scope).FullName, "skills"));
    }

    public virtual DirectoryInfo GetAgentRoot(InstallPathContext context, InstallScope scope)
    {
        return new DirectoryInfo(Path.Combine(GetNativeRoot(context, scope).FullName, "agents"));
    }

    public SkillInstallLayout CreateSkillLayout(InstallScope scope, DirectoryInfo targetRoot, bool isExplicitTarget)
    {
        return new SkillInstallLayout(Platform, scope, SkillInstallMode.SkillDirectories, targetRoot, isExplicitTarget);
    }

    public AgentInstallLayout CreateAgentLayout(InstallScope scope, DirectoryInfo targetRoot, bool isExplicitTarget)
    {
        return new AgentInstallLayout(Platform, scope, AgentMode, targetRoot, isExplicitTarget);
    }

    protected abstract DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope);
}
