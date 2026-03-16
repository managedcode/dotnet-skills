using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class ToolUpdateServiceTests
{
    [Fact]
    public void Evaluate_ReturnsUpdateAvailable_WhenLatestIsHigher()
    {
        var status = ToolUpdateService.Evaluate(
            currentVersion: "0.0.412",
            latestVersion: "0.0.500",
            checkedAt: DateTimeOffset.Parse("2026-03-16T09:30:00+00:00"),
            usedCachedValue: false,
            includeDevelopmentBuilds: true);

        Assert.Equal(ToolUpdateState.UpdateAvailable, status.State);
        Assert.True(status.HasUpdate);
        Assert.Equal("0.0.500", status.LatestVersion);
    }

    [Fact]
    public void Evaluate_ReturnsCurrent_WhenVersionsMatch()
    {
        var status = ToolUpdateService.Evaluate(
            currentVersion: "0.0.412",
            latestVersion: "0.0.412",
            checkedAt: null,
            usedCachedValue: false,
            includeDevelopmentBuilds: true);

        Assert.Equal(ToolUpdateState.Current, status.State);
        Assert.False(status.HasUpdate);
    }

    [Fact]
    public void Evaluate_ReturnsUnknown_WhenLatestVersionCannotBeParsed()
    {
        var status = ToolUpdateService.Evaluate(
            currentVersion: "0.0.412",
            latestVersion: "not-a-version",
            checkedAt: null,
            usedCachedValue: false,
            includeDevelopmentBuilds: true);

        Assert.Equal(ToolUpdateState.Unknown, status.State);
        Assert.False(status.HasUpdate);
    }

    [Fact]
    public void Evaluate_SuppressesAutomaticNotice_ForDevelopmentBuilds()
    {
        var status = ToolUpdateService.Evaluate(
            currentVersion: "0.0.0",
            latestVersion: "0.0.500",
            checkedAt: null,
            usedCachedValue: false,
            includeDevelopmentBuilds: false);

        Assert.Equal(ToolUpdateState.DevelopmentBuild, status.State);
        Assert.False(status.HasUpdate);
    }

    [Fact]
    public async Task GetStatusAsync_UsesCacheWithinLifetime()
    {
        using var tempDirectory = new TemporaryDirectory();
        var source = new FakePackageVersionSource("0.0.500");
        var currentTime = DateTimeOffset.Parse("2026-03-16T10:00:00+00:00");
        var service = new ToolUpdateService(source, () => currentTime);
        var cacheRoot = new DirectoryInfo(tempDirectory.Path);

        var first = await service.GetStatusAsync(cacheRoot, includeDevelopmentBuilds: true, CancellationToken.None);
        var second = await service.GetStatusAsync(cacheRoot, includeDevelopmentBuilds: true, CancellationToken.None);

        Assert.Equal(1, source.CallCount);
        Assert.NotNull(first.CheckedAt);
        Assert.True(second.UsedCachedValue);
    }

    private sealed class FakePackageVersionSource(string latestVersion) : IPackageVersionSource
    {
        public int CallCount { get; private set; }

        public Task<string?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<string?>(latestVersion);
        }
    }
}
