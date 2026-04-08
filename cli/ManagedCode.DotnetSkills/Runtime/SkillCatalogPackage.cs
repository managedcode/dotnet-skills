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
        DirectoryInfo skillsRoot,
        IReadOnlyList<SkillEntry> skills,
        IReadOnlyList<SkillPackageEntry> packages,
        string sourceLabel,
        string catalogVersion)
    {
        CatalogRoot = catalogRoot;
        SkillsRoot = skillsRoot;
        Skills = skills;
        Packages = packages;
        SourceLabel = sourceLabel;
        CatalogVersion = catalogVersion;
    }

    public DirectoryInfo CatalogRoot { get; }

    public DirectoryInfo SkillsRoot { get; }

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
        var skillsRoot = ResolveSkillsRoot(rootDirectory);
        var manifestPath = new FileInfo(Path.Combine(rootDirectory.FullName, "catalog", "skills.json"));

        if (!manifestPath.Exists)
        {
            throw new InvalidOperationException($"skills manifest was not found under {manifestPath.FullName}");
        }

        var manifest = JsonSerializer.Deserialize<SkillManifest>(File.ReadAllText(manifestPath.FullName), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse {manifestPath.FullName}");

        return new SkillCatalogPackage(rootDirectory, skillsRoot, manifest.Skills, manifest.Packages, sourceLabel, catalogVersion);
    }

    private static DirectoryInfo ResolveSkillsRoot(DirectoryInfo rootDirectory)
    {
        foreach (var candidateName in new[] { "skills", "Skills" })
        {
            var candidate = new DirectoryInfo(Path.Combine(rootDirectory.FullName, candidateName));
            if (candidate.Exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"skills directory was not found under {rootDirectory.FullName}");
    }

    public DirectoryInfo ResolveSkillSource(string skillName)
    {
        var directory = new DirectoryInfo(Path.Combine(SkillsRoot.FullName, skillName));
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

    [JsonPropertyName("packages")]
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

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("compatibility")]
    public string Compatibility { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;
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
