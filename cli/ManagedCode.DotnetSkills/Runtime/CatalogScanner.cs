using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagedCode.DotnetSkills.Runtime;

internal static class CatalogScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly CuratedBundleDefinition[] CuratedBundles =
    [
        new(
            "mcaf",
            "MCAF bundle",
            "Install the locally mirrored MCAF governance skills in one command, including adoption, delivery workflow, developer experience, documentation, feature specs, review planning, NFRs, source-control policy, UI/UX, and ML/AI delivery guidance.",
            "curated",
            [
                "dotnet-mcaf",
                "dotnet-mcaf-agile-delivery",
                "dotnet-mcaf-devex",
                "dotnet-mcaf-documentation",
                "dotnet-mcaf-feature-spec",
                "dotnet-mcaf-human-review-planning",
                "dotnet-mcaf-ml-ai-delivery",
                "dotnet-mcaf-nfr",
                "dotnet-mcaf-source-control",
                "dotnet-mcaf-ui-ux",
            ]),
        new(
            "orleans",
            "Orleans bundle",
            "Install the main Orleans stack in one command, including Orleans core guidance, adjacent ManagedCode integrations, worker-hosting patterns, Aspire orchestration, and SignalR delivery support.",
            "curated",
            [
                "dotnet-orleans",
                "dotnet-managedcode-orleans-graph",
                "dotnet-managedcode-orleans-signalr",
                "dotnet-worker-services",
                "dotnet-aspire",
                "dotnet-signalr",
            ]),
    ];

    public static SkillCatalogScanResult ScanSkills(DirectoryInfo rootDirectory)
    {
        var catalogRoot = ResolveCatalogRoot(rootDirectory);
        var skills = new List<SkillEntry>();

        foreach (var typeDirectoryName in ResolveTypeDirectories(catalogRoot))
        {
            var typeDirectory = new DirectoryInfo(Path.Combine(catalogRoot.FullName, typeDirectoryName));
            if (!typeDirectory.Exists)
            {
                continue;
            }

            var skillType = SingularizeTypeDirectory(typeDirectoryName);

            foreach (var packageDirectory in typeDirectory.EnumerateDirectories().OrderBy(directory => directory.Name, StringComparer.Ordinal))
            {
                var manifestPath = new FileInfo(Path.Combine(packageDirectory.FullName, "manifest.json"));
                if (!manifestPath.Exists)
                {
                    throw new InvalidOperationException($"{packageDirectory.FullName} is missing manifest.json");
                }

                var packageManifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(manifestPath.FullName), JsonOptions)
                    ?? throw new InvalidOperationException($"Could not parse {manifestPath.FullName}");
                EnsureNoLegacyPackageSkillMap(manifestPath, packageManifest);
                var packageLinks = ReadPackageLinks(manifestPath, packageManifest);

                var skillDirectories = new DirectoryInfo(Path.Combine(packageDirectory.FullName, "skills"));
                if (!skillDirectories.Exists)
                {
                    continue;
                }

                foreach (var skillDirectory in skillDirectories.EnumerateDirectories().OrderBy(directory => directory.Name, StringComparer.Ordinal))
                {
                    var skillPath = new FileInfo(Path.Combine(skillDirectory.FullName, "SKILL.md"));
                    if (!skillPath.Exists)
                    {
                        continue;
                    }
                    var (skillManifestPath, skillManifest) = LoadOptionalEntityManifest(skillDirectory);

                    var (metadata, body) = ParseFrontmatter(skillPath);
                    var title = ParseTitle(body, skillPath);
                    var expectedManifestPath = new FileInfo(Path.Combine(skillDirectory.FullName, "manifest.json"));
                    EnsureRequiredSkillFields(metadata, skillPath);
                    EnsureNoInlineSkillMetadata(metadata, skillPath, expectedManifestPath);

                    var skillName = metadata["name"];
                    var manifestSkillMetadata = ReadSkillManifestMetadata(skillManifestPath, skillManifest, expectedManifestPath);

                    skills.Add(
                        new SkillEntry
                        {
                            Name = skillName,
                            Title = title,
                            Version = manifestSkillMetadata.Version,
                            Category = manifestSkillMetadata.Category,
                            Type = skillType,
                            Package = packageDirectory.Name,
                            Description = metadata["description"],
                            Compatibility = metadata["compatibility"],
                            Path = BuildRelativePath(rootDirectory, skillDirectory),
                            Packages = manifestSkillMetadata.Packages,
                            PackagePrefix = manifestSkillMetadata.PackagePrefix,
                            Links = packageLinks,
                        });
                }
            }
        }

        EnsureUniqueNames(skills, skill => skill.Name, skill => skill.Path, "skill");
        return new SkillCatalogScanResult(catalogRoot, skills, BuildBundles(skills));
    }

    public static IReadOnlyList<AgentEntry> ScanAgents(DirectoryInfo rootDirectory)
    {
        var catalogRoot = ResolveCatalogRoot(rootDirectory);
        var agents = new List<AgentEntry>();

        foreach (var typeDirectoryName in ResolveTypeDirectories(catalogRoot))
        {
            var typeDirectory = new DirectoryInfo(Path.Combine(catalogRoot.FullName, typeDirectoryName));
            if (!typeDirectory.Exists)
            {
                continue;
            }

            foreach (var packageDirectory in typeDirectory.EnumerateDirectories().OrderBy(directory => directory.Name, StringComparer.Ordinal))
            {
                var manifestPath = new FileInfo(Path.Combine(packageDirectory.FullName, "manifest.json"));
                if (!manifestPath.Exists)
                {
                    throw new InvalidOperationException($"{packageDirectory.FullName} is missing manifest.json");
                }

                var packageManifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(manifestPath.FullName), JsonOptions)
                    ?? throw new InvalidOperationException($"Could not parse {manifestPath.FullName}");
                EnsureNoLegacyPackageSkillMap(manifestPath, packageManifest);
                var packageLinks = ReadPackageLinks(manifestPath, packageManifest);
                var agentsDirectory = new DirectoryInfo(Path.Combine(packageDirectory.FullName, "agents"));
                if (!agentsDirectory.Exists)
                {
                    continue;
                }

                foreach (var agentDirectory in agentsDirectory.EnumerateDirectories().OrderBy(directory => directory.Name, StringComparer.Ordinal))
                {
                    var agentPath = new FileInfo(Path.Combine(agentDirectory.FullName, "AGENT.md"));
                    if (!agentPath.Exists)
                    {
                        continue;
                    }
                    var (agentManifestPath, agentManifest) = LoadOptionalEntityManifest(agentDirectory);

                    var (metadata, body) = ParseFrontmatterWithLists(agentPath);
                    EnsureRequiredAgentFields(metadata, agentPath);
                    var title = ParseTitle(body, agentPath);
                    var agentManifestMetadata = ReadAgentManifestMetadata(agentManifestPath, agentManifest);

                    agents.Add(
                        new AgentEntry
                        {
                            Name = GetString(metadata, "name"),
                            Title = title,
                            Description = GetString(metadata, "description"),
                            Skills = GetList(metadata, "skills"),
                            Tools = GetStringOrDefault(metadata, "tools"),
                            Model = string.IsNullOrWhiteSpace(GetStringOrDefault(metadata, "model")) ? "inherit" : GetStringOrDefault(metadata, "model"),
                            Package = packageDirectory.Name,
                            Type = typeDirectoryName,
                            Path = BuildRelativePath(rootDirectory, agentDirectory),
                            Packages = agentManifestMetadata.Packages,
                            PackagePrefix = agentManifestMetadata.PackagePrefix,
                            Links = packageLinks,
                        });
                }
            }
        }

        EnsureUniqueNames(agents, agent => agent.Name, agent => agent.Path, "agent");
        return agents;
    }

    private static DirectoryInfo ResolveCatalogRoot(DirectoryInfo rootDirectory)
    {
        var catalogDirectory = new DirectoryInfo(Path.Combine(rootDirectory.FullName, "catalog"));
        if (!catalogDirectory.Exists)
        {
            throw new InvalidOperationException($"catalog directory was not found under {rootDirectory.FullName}");
        }

        return catalogDirectory;
    }

    private static string BuildRelativePath(DirectoryInfo rootDirectory, DirectoryInfo targetDirectory)
    {
        var relativePath = Path.GetRelativePath(rootDirectory.FullName, targetDirectory.FullName);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/') + "/";
    }

    private static (FileInfo? ManifestPath, EntityManifest Manifest) LoadOptionalEntityManifest(DirectoryInfo entityDirectory)
    {
        var manifestPath = new FileInfo(Path.Combine(entityDirectory.FullName, "manifest.json"));
        if (!manifestPath.Exists)
        {
            return (null, new EntityManifest());
        }

        var manifest = JsonSerializer.Deserialize<EntityManifest>(File.ReadAllText(manifestPath.FullName), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse {manifestPath.FullName}");
        return (manifestPath, manifest);
    }

    private static SkillManifestMetadata ReadSkillManifestMetadata(FileInfo? manifestPath, EntityManifest manifest, FileInfo expectedManifestPath)
    {
        if (manifestPath is null)
        {
            throw new InvalidOperationException($"{expectedManifestPath.FullName} is required and must define version and category.");
        }

        if (manifest.AdditionalData is { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"{manifestPath.FullName} has unsupported keys: {string.Join(", ", manifest.AdditionalData.Keys.OrderBy(key => key, StringComparer.Ordinal))}");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException($"{manifestPath.FullName} field version must be a non-empty string");
        }

        if (string.IsNullOrWhiteSpace(manifest.Category))
        {
            throw new InvalidOperationException($"{manifestPath.FullName} field category must be a non-empty string");
        }

        var category = manifest.Category.Trim();
        if (manifest.Packages.Any(package => string.IsNullOrWhiteSpace(package)))
        {
            throw new InvalidOperationException($"{manifestPath.FullName} field packages must be a list of non-empty strings");
        }

        if (manifest.PackagePrefix.Length > 0 && string.IsNullOrWhiteSpace(manifest.PackagePrefix))
        {
            throw new InvalidOperationException($"{manifestPath.FullName} field package_prefix must be a non-empty string");
        }

        return new SkillManifestMetadata(
            manifest.Version.Trim(),
            category,
            [.. manifest.Packages.Select(package => package.Trim())],
            manifest.PackagePrefix.Trim());
    }

    private static AgentManifestMetadata ReadAgentManifestMetadata(FileInfo? manifestPath, EntityManifest manifest)
    {
        if (manifestPath is null)
        {
            return AgentManifestMetadata.Empty;
        }

        if (manifest.AdditionalData is { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"{manifestPath.FullName} has unsupported keys: {string.Join(", ", manifest.AdditionalData.Keys.OrderBy(key => key, StringComparer.Ordinal))}");
        }

        if (manifest.Packages.Any(package => string.IsNullOrWhiteSpace(package)))
        {
            throw new InvalidOperationException($"{manifestPath.FullName} field packages must be a list of non-empty strings");
        }

        if (manifest.PackagePrefix.Length > 0 && string.IsNullOrWhiteSpace(manifest.PackagePrefix))
        {
            throw new InvalidOperationException($"{manifestPath.FullName} field package_prefix must be a non-empty string");
        }

        return new AgentManifestMetadata([.. manifest.Packages.Select(package => package.Trim())], manifest.PackagePrefix.Trim());
    }

    private static void EnsureNoLegacyPackageSkillMap(FileInfo manifestPath, PackageManifest manifest)
    {
        if (manifest.LegacySkills is not null)
        {
            throw new InvalidOperationException(
                $"{manifestPath.FullName} must not define a top-level skills map; move skill-specific metadata into the nearest skills/<skill>/manifest.json file.");
        }
    }

    private static CatalogLinks ReadPackageLinks(FileInfo manifestPath, PackageManifest manifest)
    {
        if (manifest.Links is null)
        {
            return CatalogLinks.Empty;
        }

        if (manifest.Links.AdditionalData is { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"{manifestPath.FullName} field links has unsupported keys: {string.Join(", ", manifest.Links.AdditionalData.Keys.OrderBy(key => key, StringComparer.Ordinal))}");
        }

        ValidateLinkValue(manifestPath, "repository", manifest.Links.Repository);
        ValidateLinkValue(manifestPath, "docs", manifest.Links.Docs);
        ValidateLinkValue(manifestPath, "nuget", manifest.Links.NuGet);

        return new CatalogLinks
        {
            Repository = manifest.Links.Repository.Trim(),
            Docs = manifest.Links.Docs.Trim(),
            NuGet = manifest.Links.NuGet.Trim(),
        };
    }

    private static void ValidateLinkValue(FileInfo manifestPath, string key, string value)
    {
        if (value.Length > 0 && string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{manifestPath.FullName} field links.{key} must be a non-empty string");
        }
    }

    private static IReadOnlyList<SkillPackageEntry> BuildBundles(IReadOnlyList<SkillEntry> skills)
    {
        var bundles = new List<SkillPackageEntry>();
        var skillsByName = skills.ToDictionary(skill => skill.Name, StringComparer.Ordinal);

        foreach (var bundle in CuratedBundles)
        {
            var missing = bundle.Skills.Where(skillName => !skillsByName.ContainsKey(skillName)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException($"Curated bundle {bundle.Name} references unknown skills: {string.Join(", ", missing)}");
            }

            bundles.Add(
                new SkillPackageEntry
                {
                    Name = bundle.Name,
                    Title = bundle.Title,
                    Description = bundle.Description,
                    Kind = bundle.Kind,
                    SourceCategory = string.Empty,
                    Skills = [.. bundle.Skills],
                });
        }

        foreach (var category in ResolveCategoryOrder(skills))
        {
            var categorySkills = skills
                .Where(skill => string.Equals(skill.Category, category, StringComparison.Ordinal))
                .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .ToArray();

            if (categorySkills.Length == 0)
            {
                continue;
            }

            bundles.Add(
                new SkillPackageEntry
                {
                    Name = Slugify(category),
                    Title = $"{category} bundle",
                    Description = $"Install all {categorySkills.Length} skills from the {category} category in one command.",
                    Kind = "category",
                    SourceCategory = category,
                    Skills = [.. categorySkills.Select(skill => skill.Name)],
                });
        }

        return bundles;
    }

    private static IReadOnlyList<string> ResolveTypeDirectories(DirectoryInfo catalogRoot)
    {
        var discovered = new HashSet<string>(
            catalogRoot.EnumerateDirectories()
                .Select(directory => directory.Name)
                .Where(name => !name.StartsWith(".", StringComparison.Ordinal)),
            StringComparer.Ordinal);
        var ordered = new List<string>();

        foreach (var preferred in CatalogGeneratedDefinitions.TypeDirectories)
        {
            if (discovered.Remove(preferred))
            {
                ordered.Add(preferred);
            }
        }

        ordered.AddRange(discovered.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ThenBy(name => name, StringComparer.Ordinal));
        return ordered;
    }

    private static IReadOnlyList<string> ResolveCategoryOrder(IReadOnlyList<SkillEntry> skills)
    {
        var discovered = new HashSet<string>(
            skills
                .Select(skill => skill.Category.Trim())
                .Where(category => category.Length > 0),
            StringComparer.Ordinal);
        var ordered = new List<string>();

        foreach (var preferred in CatalogGeneratedDefinitions.CategoryOrder)
        {
            if (discovered.Remove(preferred))
            {
                ordered.Add(preferred);
            }
        }

        ordered.AddRange(discovered.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ThenBy(name => name, StringComparer.Ordinal));
        return ordered;
    }

    private static string SingularizeTypeDirectory(string typeDirectoryName)
    {
        if (typeDirectoryName.EndsWith("ies", StringComparison.Ordinal))
        {
            return $"{typeDirectoryName[..^3]}y";
        }

        if (typeDirectoryName.EndsWith("s", StringComparison.Ordinal))
        {
            return typeDirectoryName[..^1];
        }

        return typeDirectoryName;
    }

    private static void EnsureRequiredSkillFields(IReadOnlyDictionary<string, string> metadata, FileInfo skillPath)
    {
        var required = new[] { "name", "description", "compatibility" };
        var missing = required.Where(key => !metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"{skillPath.FullName} is missing required frontmatter keys: {string.Join(", ", missing)}");
        }
    }

    private static void EnsureNoInlineSkillMetadata(IReadOnlyDictionary<string, string> metadata, FileInfo skillPath, FileInfo manifestPath)
    {
        var disallowed = new[] { "version", "category", "packages", "package_prefix" }
            .Where(metadata.ContainsKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (disallowed.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{skillPath.FullName} must not declare {string.Join(", ", disallowed)} in frontmatter; move them to {manifestPath.FullName}.");
    }

    private static void EnsureRequiredAgentFields(IReadOnlyDictionary<string, object> metadata, FileInfo agentPath)
    {
        var required = new[] { "name", "description" };
        var missing = required
            .Where(key => !metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value?.ToString()))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"{agentPath.FullName} is missing required frontmatter keys: {string.Join(", ", missing)}");
        }
    }

    private static string ParseTitle(string body, FileInfo path)
    {
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        throw new InvalidOperationException($"{path.FullName} is missing an H1 title");
    }

    private static (Dictionary<string, string> Metadata, string Body) ParseFrontmatter(FileInfo path)
    {
        var text = File.ReadAllText(path.FullName);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path.FullName} is missing YAML frontmatter");
        }

        var marker = "\n---\n";
        var markerIndex = text.IndexOf(marker, startIndex: 4, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException($"{path.FullName} has invalid frontmatter");
        }

        var rawFrontmatter = text[4..markerIndex];
        var body = text[(markerIndex + marker.Length)..];
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in rawFrontmatter.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                throw new InvalidOperationException($"{path.FullName} has malformed frontmatter line: {line}");
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            metadata[key] = Unquote(value);
        }

        return (metadata, body);
    }

    private static (Dictionary<string, object> Metadata, string Body) ParseFrontmatterWithLists(FileInfo path)
    {
        var text = File.ReadAllText(path.FullName);
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path.FullName} is missing YAML frontmatter");
        }

        var marker = "\n---\n";
        var markerIndex = text.IndexOf(marker, startIndex: 4, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException($"{path.FullName} has invalid frontmatter");
        }

        var rawFrontmatter = text[4..markerIndex];
        var body = text[(markerIndex + marker.Length)..];
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        string? currentKey = null;
        List<string>? currentList = null;

        foreach (var rawLine in rawFrontmatter.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("  - ", StringComparison.Ordinal))
            {
                if (currentKey is null || currentList is null)
                {
                    throw new InvalidOperationException($"{path.FullName} has a list item without a parent key: {line}");
                }

                currentList.Add(line[4..].Trim());
                continue;
            }

            if (currentKey is not null && currentList is not null)
            {
                metadata[currentKey] = currentList;
                currentKey = null;
                currentList = null;
            }

            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                throw new InvalidOperationException($"{path.FullName} has malformed frontmatter line: {line}");
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                currentKey = key;
                currentList = [];
                continue;
            }

            metadata[key] = Unquote(value);
        }

        if (currentKey is not null && currentList is not null)
        {
            metadata[currentKey] = currentList;
        }

        return (metadata, body);
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == value[^1] && (value[0] == '"' || value[0] == '\'')
            ? value[1..^1]
            : value;
    }

    private static string Slugify(string value)
    {
        var buffer = new List<char>(value.Length);
        var previousDash = false;

        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(ch);
                previousDash = false;
                continue;
            }

            if (previousDash)
            {
                continue;
            }

            buffer.Add('-');
            previousDash = true;
        }

        return new string(buffer.ToArray()).Trim('-');
    }

    private static string GetString(IReadOnlyDictionary<string, object> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static string GetStringOrDefault(IReadOnlyDictionary<string, object> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    private static List<string> GetList(IReadOnlyDictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value))
        {
            return [];
        }

        return value switch
        {
            List<string> list => list,
            _ => [],
        };
    }

    private static void EnsureUniqueNames<T>(IEnumerable<T> entries, Func<T, string> nameSelector, Func<T, string> pathSelector, string kind)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        foreach (var entry in entries)
        {
            var name = nameSelector(entry);
            var path = pathSelector(entry);
            if (!seen.TryAdd(name, path))
            {
                duplicates.Add($"{name}: {seen[name]}, {path}");
            }
        }

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException($"Duplicate {kind} ids were found in catalog metadata: {string.Join("; ", duplicates)}");
        }
    }
}

