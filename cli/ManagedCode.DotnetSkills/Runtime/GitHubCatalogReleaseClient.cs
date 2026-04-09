using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class GitHubCatalogReleaseClient
{
    private const string Owner = "managedcode";
    private const string Repository = "dotnet-skills";
    private const string CatalogTagPrefix = "catalog-v";
    private const string CatalogManifestAssetName = "dotnet-skills-manifest.json";
    private const string CatalogPayloadAssetName = "dotnet-skills-catalog.zip";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private readonly DirectoryInfo cacheRoot;
    private readonly HttpClient httpClient;

    public GitHubCatalogReleaseClient(DirectoryInfo cacheRoot, HttpClient? httpClient = null)
    {
        this.cacheRoot = cacheRoot;
        this.httpClient = httpClient ?? SharedHttpClient;
    }

    public DirectoryInfo ResolveCacheRoot() => cacheRoot;

    public static DirectoryInfo ResolveDefaultCacheDirectory()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            return new DirectoryInfo(Path.Combine(codexHome, "cache", ToolIdentity.CacheDirectoryName));
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new DirectoryInfo(Path.Combine(userHome, ".codex", "cache", ToolIdentity.CacheDirectoryName));
    }

    public async Task<SkillManifest> LoadManifestAsync(string? catalogVersion, CancellationToken cancellationToken)
    {
        var release = await ResolveReleaseAsync(catalogVersion, cancellationToken);
        var asset = release.GetAsset(CatalogManifestAssetName);

        await using var stream = await DownloadAssetAsync(asset.DownloadUrl, cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<SkillManifest>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Could not parse {CatalogManifestAssetName} from {release.TagName}");

        return manifest;
    }

    public async Task<SkillCatalogPackage> SyncAsync(string? catalogVersion, bool force, CancellationToken cancellationToken)
    {
        var release = await ResolveReleaseAsync(catalogVersion, cancellationToken);
        var releaseDirectory = new DirectoryInfo(Path.Combine(cacheRoot.FullName, release.TagName));
        var cachedCatalogRoot = new DirectoryInfo(Path.Combine(releaseDirectory.FullName, "catalog"));

        if (!force && cachedCatalogRoot.Exists)
        {
            return SkillCatalogPackage.LoadFromDirectory(releaseDirectory, $"GitHub release {release.TagName}", release.Version);
        }

        cacheRoot.Create();

        var tempDirectory = new DirectoryInfo(Path.Combine(cacheRoot.FullName, $".tmp-{Guid.NewGuid():N}"));
        tempDirectory.Create();

        try
        {
            var archivePath = Path.Combine(tempDirectory.FullName, CatalogPayloadAssetName);
            var asset = release.GetAsset(CatalogPayloadAssetName);

            await using (var responseStream = await DownloadAssetAsync(asset.DownloadUrl, cancellationToken))
            await using (var targetStream = File.Create(archivePath))
            {
                await responseStream.CopyToAsync(targetStream, cancellationToken);
            }

            ZipFile.ExtractToDirectory(archivePath, tempDirectory.FullName, overwriteFiles: true);

            if (releaseDirectory.Exists)
            {
                releaseDirectory.Delete(recursive: true);
            }

            var extractedCatalogDirectory = ResolveExtractedCatalogDirectory(tempDirectory);

            releaseDirectory.Create();
            SkillInstaller.CopyDirectory(extractedCatalogDirectory, new DirectoryInfo(Path.Combine(releaseDirectory.FullName, "catalog")));

            return SkillCatalogPackage.LoadFromDirectory(releaseDirectory, $"GitHub release {release.TagName}", release.Version);
        }
        finally
        {
            if (tempDirectory.Exists)
            {
                tempDirectory.Delete(recursive: true);
            }
        }
    }

    private async Task<GitHubRelease> ResolveReleaseAsync(string? catalogVersion, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(catalogVersion))
        {
            var versionedTag = $"{CatalogTagPrefix}{catalogVersion}";
            var release = await GetReleaseByTagAsync(versionedTag, cancellationToken);
            return release;
        }

        var releases = await GetReleasesAsync(cancellationToken);
        return ResolveLatestCatalogRelease(releases);
    }

    internal static GitHubRelease ResolveLatestCatalogRelease(IReadOnlyList<GitHubRelease> releases)
    {
        var candidates = releases
            .Where(release => !release.Draft && !release.Prerelease && release.TagName.StartsWith(CatalogTagPrefix, StringComparison.Ordinal))
            .Select(release => new
            {
                Release = release,
                Version = TryParseCatalogVersion(release.TagName),
            })
            .ToArray();

        var latestParsed = candidates
            .Where(candidate => candidate.Version is not null)
            .OrderByDescending(candidate => candidate.Version!, VersionComparer.VersionRelease)
            .Select(candidate => candidate.Release)
            .FirstOrDefault();

        return latestParsed
            ?? candidates.FirstOrDefault()?.Release
            ?? throw new InvalidOperationException($"No GitHub catalog release with tag prefix {CatalogTagPrefix} was found in {Owner}/{Repository}.");
    }

    internal static DirectoryInfo ResolveExtractedCatalogDirectory(DirectoryInfo extractionRoot)
    {
        var directCatalog = new DirectoryInfo(Path.Combine(extractionRoot.FullName, "catalog"));
        if (directCatalog.Exists)
        {
            return directCatalog;
        }

        if (LooksLikeCatalogRoot(extractionRoot))
        {
            return extractionRoot;
        }

        var nestedCatalogDirectories = extractionRoot.EnumerateDirectories()
            .Select(directory => new DirectoryInfo(Path.Combine(directory.FullName, "catalog")))
            .Where(directory => directory.Exists)
            .ToArray();
        if (nestedCatalogDirectories.Length == 1)
        {
            return nestedCatalogDirectories[0];
        }

        var nestedCatalogRoots = extractionRoot.EnumerateDirectories()
            .Where(LooksLikeCatalogRoot)
            .ToArray();
        if (nestedCatalogRoots.Length == 1)
        {
            return nestedCatalogRoots[0];
        }

        throw new InvalidOperationException(
            $"Release asset {CatalogPayloadAssetName} is missing a recognizable catalog payload. Expected catalog/ at the archive root or a single wrapped catalog directory.");
    }

    private async Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"https://api.github.com/repos/{Owner}/{Repository}/releases?per_page=50", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task<GitHubRelease> GetReleaseByTagAsync(string tag, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"https://api.github.com/repos/{Owner}/{Repository}/releases/tags/{tag}", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Could not parse GitHub release metadata for tag {tag}.");
    }

    private async Task<Stream> DownloadAssetAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var memoryStream = new MemoryStream();
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await responseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ToolIdentity.PackageId, "0.0.1"));
        return httpClient;
    }

    private static NuGetVersion? TryParseCatalogVersion(string tagName)
    {
        if (!tagName.StartsWith(CatalogTagPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var versionText = tagName[CatalogTagPrefix.Length..];
        return NuGetVersion.TryParse(versionText, out var parsed) ? parsed : null;
    }

    private static bool LooksLikeCatalogRoot(DirectoryInfo directory)
    {
        if (File.Exists(Path.Combine(directory.FullName, "skills.json")))
        {
            return true;
        }

        return directory.EnumerateDirectories()
            .Any(typeDirectory => typeDirectory.EnumerateDirectories()
                .Any(packageDirectory => File.Exists(Path.Combine(packageDirectory.FullName, "manifest.json"))));
    }
}

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset> Assets { get; init; } = [];

    public string Version => TagName.StartsWith("catalog-v", StringComparison.Ordinal)
        ? TagName["catalog-v".Length..]
        : TagName;

    public GitHubReleaseAsset GetAsset(string assetName)
    {
        var asset = Assets.FirstOrDefault(candidate => string.Equals(candidate.Name, assetName, StringComparison.Ordinal));
        return asset
            ?? throw new InvalidOperationException($"GitHub release {TagName} does not contain the required asset {assetName}.");
    }
}

internal sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; init; } = string.Empty;
}
