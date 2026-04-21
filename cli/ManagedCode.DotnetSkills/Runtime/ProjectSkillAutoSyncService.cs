using System.Text.Json;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class ProjectSkillAutoSyncService(SkillCatalogPackage catalog)
{
    private static readonly HashSet<string> ProtectedSkillNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Keep durable diagnostics installed even when their NuGet signal disappears.
        "archunitnet",
        "coverlet",
        "graphify-dotnet",
        "meziantou-analyzer",
        "netarchtest",
        "reportgenerator",
        "roslynator",
        "stryker",
        "stylecop-analyzers",
    };

    private readonly ProjectSkillRecommender recommender = new(catalog);
    private readonly AutoManagedSkillStateStore stateStore = new();

    public ProjectSkillAutoSyncPlan BuildPlan(string? projectDirectory, SkillInstallLayout layout, SkillInstaller installer, bool prune)
    {
        var scanResult = recommender.Analyze(projectDirectory);
        var desiredSkills = scanResult.Recommendations
            .Where(recommendation => recommendation.IsAutoInstallCandidate)
            .Select(recommendation => recommendation.Skill)
            .DistinctBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .ToArray();

        var installedSkills = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var previousState = stateStore.Load(layout.PrimaryRoot);
        var matchedPreviousProject = prune
            && !string.IsNullOrWhiteSpace(previousState.ProjectRoot)
            && string.Equals(previousState.ProjectRoot, scanResult.ProjectRoot.FullName, StringComparison.OrdinalIgnoreCase);
        var desiredNames = desiredSkills
            .Select(skill => skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removable = new List<SkillEntry>();
        var protectedStale = new List<SkillEntry>();

        foreach (var skillName in matchedPreviousProject
                     ? previousState.Skills.OrderBy(name => name, StringComparer.Ordinal).ToArray()
                     : Array.Empty<string>())
        {
            if (desiredNames.Contains(skillName))
            {
                continue;
            }

            if (!installedSkills.TryGetValue(skillName, out var installed))
            {
                continue;
            }

            if (IsProtected(installed.Skill))
            {
                protectedStale.Add(installed.Skill);
                continue;
            }

            removable.Add(installed.Skill);
        }

        return new ProjectSkillAutoSyncPlan(
            scanResult,
            desiredSkills,
            removable,
            protectedStale,
            previousState,
            matchedPreviousProject);
    }

    public void SaveState(SkillInstallLayout layout, ProjectSkillAutoSyncPlan plan)
    {
        stateStore.Save(
            layout.PrimaryRoot,
            plan.ScanResult.ProjectRoot.FullName,
            plan.DesiredSkills.Select(skill => skill.Name));
    }

    public static bool IsProtected(SkillEntry skill)
    {
        return ProtectedSkillNames.Contains(skill.Name);
    }
}

internal sealed class AutoManagedSkillStateStore
{
    private const string StateFileName = ".dotnet-skills-auto-state.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public AutoManagedSkillState Load(DirectoryInfo root)
    {
        var path = Path.Combine(root.FullName, StateFileName);
        if (!File.Exists(path))
        {
            return AutoManagedSkillState.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<AutoManagedSkillState>(File.ReadAllText(path))
                ?? AutoManagedSkillState.Empty;
        }
        catch (Exception) when (path is not null)
        {
            return AutoManagedSkillState.Empty;
        }
    }

    public void Save(DirectoryInfo root, string projectRoot, IEnumerable<string> skillNames)
    {
        root.Create();

        var path = Path.Combine(root.FullName, StateFileName);
        var state = new AutoManagedSkillState(
            Version: 1,
            ProjectRoot: Path.GetFullPath(projectRoot),
            Skills: skillNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray(),
            UpdatedAt: DateTimeOffset.UtcNow);

        File.WriteAllText(path, JsonSerializer.Serialize(state, SerializerOptions));
    }
}

internal sealed record AutoManagedSkillState(
    int Version,
    string ProjectRoot,
    IReadOnlyList<string> Skills,
    DateTimeOffset UpdatedAt)
{
    public static AutoManagedSkillState Empty { get; } =
        new(Version: 1, ProjectRoot: string.Empty, Skills: Array.Empty<string>(), UpdatedAt: DateTimeOffset.MinValue);
}

internal sealed record ProjectSkillAutoSyncPlan(
    ProjectScanResult ScanResult,
    IReadOnlyList<SkillEntry> DesiredSkills,
    IReadOnlyList<SkillEntry> SkillsToRemove,
    IReadOnlyList<SkillEntry> ProtectedStaleSkills,
    AutoManagedSkillState PreviousState,
    bool MatchedPreviousProject);
