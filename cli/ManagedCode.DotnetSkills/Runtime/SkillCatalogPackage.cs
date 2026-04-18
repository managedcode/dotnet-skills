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
        Skills = NormalizeSkills(catalogRoot, skills);
        Packages = NormalizePackages(packages, Skills);
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
        var directory = PathSafety.ResolveDirectoryWithinRoot(
            CatalogRoot,
            skill.Path,
            $"Skill payload path for {skillName} in {SourceLabel}");
        if (!directory.Exists)
        {
            throw new InvalidOperationException($"Skill payload is missing for {skillName} in {SourceLabel}");
        }

        return directory;
    }

    private static IReadOnlyList<SkillEntry> NormalizeSkills(DirectoryInfo catalogRoot, IReadOnlyList<SkillEntry> skills)
    {
        return skills.Select(skill =>
        {
            var grouping = string.IsNullOrWhiteSpace(skill.Stack) || string.IsNullOrWhiteSpace(skill.Lane)
                ? CatalogOrganization.Classify(skill)
                : null;
            var tokenCount = skill.TokenCount > 0 ? skill.TokenCount : TryResolveTokenCount(catalogRoot, skill.Path);

            if (!string.IsNullOrWhiteSpace(skill.Stack) && !string.IsNullOrWhiteSpace(skill.Lane) && tokenCount == skill.TokenCount)
            {
                return skill;
            }

            return new SkillEntry
            {
                Name = skill.Name,
                Title = skill.Title,
                Version = skill.Version,
                Category = skill.Category,
                Type = skill.Type,
                Package = skill.Package,
                Stack = string.IsNullOrWhiteSpace(skill.Stack) ? grouping!.Stack : skill.Stack,
                Lane = string.IsNullOrWhiteSpace(skill.Lane) ? grouping!.Lane : skill.Lane,
                Description = skill.Description,
                Compatibility = skill.Compatibility,
                Path = skill.Path,
                TokenCount = tokenCount,
                Packages = [.. skill.Packages],
                PackagePrefix = skill.PackagePrefix,
                Links = skill.Links,
            };
        }).ToArray();
    }

    private static IReadOnlyList<SkillPackageEntry> NormalizePackages(IReadOnlyList<SkillPackageEntry> packages, IReadOnlyList<SkillEntry> skills)
    {
        var skillsByName = skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);

        return packages.Select(package =>
        {
            if (!string.IsNullOrWhiteSpace(package.Stack) && !string.IsNullOrWhiteSpace(package.Lane))
            {
                return package;
            }

            var bundleSkills = package.Skills
                .Select(skillName => skillsByName.TryGetValue(skillName, out var skill) ? skill : null)
                .Where(skill => skill is not null)
                .Cast<SkillEntry>()
                .ToArray();

            var stack = string.IsNullOrWhiteSpace(package.Stack)
                ? ResolveBundleStack(bundleSkills)
                : package.Stack;
            var lane = string.IsNullOrWhiteSpace(package.Lane)
                ? ResolveBundleLane(bundleSkills)
                : package.Lane;

            return new SkillPackageEntry
            {
                Name = package.Name,
                Title = package.Title,
                Description = package.Description,
                Kind = package.Kind,
                Stack = stack,
                Lane = lane,
                SourceCategory = package.SourceCategory,
                Skills = [.. package.Skills],
            };
        }).ToArray();
    }

    private static string ResolveBundleStack(IReadOnlyList<SkillEntry> skills)
    {
        return skills
            .GroupBy(skill => skill.Stack, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => CatalogOrganization.GetStackRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string ResolveBundleLane(IReadOnlyList<SkillEntry> skills)
    {
        return skills
            .GroupBy(skill => skill.Lane, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => CatalogOrganization.GetLaneRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault() ?? string.Empty;
    }

    private static int TryResolveTokenCount(DirectoryInfo catalogRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return 0;
        }

        try
        {
            var directory = PathSafety.ResolveDirectoryWithinRoot(
                catalogRoot,
                relativePath,
                $"Skill payload path for token metadata {relativePath}");
            var skillPath = new FileInfo(Path.Combine(directory.FullName, "SKILL.md"));
            return SkillTokenCounter.CountTokens(skillPath);
        }
        catch
        {
            return 0;
        }
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

    [JsonPropertyName("stack")]
    public string Stack { get; init; } = string.Empty;

    [JsonPropertyName("lane")]
    public string Lane { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("compatibility")]
    public string Compatibility { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("tokenCount")]
    public int TokenCount { get; init; }

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

    [JsonPropertyName("stack")]
    public string Stack { get; init; } = string.Empty;

    [JsonPropertyName("lane")]
    public string Lane { get; init; } = string.Empty;

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
