namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class GrokInstallPlatformStrategy : InstallPlatformStrategyBase
{
    public override AgentPlatform Platform => AgentPlatform.Grok;

    protected override DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".grok"))
            : new DirectoryInfo(Path.Combine(context.UserHome.FullName, ".grok"));
    }
}
