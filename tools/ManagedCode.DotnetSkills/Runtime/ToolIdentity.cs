using System.Reflection;

namespace ManagedCode.DotnetSkills.Runtime;

internal enum ToolSurface
{
    Skills,
    Agents,
}

internal static class ToolIdentity
{
    private const string MetadataPrefix = "ManagedCode.DotnetSkills.";

    private static readonly IReadOnlyDictionary<string, string> Metadata = typeof(ToolIdentity)
        .Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Where(attribute => attribute.Key.StartsWith(MetadataPrefix, StringComparison.Ordinal))
        .ToDictionary(
            attribute => attribute.Key[MetadataPrefix.Length..],
            attribute => attribute.Value ?? string.Empty,
            StringComparer.Ordinal);

    public static ToolSurface Surface { get; } = ParseSurface(GetMetadata("Surface", "skills"));

    public static bool IsAgentFirstTool => Surface == ToolSurface.Agents;

    public static string PackageId { get; } = GetMetadata("PackageId", "dotnet-skills");

    public static string ToolCommandName { get; } = GetMetadata("ToolCommandName", "dotnet-skills");

    public static string DisplayCommand { get; } = GetMetadata("DisplayCommand", ResolveDisplayCommand(ToolCommandName));

    public static string SkillsDisplayCommand => "dotnet skills";

    public static string AgentDisplayCommand => IsAgentFirstTool
        ? DisplayCommand
        : $"{DisplayCommand} agent";

    public static string SkipUpdateEnvironmentVariable { get; } = GetMetadata("SkipUpdateEnvironmentVariable", "DOTNET_SKILLS_SKIP_UPDATE_CHECK");

    public static string CacheDirectoryName { get; } = GetMetadata("CacheDirectoryName", PackageId);

    private static string GetMetadata(string key, string fallback)
    {
        return Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static ToolSurface ParseSurface(string value) => value.ToLowerInvariant() switch
    {
        "agents" => ToolSurface.Agents,
        _ => ToolSurface.Skills,
    };

    private static string ResolveDisplayCommand(string toolCommandName)
    {
        return toolCommandName.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase)
            ? $"dotnet {toolCommandName["dotnet-".Length..]}"
            : toolCommandName;
    }
}
