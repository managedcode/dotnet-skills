using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills.Tests;

public sealed class GitHubCatalogReleaseClientTests
{
    [Fact]
    public void ResolveLatestCatalogRelease_PrefersHighestSemanticVersion()
    {
        var latest = GitHubCatalogReleaseClient.ResolveLatestCatalogRelease(
        [
            new GitHubRelease
            {
                TagName = "catalog-v2026.4.8.1",
            },
            new GitHubRelease
            {
                TagName = "catalog-v2026.4.10.0",
            },
            new GitHubRelease
            {
                TagName = "catalog-v2026.4.9.0",
                Prerelease = true,
            },
        ]);

        Assert.Equal("catalog-v2026.4.10.0", latest.TagName);
    }

    [Fact]
    public void ResolveExtractedCatalogDirectory_FindsWrappedCatalogFolder()
    {
        using var tempDirectory = new TemporaryDirectory();
        var wrapperDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.Path, "wrapped-release", "catalog"));

        var resolved = GitHubCatalogReleaseClient.ResolveExtractedCatalogDirectory(new DirectoryInfo(tempDirectory.Path));

        Assert.Equal(wrapperDirectory.FullName, resolved.FullName);
    }

    [Fact]
    public async Task SyncAsync_UsesHighestCatalogReleaseEvenWhenApiOrderDiffers()
    {
        using var tempDirectory = new TemporaryDirectory();

        var repositoryRoot = ResolveRepositoryRoot();
        var oldArchive = CreateRepositoryCatalogArchive(repositoryRoot, "catalog");
        var latestArchive = CreateRepositoryCatalogArchive(repositoryRoot, "wrapped-release/catalog");

        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;

            return url switch
            {
                "https://api.github.com/repos/managedcode/dotnet-skills/releases?per_page=50" => JsonResponse(
                    """
                    [
                      {
                        "tag_name": "catalog-v2026.4.8.1",
                        "draft": false,
                        "prerelease": false,
                        "assets": [
                          {
                            "name": "dotnet-skills-catalog.zip",
                            "browser_download_url": "https://example.test/catalog-v2026.4.8.1.zip"
                          }
                        ]
                      },
                      {
                        "tag_name": "catalog-v2026.4.10.0",
                        "draft": false,
                        "prerelease": false,
                        "assets": [
                          {
                            "name": "dotnet-skills-catalog.zip",
                            "browser_download_url": "https://example.test/catalog-v2026.4.10.0.zip"
                          }
                        ]
                      }
                    ]
                    """),
                "https://example.test/catalog-v2026.4.8.1.zip" => ZipResponse(oldArchive),
                "https://example.test/catalog-v2026.4.10.0.zip" => ZipResponse(latestArchive),
                _ => throw new InvalidOperationException($"Unexpected request: {url}"),
            };
        }));

        var client = new GitHubCatalogReleaseClient(new DirectoryInfo(tempDirectory.Path), httpClient);

        var catalog = await client.SyncAsync(catalogVersion: null, force: true, CancellationToken.None);

        Assert.Equal("2026.4.10.0", catalog.CatalogVersion);
        Assert.Contains(catalog.Skills, skill => skill.Name == "dotnet-aspire");
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage ZipResponse(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        return response;
    }

    private static byte[] CreateRepositoryCatalogArchive(DirectoryInfo repositoryRoot, string catalogRootPath)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var catalogDirectory = new DirectoryInfo(Path.Combine(repositoryRoot.FullName, "catalog"));
            foreach (var file in catalogDirectory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(catalogDirectory.FullName, file.FullName)
                    .Replace(Path.DirectorySeparatorChar, '/');
                AddFileEntry(archive, $"{catalogRootPath}/{relativePath}", file.FullName);
            }
        }

        return stream.ToArray();
    }

    private static void AddFileEntry(ZipArchive archive, string path, string sourcePath)
    {
        var entry = archive.CreateEntry(path);
        using var sourceStream = File.OpenRead(sourcePath);
        using var entryStream = entry.Open();
        sourceStream.CopyTo(entryStream);
    }

    private static DirectoryInfo ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dotnet-skills.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
