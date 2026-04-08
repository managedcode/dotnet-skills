namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class GeminiInstallPlatformStrategy : InstallPlatformStrategyBase
{
    public override AgentPlatform Platform => AgentPlatform.Gemini;

    protected override DirectoryInfo GetNativeRoot(InstallPathContext context, InstallScope scope)
    {
        return scope == InstallScope.Project
            ? new DirectoryInfo(Path.Combine(context.ProjectRoot.FullName, ".gemini"))
            : new DirectoryInfo(Path.Combine(context.UserHome.FullName, ".gemini"));
    }
}
