using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class SkillCatalogPackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private SkillCatalogPackage(
        DirectoryInfo catalogRoot,
        IReadOnlyList<SkillEntry> skills,
        IReadOnlyList<SkillPackageEntry> packages,
        string sourceLabel,
        string catalogVersion)
    {
        CatalogRoot = catalogRoot;
        Skills = skills;
        Packages = packages;
        SourceLabel = sourceLabel;
        CatalogVersion = catalogVersion;
    }

    public DirectoryInfo CatalogRoot { get; }

    public IReadOnlyList<SkillEntry> Skills { get; }

    public IReadOnlyList<SkillPackageEntry> Packages { get; }

    public string SourceLabel { get; }

    public string CatalogVersion { get; }

    public static SkillCatalogPackage LoadBundled()
    {
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        return LoadFromDirectory(baseDirectory, "bundled tool payload", "bundled");
    }

    public static SkillCatalogPackage LoadFromDirectory(DirectoryInfo rootDirectory, string sourceLabel, string catalogVersion)
    {
        var scan = CatalogScanner.ScanSkills(rootDirectory);
        return new SkillCatalogPackage(rootDirectory, scan.Skills, scan.Packages, sourceLabel, catalogVersion);
    }

    public static SkillCatalogPackage LoadFromManifest(DirectoryInfo catalogRoot, SkillManifest manifest, string sourceLabel, string catalogVersion)
    {
        return new SkillCatalogPackage(catalogRoot, manifest.Skills, manifest.Packages, sourceLabel, catalogVersion);
    }

    public DirectoryInfo ResolveSkillSource(string skillName)
    {
        var skill = Skills.FirstOrDefault(candidate => string.Equals(candidate.Name, skillName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Skill metadata is missing for {skillName} in {SourceLabel}");
        var directory = new DirectoryInfo(Path.Combine(CatalogRoot.FullName, skill.Path.Replace('/', Path.DirectorySeparatorChar)));
        if (!directory.Exists)
        {
            throw new InvalidOperationException($"Skill payload is missing for {skillName} in {SourceLabel}");
        }

        return directory;
    }
}

internal sealed class SkillManifest
{
    [JsonPropertyName("skills")]
    public List<SkillEntry> Skills { get; init; } = [];

    [JsonPropertyName("bundles")]
    public List<SkillPackageEntry> Packages { get; init; } = [];
}

internal sealed class SkillEntry
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("package")]
    public string Package { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("compatibility")]
    public string Compatibility { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("packages")]
    public List<string> Packages { get; init; } = [];

    [JsonPropertyName("package_prefix")]
    public string PackagePrefix { get; init; } = string.Empty;

    [JsonPropertyName("links")]
    public CatalogLinks Links { get; init; } = CatalogLinks.Empty;
}

internal sealed class SkillPackageEntry
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("sourceCategory")]
    public string SourceCategory { get; init; } = string.Empty;

    [JsonPropertyName("skills")]
    public List<string> Skills { get; init; } = [];
}

internal sealed class CatalogLinks
{
    public static CatalogLinks Empty { get; } = new();

    [JsonPropertyName("repository")]
    public string Repository { get; init; } = string.Empty;

    [JsonPropertyName("docs")]
    public string Docs { get; init; } = string.Empty;

    [JsonPropertyName("nuget")]
    public string NuGet { get; init; } = string.Empty;
}
