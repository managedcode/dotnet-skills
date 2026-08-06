namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class AgentsInstallPlatformStrategy : InstallPlatformStrategyBase
{
    public override AgentPlatform Platform => AgentPlatform.Agents;

    protected override DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".agents"))
            : new DirectoryInfo(Path.Combine(context.UserHome.FullName, ".agents"));
    }
}
