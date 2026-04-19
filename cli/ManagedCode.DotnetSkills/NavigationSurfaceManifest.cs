using System.Reflection;
using System.Text.Json;

namespace ManagedCode.DotnetSkills;

internal static class NavigationSurfaceManifest
{
    private const string ResourceName = "ManagedCode.DotnetSkills.Config.navigation-surfaces.json";
    private static readonly Lazy<NavigationSurfaceModel> LazyCurrent = new(Load);

    public static NavigationSurfaceModel Current => LazyCurrent.Value;

    private static NavigationSurfaceModel Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded navigation manifest '{ResourceName}' was not found.");
        }

        var manifest = JsonSerializer.Deserialize<NavigationSurfaceModel>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (manifest is null)
        {
            throw new InvalidOperationException("The embedded navigation manifest could not be parsed.");
        }

        manifest.Initialize();
        return manifest;
    }
}

internal sealed class NavigationSurfaceModel
{
    private Dictionary<string, NavigationSurfaceDefinition>? surfacesById;

    public List<string> SitePrimary { get; init; } = [];

    public List<string> SiteFooterBrowse { get; init; } = [];

    public List<NavigationSectionDefinition> CliSections { get; init; } = [];

    public List<NavigationSurfaceDefinition> Surfaces { get; init; } = [];

    public void Initialize()
    {
        surfacesById = Surfaces.ToDictionary(surface => surface.Id, StringComparer.OrdinalIgnoreCase);
    }

    public NavigationSurfaceDefinition GetSurface(string id)
    {
        if (surfacesById is null)
        {
            Initialize();
        }

        if (surfacesById is null || !surfacesById.TryGetValue(id, out var surface))
        {
            throw new InvalidOperationException($"Navigation surface '{id}' was not found in the shared manifest.");
        }

        return surface;
    }
}

internal sealed class NavigationSectionDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public List<string> Items { get; init; } = [];
}

internal sealed class NavigationSurfaceDefinition
{
    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string? Href { get; init; }

    public CliNavigationSurfaceDefinition? Cli { get; init; }
}

internal sealed class CliNavigationSurfaceDefinition
{
    public string HotKey { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string Accent { get; init; } = string.Empty;
}
