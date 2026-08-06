namespace ManagedCode.DotnetSkills.Runtime;

internal static class InstallPlatformRegistry
{
    private static readonly IInstallPlatformStrategy[] Strategies =
    [
        new AgentsInstallPlatformStrategy(),
        new CodexInstallPlatformStrategy(),
        new ClaudeInstallPlatformStrategy(),
        new CopilotInstallPlatformStrategy(),
        new GeminiInstallPlatformStrategy(),
        new JunieInstallPlatformStrategy(),
        new GrokInstallPlatformStrategy(),
    ];

    public static IReadOnlyList<IInstallPlatformStrategy> StrategiesInDetectionOrder => Strategies;

    public static IInstallPlatformStrategy Get(AgentPlatform platform)
    {
        return Strategies.FirstOrDefault(strategy => strategy.Platform == platform)
            ?? throw new InvalidOperationException($"Unsupported agent: {platform}");
    }
}
