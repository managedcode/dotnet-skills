namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class ClaudeInstallPlatformStrategy : InstallPlatformStrategyBase
{
    public override AgentPlatform Platform => AgentPlatform.Claude;

    protected override DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".claude"))
            : new DirectoryInfo(Path.Combine(context.UserHome.FullName, ".claude"));
    }
}
