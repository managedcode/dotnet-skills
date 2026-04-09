using System.Xml.Linq;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class ProjectSkillRecommender(SkillCatalogPackage catalog)
{
    public ProjectScanResult Analyze(string? projectDirectory)
    {
        var rootPath = string.IsNullOrWhiteSpace(projectDirectory)
            ? Path.GetFullPath(Directory.GetCurrentDirectory())
            : Path.GetFullPath(projectDirectory);

        var projectFiles = Directory
            .EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(path))
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.FullName, StringComparer.Ordinal)
            .ToArray();

        if (projectFiles.Length == 0)
        {
            throw new InvalidOperationException($"No .csproj files were found under {rootPath}");
        }

        var inventory = ProjectInventory.Load(projectFiles);
        var recommendations = BuildRecommendations(rootPath, projectFiles.Length, inventory);

        return new ProjectScanResult(new DirectoryInfo(rootPath), projectFiles, inventory.TargetFrameworks.OrderBy(value => value, StringComparer.Ordinal).ToArray(), recommendations);
    }

    private IReadOnlyList<ProjectSkillRecommendation> BuildRecommendations(string rootPath, int projectCount, ProjectInventory inventory)
    {
        var builders = new Dictionary<string, RecommendationBuilder>(StringComparer.OrdinalIgnoreCase);

        Add("dotnet", RecommendationConfidence.Low, $"Detected {projectCount} .NET project file(s).", RecommendationSignalKind.Project, builders);
        Add("dotnet-modern-csharp", RecommendationConfidence.Low, FormatFrameworkReason(inventory.TargetFrameworks), RecommendationSignalKind.TargetFramework, builders);

        if (projectCount > 1 || HasSolutionFile(rootPath))
        {
            Add("dotnet-project-setup", RecommendationConfidence.Medium, $"Repository layout includes {projectCount} projects.", RecommendationSignalKind.Project, builders);
        }

        if (inventory.HasSdk("Microsoft.NET.Sdk.Web"))
        {
            Add("dotnet-aspnet-core", RecommendationConfidence.High, "Project uses Microsoft.NET.Sdk.Web.", RecommendationSignalKind.Sdk, builders);
            Add("dotnet-microsoft-extensions", RecommendationConfidence.Medium, "ASP.NET Core apps rely on the Microsoft.Extensions hosting stack.", RecommendationSignalKind.Sdk, builders);
        }

        if (inventory.HasSdk("Microsoft.NET.Sdk.BlazorWebAssembly"))
        {
            Add("dotnet-blazor", RecommendationConfidence.High, "Project uses Microsoft.NET.Sdk.BlazorWebAssembly.", RecommendationSignalKind.Sdk, builders);
        }

        if (inventory.HasSdk("Aspire.AppHost.Sdk"))
        {
            Add("dotnet-aspire", RecommendationConfidence.High, "Project uses Aspire.AppHost.Sdk.", RecommendationSignalKind.Sdk, builders);
        }

        if (inventory.HasSdkPrefix("Uno."))
        {
            Add("dotnet-uno-platform", RecommendationConfidence.High, "Project uses an Uno SDK.", RecommendationSignalKind.Sdk, builders);
        }

        AddManifestDrivenPackageRecommendations(builders, inventory);
        AddPackageRecommendation("dotnet-grpc", RecommendationConfidence.High, builders, inventory, "Google.Protobuf", "Detected Google.Protobuf alongside gRPC-style dependencies.");
        AddPackagePrefixRecommendation("dotnet-orleans", RecommendationConfidence.High, builders, inventory, "Orleans.", "Detected Orleans packages.");
        AddPackagePrefixRecommendation("dotnet-uno-platform", RecommendationConfidence.High, builders, inventory, "Uno.", "Detected Uno Platform packages.");
        AddPackagePrefixRecommendation("dotnet-wcf", RecommendationConfidence.Medium, builders, inventory, "CoreWCF", "Detected CoreWCF packages.");
        AddPackageRecommendation("dotnet-mvvm", RecommendationConfidence.High, builders, inventory, "CommunityToolkit.Mvvm", "Detected MVVM Toolkit packages.");

        if (inventory.HasSdk("Microsoft.NET.Sdk.Worker"))
        {
            Add("dotnet-worker-services", RecommendationConfidence.Medium, "Project uses Microsoft.NET.Sdk.Worker.", RecommendationSignalKind.Sdk, builders);
            Add("dotnet-microsoft-extensions", RecommendationConfidence.Medium, "Worker services are built on Microsoft.Extensions hosting primitives.", RecommendationSignalKind.Sdk, builders);
        }

        if (inventory.HasPackage("Microsoft.Extensions.Hosting"))
        {
            Add("dotnet-worker-services", RecommendationConfidence.Medium, "Detected Microsoft.Extensions.Hosting package usage.", RecommendationSignalKind.Package, builders);
            Add("dotnet-microsoft-extensions", RecommendationConfidence.Medium, "Detected Microsoft.Extensions.Hosting package usage.", RecommendationSignalKind.Package, builders);
        }

        if (inventory.UsesMaui)
        {
            Add("dotnet-maui", RecommendationConfidence.High, "Project enables UseMaui.", RecommendationSignalKind.ProjectProperty, builders);
        }

        if (inventory.HasPackagePrefix("Microsoft.Maui"))
        {
            Add("dotnet-maui", RecommendationConfidence.High, "Detected Microsoft.Maui packages.", RecommendationSignalKind.Package, builders);
        }

        if (inventory.UsesWpf)
        {
            Add("dotnet-wpf", RecommendationConfidence.High, "Project enables UseWPF.", RecommendationSignalKind.ProjectProperty, builders);
        }

        if (inventory.UsesWindowsForms)
        {
            Add("dotnet-winforms", RecommendationConfidence.High, "Project enables UseWindowsForms.", RecommendationSignalKind.ProjectProperty, builders);
        }

        return builders.Values
            .Select(builder => builder.Build(catalog))
            .Where(recommendation => recommendation is not null)
            .Cast<ProjectSkillRecommendation>()
            .OrderByDescending(recommendation => recommendation.Confidence)
            .ThenBy(recommendation => recommendation.Skill.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsIgnoredPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains(".git", StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasSolutionFile(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.TopDirectoryOnly).Any()
            || Directory.EnumerateFiles(rootPath, "*.slnx", SearchOption.TopDirectoryOnly).Any();
    }

    private static string FormatFrameworkReason(IReadOnlyCollection<string> frameworks)
    {
        if (frameworks.Count == 0)
        {
            return "Detected a .NET project and modern C# guidance is broadly applicable.";
        }

        return $"Target frameworks: {string.Join(", ", frameworks.OrderBy(value => value, StringComparer.Ordinal))}.";
    }

    private static void AddPackageRecommendation(
        string skillName,
        RecommendationConfidence confidence,
        IDictionary<string, RecommendationBuilder> builders,
        ProjectInventory inventory,
        string packageId,
        string reason)
    {
        if (inventory.HasPackage(packageId))
        {
            Add(skillName, confidence, reason, RecommendationSignalKind.Package, builders);
        }
    }

    private static void AddPackagePrefixRecommendation(
        string skillName,
        RecommendationConfidence confidence,
        IDictionary<string, RecommendationBuilder> builders,
        ProjectInventory inventory,
        string packagePrefix,
        string reason)
    {
        if (inventory.HasPackagePrefix(packagePrefix))
        {
            Add(skillName, confidence, reason, RecommendationSignalKind.Package, builders);
        }
    }

    private static void Add(
        string skillName,
        RecommendationConfidence confidence,
        string reason,
        RecommendationSignalKind signal,
        IDictionary<string, RecommendationBuilder> builders)
    {
        if (!builders.TryGetValue(skillName, out var builder))
        {
            builder = new RecommendationBuilder(skillName);
            builders[skillName] = builder;
        }

        builder.Add(confidence, reason, signal);
    }

    private void AddManifestDrivenPackageRecommendations(
        IDictionary<string, RecommendationBuilder> builders,
        ProjectInventory inventory)
    {
        foreach (var skill in catalog.Skills)
        {
            if (!string.IsNullOrWhiteSpace(skill.PackagePrefix) && inventory.HasPackagePrefix(skill.PackagePrefix))
            {
                Add(
                    skill.Name,
                    skill.Name.Equals("dotnet-microsoft-extensions", StringComparison.OrdinalIgnoreCase)
                        ? RecommendationConfidence.Medium
                        : RecommendationConfidence.High,
                    $"Detected packages with prefix {skill.PackagePrefix}.",
                    RecommendationSignalKind.Package,
                    builders);
            }

            foreach (var packageId in skill.Packages)
            {
                if (!inventory.HasPackage(packageId))
                {
                    continue;
                }

                Add(
                    skill.Name,
                    RecommendationConfidence.High,
                    $"Detected package {packageId}.",
                    RecommendationSignalKind.Package,
                    builders);
            }
        }
    }
}

internal sealed record ProjectScanResult(
    DirectoryInfo ProjectRoot,
    IReadOnlyList<FileInfo> ProjectFiles,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<ProjectSkillRecommendation> Recommendations);

internal sealed record ProjectSkillRecommendation(
    SkillEntry Skill,
    RecommendationConfidence Confidence,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<RecommendationSignalKind> Signals)
{
    public bool IsAutoInstallCandidate =>
        Signals.Any(signal => signal is RecommendationSignalKind.Package or RecommendationSignalKind.Sdk or RecommendationSignalKind.ProjectProperty);
}

internal enum RecommendationConfidence
{
    Low = 1,
    Medium = 2,
    High = 3,
}

internal enum RecommendationSignalKind
{
    Project,
    TargetFramework,
    Sdk,
    Package,
    ProjectProperty,
}

internal sealed class RecommendationBuilder(string skillName)
{
    private readonly List<string> reasons = [];
    private readonly HashSet<RecommendationSignalKind> signals = [];

    public RecommendationConfidence Confidence { get; private set; } = RecommendationConfidence.Low;

    public void Add(RecommendationConfidence confidence, string reason, RecommendationSignalKind signal)
    {
        if (!reasons.Contains(reason, StringComparer.Ordinal))
        {
            reasons.Add(reason);
        }

        signals.Add(signal);

        if (confidence > Confidence)
        {
            Confidence = confidence;
        }
    }

    public ProjectSkillRecommendation? Build(SkillCatalogPackage catalog)
    {
        var skill = catalog.Skills.FirstOrDefault(candidate => string.Equals(candidate.Name, skillName, StringComparison.OrdinalIgnoreCase));
        return skill is null ? null : new ProjectSkillRecommendation(skill, Confidence, reasons, signals.OrderBy(signal => signal).ToArray());
    }
}

internal sealed class ProjectInventory
{
    private readonly HashSet<string> sdks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> packageIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> targetFrameworks = new(StringComparer.OrdinalIgnoreCase);

    private ProjectInventory()
    {
    }

    public bool UsesMaui { get; private set; }

    public bool UsesWpf { get; private set; }

    public bool UsesWindowsForms { get; private set; }

    public IReadOnlyCollection<string> TargetFrameworks => targetFrameworks;

    public bool HasPackage(string packageId) => packageIds.Contains(packageId);

    public bool HasPackagePrefix(string packagePrefix) => packageIds.Any(package => package.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase));

    public bool HasSdk(string sdkName) => sdks.Contains(sdkName);

    public bool HasSdkPrefix(string sdkPrefix) => sdks.Any(sdk => sdk.StartsWith(sdkPrefix, StringComparison.OrdinalIgnoreCase));

    public static ProjectInventory Load(IEnumerable<FileInfo> projectFiles)
    {
        var inventory = new ProjectInventory();

        foreach (var projectFile in projectFiles)
        {
            XDocument document;
            try
            {
                document = XDocument.Load(projectFile.FullName, LoadOptions.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                continue;
            }

            var projectElement = document.Root;
            if (projectElement is null)
            {
                continue;
            }

            inventory.AddSdkAttribute(projectElement.Attribute("Sdk")?.Value);
            foreach (var sdkElement in projectElement.Elements().Where(element => string.Equals(element.Name.LocalName, "Sdk", StringComparison.OrdinalIgnoreCase)))
            {
                inventory.AddSdkAttribute(sdkElement.Attribute("Name")?.Value);
            }

            foreach (var property in projectElement.Descendants())
            {
                switch (property.Name.LocalName)
                {
                    case "TargetFramework":
                        inventory.AddFramework(property.Value);
                        break;
                    case "TargetFrameworks":
                        foreach (var framework in property.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            inventory.AddFramework(framework);
                        }

                        break;
                    case "UseWPF":
                        inventory.UsesWpf |= IsTrue(property.Value);
                        break;
                    case "UseWindowsForms":
                        inventory.UsesWindowsForms |= IsTrue(property.Value);
                        break;
                    case "UseMaui":
                        inventory.UsesMaui |= IsTrue(property.Value);
                        break;
                }
            }

            foreach (var packageReference in projectElement
                         .Descendants()
                         .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase)))
            {
                var packageId = packageReference.Attribute("Include")?.Value
                    ?? packageReference.Attribute("Update")?.Value;

                if (!string.IsNullOrWhiteSpace(packageId))
                {
                    inventory.packageIds.Add(packageId);
                }
            }
        }

        return inventory;
    }

    private static bool IsTrue(string value) => string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private void AddFramework(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            targetFrameworks.Add(value.Trim());
        }
    }

    private void AddSdkAttribute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var sdkName in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            sdks.Add(sdkName);
        }
    }

}
