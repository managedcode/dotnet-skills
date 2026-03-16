using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class ToolUpdateService(IPackageVersionSource versionSource, Func<DateTimeOffset>? clock = null)
{
    private const string CacheFileName = "tool-version-check.json";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);

    public async Task<ToolUpdateStatusInfo> GetStatusAsync(DirectoryInfo cacheRoot, bool includeDevelopmentBuilds, CancellationToken cancellationToken)
    {
        if (!includeDevelopmentBuilds && ToolVersionInfo.IsDevelopmentBuild)
        {
            return Evaluate(ToolVersionInfo.CurrentVersion, latestVersion: null, checkedAt: null, usedCachedValue: false, includeDevelopmentBuilds: false);
        }

        var cacheFile = new FileInfo(Path.Combine(cacheRoot.FullName, CacheFileName));
        var cached = await TryReadCacheAsync(cacheFile, cancellationToken);
        if (cached is not null && now() - cached.CheckedAt <= CacheLifetime)
        {
            return Evaluate(ToolVersionInfo.CurrentVersion, cached.LatestVersion, cached.CheckedAt, usedCachedValue: true, includeDevelopmentBuilds);
        }

        try
        {
            var latestVersion = await versionSource.GetLatestStableVersionAsync(ToolVersionInfo.PackageId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(latestVersion))
            {
                cacheRoot.Create();
                await File.WriteAllTextAsync(
                    cacheFile.FullName,
                    JsonSerializer.Serialize(new ToolUpdateCacheDocument(latestVersion, now()), JsonOptions),
                    cancellationToken);
            }

            return Evaluate(ToolVersionInfo.CurrentVersion, latestVersion, now(), usedCachedValue: false, includeDevelopmentBuilds);
        }
        catch when (cached is not null)
        {
            return Evaluate(ToolVersionInfo.CurrentVersion, cached.LatestVersion, cached.CheckedAt, usedCachedValue: true, includeDevelopmentBuilds);
        }
        catch
        {
            return Evaluate(ToolVersionInfo.CurrentVersion, latestVersion: null, checkedAt: null, usedCachedValue: false, includeDevelopmentBuilds);
        }
    }

    public static bool ShouldSkipAutomaticCheck()
    {
        var value = Environment.GetEnvironmentVariable("DOTNET_SKILLS_SKIP_UPDATE_CHECK");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static ToolUpdateStatusInfo Evaluate(
        string currentVersion,
        string? latestVersion,
        DateTimeOffset? checkedAt,
        bool usedCachedValue,
        bool includeDevelopmentBuilds)
    {
        var current = ToolVersionInfo.ParseNuGetVersion(currentVersion);

        if (!includeDevelopmentBuilds && current.Major == 0 && current.Minor == 0 && current.Patch == 0)
        {
            return new ToolUpdateStatusInfo(
                currentVersion,
                latestVersion,
                ToolUpdateState.DevelopmentBuild,
                checkedAt,
                usedCachedValue);
        }

        if (string.IsNullOrWhiteSpace(latestVersion))
        {
            return new ToolUpdateStatusInfo(
                currentVersion,
                latestVersion,
                ToolUpdateState.Unknown,
                checkedAt,
                usedCachedValue);
        }

        var normalizedLatestVersion = latestVersion.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        if (!NuGetVersion.TryParse(normalizedLatestVersion, out var latest))
        {
            return new ToolUpdateStatusInfo(
                currentVersion,
                latestVersion,
                ToolUpdateState.Unknown,
                checkedAt,
                usedCachedValue);
        }

        var state = latest > current
            ? ToolUpdateState.UpdateAvailable
            : ToolUpdateState.Current;

        return new ToolUpdateStatusInfo(
            currentVersion,
            latest.ToNormalizedString(),
            state,
            checkedAt,
            usedCachedValue);
    }

    private static async Task<ToolUpdateCacheDocument?> TryReadCacheAsync(FileInfo cacheFile, CancellationToken cancellationToken)
    {
        if (!cacheFile.Exists)
        {
            return null;
        }

        await using var stream = cacheFile.OpenRead();
        return await JsonSerializer.DeserializeAsync<ToolUpdateCacheDocument>(stream, JsonOptions, cancellationToken);
    }
}

internal sealed record ToolUpdateStatusInfo(
    string CurrentVersion,
    string? LatestVersion,
    ToolUpdateState State,
    DateTimeOffset? CheckedAt,
    bool UsedCachedValue)
{
    public bool HasUpdate => State == ToolUpdateState.UpdateAvailable;
}

internal enum ToolUpdateState
{
    Unknown,
    Current,
    UpdateAvailable,
    DevelopmentBuild,
}

internal sealed record ToolUpdateCacheDocument(
    [property: JsonPropertyName("latestVersion")] string LatestVersion,
    [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt);