internal sealed record SkillCatalogScanResult(
    DirectoryInfo CatalogRoot,
    IReadOnlyList<SkillEntry> Skills,
    IReadOnlyList<SkillPackageEntry> Packages);

internal sealed record CuratedBundleDefinition(
    string Name,
    string Title,
    string Description,
    string Kind,
    IReadOnlyList<string> Skills);

internal sealed class PackageManifest
{
    [JsonPropertyName("links")]
    public PackageLinks Links { get; init; } = new();

    [JsonPropertyName("skills")]
    public Dictionary<string, JsonElement>? LegacySkills { get; init; }
}

internal sealed class PackageLinks
{
    [JsonPropertyName("repository")]
    public string Repository { get; init; } = string.Empty;

    [JsonPropertyName("docs")]
    public string Docs { get; init; } = string.Empty;

    [JsonPropertyName("nuget")]
    public string NuGet { get; init; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

internal sealed class EntityManifest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("packages")]
    public List<string> Packages { get; init; } = [];

    [JsonPropertyName("package_prefix")]
    public string PackagePrefix { get; init; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

internal sealed record SkillManifestMetadata(string Version, string Category, List<string> Packages, string PackagePrefix)
{
}

internal sealed record AgentManifestMetadata(List<string> Packages, string PackagePrefix)
{
    public static AgentManifestMetadata Empty { get; } = new([], string.Empty);
}
