using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace ManagedCode.DotnetSkills.Runtime;

internal interface IPackageVersionSource
{
    Task<string?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken);
}

internal sealed class NuGetPackageVersionClient : IPackageVersionSource
{
    private const string ServiceIndexUrl = "https://api.nuget.org/v3/index.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<string?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        var packageBaseAddress = await ResolvePackageBaseAddressAsync(cancellationToken);
        var versionIndex = await LoadPackageVersionIndexAsync(packageBaseAddress, packageId, cancellationToken);

        return versionIndex.Versions
            .Select(ToolVersionInfo.ParseNuGetVersion)
            .Where(version => !version.IsPrerelease)
            .OrderByDescending(version => version, VersionComparer.VersionRelease)
            .Select(version => version.ToNormalizedString())
            .FirstOrDefault();
    }

    private static async Task<string> ResolvePackageBaseAddressAsync(CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(ServiceIndexUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var serviceIndex = await JsonSerializer.DeserializeAsync<NuGetServiceIndexDocument>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Could not parse the NuGet service index.");

        var packageBaseAddress = serviceIndex.Resources
            .FirstOrDefault(resource => string.Equals(resource.Type, "PackageBaseAddress/3.0.0", StringComparison.OrdinalIgnoreCase))
            ?.Id;

        return !string.IsNullOrWhiteSpace(packageBaseAddress)
            ? packageBaseAddress
            : throw new InvalidOperationException("NuGet service index does not expose PackageBaseAddress/3.0.0.");
    }

    private static async Task<NuGetPackageVersionIndex> LoadPackageVersionIndexAsync(string packageBaseAddress, string packageId, CancellationToken cancellationToken)
    {
        var normalizedPackageId = packageId.ToLowerInvariant();
        var requestUri = $"{packageBaseAddress.TrimEnd('/')}/{normalizedPackageId}/index.json";

        using var response = await HttpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<NuGetPackageVersionIndex>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Could not parse the NuGet package version index for {packageId}.");
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ToolVersionInfo.PackageId, "1.0.0"));
        return httpClient;
    }
}

internal sealed class NuGetServiceIndexDocument
{
    [JsonPropertyName("resources")]
    public List<NuGetServiceResource> Resources { get; init; } = [];
}

internal sealed class NuGetServiceResource
{
    [JsonPropertyName("@id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("@type")]
    public string Type { get; init; } = string.Empty;
}

internal sealed class NuGetPackageVersionIndex
{
    [JsonPropertyName("versions")]
    public List<string> Versions { get; init; } = [];
}
