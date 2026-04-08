namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class CodexInstallPlatformStrategy : InstallPlatformStrategyBase
{
    public override AgentPlatform Platform => AgentPlatform.Codex;

    protected override AgentInstallMode AgentMode => AgentInstallMode.CodexRoleFiles;

    protected override DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".codex"))
            : context.CodexHome;
    }
}
