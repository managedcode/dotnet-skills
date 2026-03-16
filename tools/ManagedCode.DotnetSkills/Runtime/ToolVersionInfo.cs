using System.Reflection;
using NuGet.Versioning;

namespace ManagedCode.DotnetSkills.Runtime;

internal static class ToolVersionInfo
{
    public const string PackageId = "dotnet-skills";

    public static string CurrentVersion { get; } = ResolveCurrentVersion();

    public static NuGetVersion CurrentNuGetVersion { get; } = ParseNuGetVersion(CurrentVersion);

    public static bool IsDevelopmentBuild =>
        CurrentNuGetVersion.Major == 0
        && CurrentNuGetVersion.Minor == 0
        && CurrentNuGetVersion.Patch == 0;

    public static NuGetVersion ParseNuGetVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return new NuGetVersion(0, 0, 0);
        }

        var normalized = version.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        return NuGetVersion.TryParse(normalized, out var parsed)
            ? parsed
            : new NuGetVersion(0, 0, 0);
    }

    private static string ResolveCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
