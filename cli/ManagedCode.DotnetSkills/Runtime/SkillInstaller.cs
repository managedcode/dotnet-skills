using System.Text.Json;

namespace ManagedCode.DotnetSkills.Runtime;

internal sealed class SkillInstaller(SkillCatalogPackage catalog)
{
    public IReadOnlyList<SkillEntry> SelectSkills(IReadOnlyList<string> requestedSkills, bool installAll)
    {
        if (installAll || requestedSkills.Count == 0)
        {
            return catalog.Skills.OrderBy(skill => skill.Name, StringComparer.Ordinal).ToArray();
        }

        var available = catalog.Skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
        var selected = new List<SkillEntry>();

        foreach (var skillName in requestedSkills)
        {
            if (!TryResolveSkill(available, skillName, out var skill))
            {
                throw new InvalidOperationException($"Unknown skill: {skillName}");
            }

            selected.Add(skill);
        }

        return selected;
    }

    public IReadOnlyList<SkillEntry> SelectSkillsFromPackages(IReadOnlyList<string> requestedPackages)
    {
        if (requestedPackages.Count == 0)
        {
            throw new InvalidOperationException("Specify one or more package names.");
        }

        var availableSkills = catalog.Skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
        var availablePackages = catalog.Packages.ToDictionary(
            package => NormalizePackageKey(package.Name),
            StringComparer.OrdinalIgnoreCase);

        var selected = new List<SkillEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var packageName in requestedPackages)
        {
            if (!TryResolvePackage(availablePackages, packageName, out var package))
            {
                throw new InvalidOperationException($"Unknown package: {packageName}");
            }

            foreach (var skillName in package.Skills)
            {
                if (!availableSkills.TryGetValue(skillName, out var skill))
                {
                    throw new InvalidOperationException($"Package {package.Name} references unknown skill {skillName}.");
                }

                if (seen.Add(skill.Name))
                {
                    selected.Add(skill);
                }
            }
        }

        return selected;
    }

    public SkillInstallSummary Install(IReadOnlyList<SkillEntry> skills, SkillInstallLayout layout, bool force)
    {
        layout.PrimaryRoot.Create();

        var installedCount = 0;
        var skippedExisting = new List<string>();

        foreach (var skill in skills)
        {
            var sourceDirectory = catalog.ResolveSkillSource(skill.Name);
            var destinationDirectory = new DirectoryInfo(Path.Combine(layout.PrimaryRoot.FullName, skill.Name));

            if (destinationDirectory.Exists)
            {
                if (!force)
                {
                    skippedExisting.Add(skill.Name);
                    continue;
                }

                destinationDirectory.Delete(recursive: true);
            }

            CopyDirectory(sourceDirectory, destinationDirectory);
            installedCount++;
        }

        return new SkillInstallSummary(installedCount, GeneratedAdapters: 0, skippedExisting);
    }

    public SkillRemoveSummary Remove(IReadOnlyList<SkillEntry> skills, SkillInstallLayout layout)
    {
        var removedCount = 0;
        var missingSkills = new List<string>();

        foreach (var skill in skills)
        {
            var destinationDirectory = new DirectoryInfo(Path.Combine(layout.PrimaryRoot.FullName, skill.Name));
            if (!destinationDirectory.Exists)
            {
                missingSkills.Add(skill.Name);
                continue;
            }

            destinationDirectory.Delete(recursive: true);
            removedCount++;
        }

        return new SkillRemoveSummary(removedCount, missingSkills);
    }

    public bool IsInstalled(SkillEntry skill, SkillInstallLayout layout)
    {
        return Directory.Exists(Path.Combine(layout.PrimaryRoot.FullName, skill.Name));
    }

    public IReadOnlyList<InstalledSkillRecord> GetInstalledSkills(SkillInstallLayout layout)
    {
        return catalog.Skills
            .Where(skill => IsInstalled(skill, layout))
            .Select(skill => new InstalledSkillRecord(skill, ReadInstalledVersion(skill, layout)))
            .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
    {
        destination.Create();

        foreach (var file in source.GetFiles())
        {
            var targetPath = Path.Combine(destination.FullName, file.Name);
            file.CopyTo(targetPath, overwrite: true);
        }

        foreach (var childDirectory in source.GetDirectories())
        {
            var childDestination = new DirectoryInfo(Path.Combine(destination.FullName, childDirectory.Name));
            CopyDirectory(childDirectory, childDestination);
        }
    }

    private static bool TryResolveSkill(
        IReadOnlyDictionary<string, SkillEntry> available,
        string requestedSkill,
        out SkillEntry skill)
    {
        if (available.TryGetValue(requestedSkill, out skill!))
        {
            return true;
        }

        if (!requestedSkill.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase))
        {
            return available.TryGetValue($"dotnet-{requestedSkill}", out skill!);
        }

        return false;
    }

    private static bool TryResolvePackage(
        IReadOnlyDictionary<string, SkillPackageEntry> available,
        string requestedPackage,
        out SkillPackageEntry package)
    {
        return available.TryGetValue(NormalizePackageKey(requestedPackage), out package!);
    }

    private static string NormalizePackageKey(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string ReadInstalledVersion(SkillEntry skill, SkillInstallLayout layout)
    {
        return ReadManifestValue(Path.Combine(layout.PrimaryRoot.FullName, skill.Name, "manifest.json"), "version")
            ?? ReadFrontMatterValue(Path.Combine(layout.PrimaryRoot.FullName, skill.Name, "SKILL.md"), "version")
            ?? "unknown";
    }

    private static string? ReadManifestValue(string filePath, string key)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        if (!document.RootElement.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var result = value.GetString();
        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    private static string? ReadFrontMatterValue(string filePath, string key)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var lines = File.ReadLines(filePath).ToArray();
        if (lines.Length == 0 || lines[0] != "---")
        {
            return null;
        }

        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line == "---")
            {
                break;
            }

            if (!line.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim().Trim('"');
        }

        return null;
    }
}

internal sealed record SkillInstallSummary(int InstalledCount, int GeneratedAdapters, IReadOnlyList<string> SkippedExisting);
internal sealed record SkillRemoveSummary(int RemovedCount, IReadOnlyList<string> MissingSkills);
internal sealed record InstalledSkillRecord(SkillEntry Skill, string InstalledVersion)
{
    public bool IsCurrent => string.Equals(InstalledVersion, Skill.Version, StringComparison.OrdinalIgnoreCase);
}
