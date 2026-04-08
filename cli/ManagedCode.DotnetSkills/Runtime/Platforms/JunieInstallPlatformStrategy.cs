namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class JunieInstallPlatformStrategy : InstallPlatformStrategyBase
{
    public override AgentPlatform Platform => AgentPlatform.Junie;

    protected override DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".junie"))
            : new DirectoryInfo(Path.Combine(context.UserHome.FullName, ".junie"));
    }
}
