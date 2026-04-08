namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class CopilotInstallPlatformStrategy : InstallPlatformStrategyBase
{
    public override AgentPlatform Platform => AgentPlatform.Copilot;

    protected override AgentInstallMode AgentMode => AgentInstallMode.CopilotAgentFiles;

    protected override DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".github"))
            : new DirectoryInfo(Path.Combine(context.UserHome.FullName, ".copilot"));
    }
}
