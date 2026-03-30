using ManagedCode.DotnetSkills.Runtime;

namespace ManagedCode.DotnetSkills;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAsync(args);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        return ToolIdentity.IsAgentFirstTool
            ? await RunAgentToolAsync(args)
            : await RunSkillsToolAsync(args);
    }

    private static async Task<int> RunSkillsToolAsync(string[] args)
    {
        if (IsInteractiveStartup(args))
        {
            return await RunInteractiveAsync(cachePath: null);
        }

        if (IsUsageStartup(args))
        {
            return await RunUsageAsync(cachePath: null);
        }

        var command = args[0];
        if (IsVersionCommand(command))
        {
            return await RunVersionAsync(args[1..]);
        }

        return command switch
        {
            "list" => await RunListAsync(args[1..]),
            "package" => await RunPackageAsync(args[1..]),
            "recommend" => await RunRecommendAsync(args[1..]),
            "install" => await RunInstallAsync(args[1..]),
            "remove" => await RunRemoveAsync(args[1..]),
            "update" => await RunUpdateAsync(args[1..]),
            "sync" => await RunSyncAsync(args[1..]),
            "where" => await RunWhereAsync(args[1..]),
            "agent" => await RunAgentAsync(args[1..]),
            _ => UnknownCommand(command),
        };
    }

    private static async Task<int> RunAgentToolAsync(string[] args)
    {
        if (IsInteractiveStartup(args))
        {
            return await RunAgentListAsync([]);
        }

        if (IsUsageStartup(args))
        {
            return await RunUsageAsync(cachePath: null);
        }

        var command = args[0];
        if (IsVersionCommand(command))
        {
            return await RunVersionAsync(args[1..]);
        }

        return command switch
        {
            "list" => await RunAgentListAsync(args[1..]),
            "install" => await RunAgentInstallAsync(args[1..]),
            "remove" => await RunAgentRemoveAsync(args[1..]),
            "where" => await RunAgentWhereAsync(args[1..]),
            "agent" => await RunAgentAsync(args[1..]),
            _ => UnknownCommand(command),
        };
    }

    private static async Task<int> RunInteractiveAsync(string? cachePath)
    {
        var app = new InteractiveConsoleApp(cachePath: cachePath);
        return await app.RunAsync();
    }

    private static async Task<int> RunVersionAsync(string[] args)
    {
        string? cachePath = null;
        var checkLatest = true;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--no-check":
                    checkLatest = false;
                    break;
                default:
                    return UnknownCommand($"version {string.Join(' ', args)}");
            }
        }

        ToolUpdateStatusInfo? status = null;
        if (checkLatest)
        {
            status = await CreateToolUpdateService().GetStatusAsync(ResolveCacheRoot(cachePath), includeDevelopmentBuilds: true, CancellationToken.None);
        }

        ConsoleUi.RenderVersionSummary(ToolVersionInfo.CurrentVersion, status);
        return 0;
    }

    private static async Task<int> RunListAsync(string[] args)
    {
        string? targetPath = null;
        string? cachePath = null;
        string? catalogVersion = null;
        string? projectDirectory = null;
        var bundledOnly = false;
        var installedOnly = false;
        var availableOnly = false;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--catalog-version":
                    catalogVersion = ReadValue(args, ++index, "--catalog-version");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                case "--bundled":
                    bundledOnly = true;
                    break;
                case "--installed-only":
                case "--local":
                    installedOnly = true;
                    break;
                case "--available-only":
                    availableOnly = true;
                    break;
                default:
                    return UnknownCommand($"list {string.Join(' ', args)}");
            }
        }

        if (installedOnly && availableOnly)
        {
            throw new InvalidOperationException("Use either --installed-only/--local or --available-only, not both.");
        }

        await MaybeShowToolUpdateAsync(cachePath);

        var catalog = await ResolveCatalogForDisplayAsync(bundledOnly, cachePath, catalogVersion);
        var layout = SkillInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        var installer = new SkillInstaller(catalog);
        var installedSkills = installer.GetInstalledSkills(layout);
        var scopeInventory = BuildScopeInventory(layout, projectDirectory, installer, installedSkills);
        var projectRoot = layout.Scope == InstallScope.Project
            ? ResolveProjectRoot(projectDirectory)
            : null;

        ConsoleUi.RenderList(
            catalog,
            layout,
            installedSkills,
            scopeInventory,
            projectRoot,
            showInstalledSection: !availableOnly,
            showAvailableSection: !installedOnly);
        return 0;
    }

    private static async Task<int> RunRecommendAsync(string[] args)
    {
        string? targetPath = null;
        string? cachePath = null;
        string? catalogVersion = null;
        string? projectDirectory = null;
        var bundledOnly = false;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--catalog-version":
                    catalogVersion = ReadValue(args, ++index, "--catalog-version");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                case "--bundled":
                    bundledOnly = true;
                    break;
                default:
                    return UnknownCommand($"recommend {string.Join(' ', args)}");
            }
        }

        await MaybeShowToolUpdateAsync(cachePath);

        var catalog = await ResolveCatalogForDisplayAsync(bundledOnly, cachePath, catalogVersion);
        var layout = SkillInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        var installer = new SkillInstaller(catalog);
        var installedSkills = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var scanResult = new ProjectSkillRecommender(catalog).Analyze(projectDirectory);

        ConsoleUi.RenderRecommendationSummary(scanResult, catalog, layout, installedSkills);
        return 0;
    }

    private static async Task<int> RunInstallAsync(string[] args)
    {
        var requestedSkills = new List<string>();
        string? targetPath = null;
        string? cachePath = null;
        string? catalogVersion = null;
        string? projectDirectory = null;
        var installAll = false;
        var force = false;
        var bundledOnly = false;
        var refreshCatalog = false;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--all":
                    installAll = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--catalog-version":
                    catalogVersion = ReadValue(args, ++index, "--catalog-version");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                case "--bundled":
                    bundledOnly = true;
                    break;
                case "--refresh":
                    refreshCatalog = true;
                    break;
                default:
                    requestedSkills.Add(args[index]);
                    break;
            }
        }

        var packageMode = requestedSkills.Count > 0
            && string.Equals(requestedSkills[0], "package", StringComparison.OrdinalIgnoreCase);

        if (packageMode)
        {
            requestedSkills.RemoveAt(0);

            if (installAll)
            {
                throw new InvalidOperationException("`dotnet skills install package` requires explicit package names and does not support --all.");
            }

            if (requestedSkills.Count == 0)
            {
                throw new InvalidOperationException("Specify one or more package names after `dotnet skills install package`.");
            }
        }

        await MaybeShowToolUpdateAsync(cachePath);

        var catalog = await ResolveCatalogForInstallAsync(bundledOnly, cachePath, catalogVersion, refreshCatalog);
        var installer = new SkillInstaller(catalog);
        var selectedSkills = packageMode
            ? installer.SelectSkillsFromPackages(requestedSkills)
            : installer.SelectSkills(requestedSkills, installAll);
        if (ShouldUseAutoDetectedLayouts(targetPath, agent))
        {
            var batchResults = new List<SkillInstallBatchResult>();

            foreach (var layout in SkillInstallTarget.ResolveAllDetected(projectDirectory, scope))
            {
                var installedBefore = installer.GetInstalledSkills(layout)
                    .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
                var summary = installer.Install(selectedSkills, layout, force);
                var rows = BuildInstallRows(selectedSkills, installedBefore, force, summary);
                batchResults.Add(new SkillInstallBatchResult(layout, rows, summary));
            }

            ConsoleUi.RenderInstallSummaryMultiple(catalog, batchResults);
            return 0;
        }

        var singleLayout = SkillInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        var installedInSingleLayout = installer.GetInstalledSkills(singleLayout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var singleSummary = installer.Install(selectedSkills, singleLayout, force);
        var singleRows = BuildInstallRows(selectedSkills, installedInSingleLayout, force, singleSummary);

        ConsoleUi.RenderInstallSummary(catalog, singleLayout, singleRows, singleSummary);
        return 0;
    }

    private static async Task<int> RunRemoveAsync(string[] args)
    {
        var requestedSkills = new List<string>();
        string? targetPath = null;
        string? cachePath = null;
        string? catalogVersion = null;
        string? projectDirectory = null;
        var bundledOnly = false;
        var removeAll = false;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--all":
                    removeAll = true;
                    break;
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--catalog-version":
                    catalogVersion = ReadValue(args, ++index, "--catalog-version");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                case "--bundled":
                    bundledOnly = true;
                    break;
                default:
                    requestedSkills.Add(args[index]);
                    break;
            }
        }

        if (!removeAll && requestedSkills.Count == 0)
        {
            throw new InvalidOperationException("Specify one or more skills to remove, or use `dotnet skills remove --all`.");
        }

        await MaybeShowToolUpdateAsync(cachePath);

        var catalog = await ResolveCatalogForDisplayAsync(bundledOnly, cachePath, catalogVersion);
        var layout = SkillInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        var installer = new SkillInstaller(catalog);
        var installedBefore = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var selectedSkills = removeAll
            ? installedBefore.Values.Select(record => record.Skill).OrderBy(skill => skill.Name, StringComparer.Ordinal).ToArray()
            : installer.SelectSkills(requestedSkills, installAll: false);

        if (selectedSkills.Count == 0)
        {
            ConsoleUi.RenderRemoveSummary(catalog, layout, [], 0, "No catalog skills are currently installed in this target.");
            return 0;
        }

        var summary = installer.Remove(selectedSkills, layout);
        var rows = BuildRemoveRows(selectedSkills, installedBefore, summary);

        ConsoleUi.RenderRemoveSummary(catalog, layout, rows, summary.RemovedCount, null);
        return 0;
    }

    private static async Task<int> RunUpdateAsync(string[] args)
    {
        var requestedSkills = new List<string>();
        string? targetPath = null;
        string? cachePath = null;
        string? catalogVersion = null;
        string? projectDirectory = null;
        var force = false;
        var bundledOnly = false;
        var refreshCatalog = false;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--force":
                    force = true;
                    break;
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--catalog-version":
                    catalogVersion = ReadValue(args, ++index, "--catalog-version");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                case "--bundled":
                    bundledOnly = true;
                    break;
                case "--refresh":
                    refreshCatalog = true;
                    break;
                default:
                    requestedSkills.Add(args[index]);
                    break;
            }
        }

        await MaybeShowToolUpdateAsync(cachePath);

        var catalog = await ResolveCatalogForInstallAsync(bundledOnly, cachePath, catalogVersion, refreshCatalog);
        var layout = SkillInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        var installer = new SkillInstaller(catalog);
        var installedBefore = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);

        if (installedBefore.Count == 0)
        {
            ConsoleUi.RenderUpdateSummary(catalog, layout, [], 0, "No catalog skills are currently installed in this target.");
            return 0;
        }

        var selectedInstalledSkills = ResolveInstalledSkillsToUpdate(requestedSkills, installer, installedBefore);
        if (selectedInstalledSkills.Count == 0)
        {
            ConsoleUi.RenderUpdateSummary(catalog, layout, [], 0, "No matching installed catalog skills were selected for update.");
            return 0;
        }

        var updateCandidates = force
            ? selectedInstalledSkills
            : selectedInstalledSkills.Where(record => !record.IsCurrent).ToArray();

        if (updateCandidates.Count == 0)
        {
            ConsoleUi.RenderUpdateSummary(catalog, layout, [], 0, "All selected installed skills already match the selected catalog version.");
            return 0;
        }

        installer.Install(updateCandidates.Select(record => record.Skill).ToArray(), layout, force: true);

        var rows = updateCandidates
            .Select(record => new SkillActionRow(record.Skill, record.InstalledVersion, record.Skill.Version, SkillAction.Updated))
            .ToArray();

        ConsoleUi.RenderUpdateSummary(catalog, layout, rows, rows.Length, null);
        return 0;
    }

    private static async Task<int> RunWhereAsync(string[] args)
    {
        string? targetPath = null;
        string? projectDirectory = null;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                default:
                    return UnknownCommand($"where {string.Join(' ', args)}");
            }
        }

        await MaybeShowToolUpdateAsync(cachePath: null);

        var layout = SkillInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        Console.WriteLine(layout.PrimaryPath);
        return 0;
    }

    private static async Task<int> RunSyncAsync(string[] args)
    {
        string? cachePath = null;
        string? catalogVersion = null;
        var force = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--catalog-version":
                    catalogVersion = ReadValue(args, ++index, "--catalog-version");
                    break;
                case "--force":
                    force = true;
                    break;
                default:
                    return UnknownCommand($"sync {string.Join(' ', args)}");
            }
        }

        await MaybeShowToolUpdateAsync(cachePath);

        var client = CreateReleaseClient(cachePath);
        var catalog = await client.SyncAsync(catalogVersion, force, CancellationToken.None);
        ConsoleUi.RenderSyncSummary(catalog);
        return 0;
    }

    internal static async Task<SkillCatalogPackage> ResolveCatalogForDisplayAsync(bool bundledOnly, string? cachePath, string? catalogVersion)
    {
        if (bundledOnly)
        {
            return SkillCatalogPackage.LoadBundled();
        }

        var client = CreateReleaseClient(cachePath);

        try
        {
            var manifest = await client.LoadManifestAsync(catalogVersion, CancellationToken.None);
            return SkillCatalogPackage.LoadFromDirectory(
                await MaterializeManifestOnlyCatalogAsync(client.ResolveCacheRoot(), manifest, catalogVersion),
                string.IsNullOrWhiteSpace(catalogVersion) ? "latest GitHub catalog manifest" : $"GitHub catalog manifest {catalogVersion}",
                string.IsNullOrWhiteSpace(catalogVersion) ? "latest" : catalogVersion);
        }
        catch (Exception exception) when (string.IsNullOrWhiteSpace(catalogVersion))
        {
            ConsoleUi.WriteWarning($"Remote catalog unavailable: {exception.Message}");
            ConsoleUi.WriteWarning("Falling back to bundled catalog.");
            return SkillCatalogPackage.LoadBundled();
        }
    }

    internal static async Task<SkillCatalogPackage> ResolveCatalogForInstallAsync(bool bundledOnly, string? cachePath, string? catalogVersion, bool refreshCatalog)
    {
        if (bundledOnly)
        {
            return SkillCatalogPackage.LoadBundled();
        }

        var client = CreateReleaseClient(cachePath);

        try
        {
            return await client.SyncAsync(catalogVersion, refreshCatalog, CancellationToken.None);
        }
        catch (Exception exception) when (string.IsNullOrWhiteSpace(catalogVersion))
        {
            ConsoleUi.WriteWarning($"Remote catalog unavailable: {exception.Message}");
            ConsoleUi.WriteWarning("Falling back to bundled catalog.");
            return SkillCatalogPackage.LoadBundled();
        }
    }

    private static GitHubCatalogReleaseClient CreateReleaseClient(string? cachePath)
    {
        return new GitHubCatalogReleaseClient(ResolveCacheRoot(cachePath));
    }

    private static async Task<DirectoryInfo> MaterializeManifestOnlyCatalogAsync(DirectoryInfo cacheRoot, SkillManifest manifest, string? catalogVersion)
    {
        var versionSuffix = string.IsNullOrWhiteSpace(catalogVersion) ? "latest" : catalogVersion;
        var directory = new DirectoryInfo(Path.Combine(cacheRoot.FullName, ".manifest", versionSuffix));
        directory.Create();

        var catalogDirectory = new DirectoryInfo(Path.Combine(directory.FullName, "catalog"));
        var skillsDirectory = new DirectoryInfo(Path.Combine(directory.FullName, "skills"));
        catalogDirectory.Create();
        skillsDirectory.Create();

        var manifestPath = Path.Combine(catalogDirectory.FullName, "skills.json");
        await File.WriteAllTextAsync(
            manifestPath,
            System.Text.Json.JsonSerializer.Serialize(
                manifest,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        return directory;
    }

    internal static IReadOnlyList<InstalledSkillRecord> ResolveInstalledSkillsToUpdate(
        IReadOnlyList<string> requestedSkills,
        SkillInstaller installer,
        IReadOnlyDictionary<string, InstalledSkillRecord> installedBefore)
    {
        if (requestedSkills.Count == 0)
        {
            return installedBefore.Values.OrderBy(record => record.Skill.Name, StringComparer.Ordinal).ToArray();
        }

        var selectedSkills = installer.SelectSkills(requestedSkills, installAll: false);
        var notInstalled = selectedSkills
            .Where(skill => !installedBefore.ContainsKey(skill.Name))
            .Select(skill => skill.Name)
            .ToArray();

        if (notInstalled.Length > 0)
        {
            throw new InvalidOperationException($"Skill(s) are not installed in the selected target: {string.Join(", ", notInstalled)}. Use `{ToolIdentity.SkillsDisplayCommand} install ...` first.");
        }

        return selectedSkills
            .Select(skill => installedBefore[skill.Name])
            .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<SkillActionRow> BuildInstallRows(
        IReadOnlyList<SkillEntry> selectedSkills,
        IReadOnlyDictionary<string, InstalledSkillRecord> installedBefore,
        bool force,
        SkillInstallSummary summary)
    {
        var skipped = summary.SkippedExisting.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selectedSkills
            .Select(skill =>
            {
                installedBefore.TryGetValue(skill.Name, out var previous);
                var action = skipped.Contains(skill.Name)
                    ? SkillAction.Skipped
                    : previous is null
                        ? SkillAction.Installed
                        : force
                            ? SkillAction.Updated
                            : SkillAction.Installed;

                return new SkillActionRow(
                    skill,
                    previous?.InstalledVersion ?? "-",
                    skill.Version,
                    action);
            })
            .OrderBy(row => row.Skill.Name, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<SkillActionRow> BuildRemoveRows(
        IReadOnlyList<SkillEntry> selectedSkills,
        IReadOnlyDictionary<string, InstalledSkillRecord> installedBefore,
        SkillRemoveSummary summary)
    {
        var missing = summary.MissingSkills.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return selectedSkills
            .Select(skill =>
            {
                installedBefore.TryGetValue(skill.Name, out var previous);
                var action = missing.Contains(skill.Name)
                    ? SkillAction.Missing
                    : SkillAction.Removed;

                return new SkillActionRow(
                    skill,
                    previous?.InstalledVersion ?? "-",
                    "-",
                    action);
            })
            .OrderBy(row => row.Skill.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadValue(string[] args, int index, string optionName)
    {
        if (index >= args.Length)
        {
            throw new InvalidOperationException($"{optionName} requires a value");
        }

        return args[index];
    }

    private static bool IsHelpCommand(string command) =>
        string.Equals(command, "help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase);

    internal static bool IsUsageStartup(string[] args) =>
        args.Length > 0 && IsHelpCommand(args[0]);

    internal static bool IsInteractiveStartup(string[] args) => args.Length == 0;

    private static bool IsVersionCommand(string command) =>
        string.Equals(command, "version", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "--version", StringComparison.OrdinalIgnoreCase);

    internal static IReadOnlyList<ScopeInventoryRow> BuildScopeInventory(
        SkillInstallLayout currentLayout,
        string? projectDirectory,
        SkillInstaller installer,
        IReadOnlyList<InstalledSkillRecord> currentInstalledSkills)
    {
        var rows = new List<ScopeInventoryRow>
        {
            new(currentLayout.Scope, currentLayout.PrimaryRoot, currentInstalledSkills),
        };

        if (currentLayout.IsExplicitTarget || currentLayout.Agent == AgentPlatform.Auto)
        {
            return rows;
        }

        var otherScope = currentLayout.Scope == InstallScope.Project
            ? InstallScope.Global
            : InstallScope.Project;
        var otherLayout = SkillInstallTarget.Resolve(
            null,
            currentLayout.Agent,
            otherScope,
            projectDirectory);

        if (string.Equals(
                currentLayout.PrimaryRoot.FullName,
                otherLayout.PrimaryRoot.FullName,
                StringComparison.OrdinalIgnoreCase))
        {
            return rows;
        }

        rows.Add(new ScopeInventoryRow(
            otherLayout.Scope,
            otherLayout.PrimaryRoot,
            installer.GetInstalledSkills(otherLayout)));

        return rows;
    }

    internal static string ResolveProjectRoot(string? projectDirectory) => string.IsNullOrWhiteSpace(projectDirectory)
        ? Path.GetFullPath(Directory.GetCurrentDirectory())
        : Path.GetFullPath(projectDirectory);

    private static DirectoryInfo ResolveCacheRoot(string? cachePath) => string.IsNullOrWhiteSpace(cachePath)
        ? GitHubCatalogReleaseClient.ResolveDefaultCacheDirectory()
        : new DirectoryInfo(Path.GetFullPath(cachePath));

    private static bool ShouldUseAutoDetectedLayouts(string? targetPath, AgentPlatform agent) =>
        string.IsNullOrWhiteSpace(targetPath) && agent == AgentPlatform.Auto;

    private static ToolUpdateService CreateToolUpdateService() => new(new NuGetPackageVersionClient());

    internal static async Task MaybeShowToolUpdateAsync(string? cachePath)
    {
        if (ToolUpdateService.ShouldSkipAutomaticCheck())
        {
            return;
        }

        var status = await CreateToolUpdateService().GetStatusAsync(ResolveCacheRoot(cachePath), includeDevelopmentBuilds: false, CancellationToken.None);
        if (status.HasUpdate)
        {
            ConsoleUi.RenderToolUpdateNotice(status);
        }
    }

    private static async Task<int> RunUsageAsync(string? cachePath)
    {
        await MaybeShowToolUpdateAsync(cachePath);
        WriteUsage();
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        WriteUsage();
        return 1;
    }

    private static void WriteUsage() => ConsoleUi.RenderUsage();

    private static async Task<int> RunPackageAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return await RunPackageListAsync([]);
        }

        var subCommand = args[0];
        return subCommand switch
        {
            "list" => await RunPackageListAsync(args[1..]),
            "install" => await RunPackageInstallAsync(args[1..]),
            _ => UnknownCommand($"package {subCommand}"),
        };
    }

    private static async Task<int> RunPackageListAsync(string[] args)
    {
        string? cachePath = null;
        string? catalogVersion = null;
        var bundledOnly = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--cache-dir":
                    cachePath = ReadValue(args, ++index, "--cache-dir");
                    break;
                case "--catalog-version":
                    catalogVersion = ReadValue(args, ++index, "--catalog-version");
                    break;
                case "--bundled":
                    bundledOnly = true;
                    break;
                default:
                    return UnknownCommand($"package list {string.Join(' ', args)}");
            }
        }

        await MaybeShowToolUpdateAsync(cachePath);

        var catalog = await ResolveCatalogForDisplayAsync(bundledOnly, cachePath, catalogVersion);
        ConsoleUi.RenderPackageList(catalog);
        return 0;
    }

    private static Task<int> RunPackageInstallAsync(string[] args)
    {
        var forwardedArgs = new string[args.Length + 1];
        forwardedArgs[0] = "package";
        Array.Copy(args, 0, forwardedArgs, 1, args.Length);
        return RunInstallAsync(forwardedArgs);
    }

    private static async Task<int> RunAgentAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return UnknownCommand("agent (expected: list, install, remove)");
        }

        var subCommand = args[0];
        return subCommand switch
        {
            "list" => await RunAgentListAsync(args[1..]),
            "install" => await RunAgentInstallAsync(args[1..]),
            "remove" => await RunAgentRemoveAsync(args[1..]),
            _ => UnknownCommand($"agent {subCommand}"),
        };
    }

    private static async Task<int> RunAgentListAsync(string[] args)
    {
        string? targetPath = null;
        string? projectDirectory = null;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                default:
                    return UnknownCommand($"agent list {string.Join(' ', args)}");
            }
        }

        await MaybeShowToolUpdateAsync(null);

        var agentCatalog = AgentCatalogPackage.LoadBundled();
        var layout = AgentInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        var installer = new AgentInstaller(agentCatalog);
        var installedAgents = installer.GetInstalledAgents(layout);

        ConsoleUi.RenderAgentList(agentCatalog, layout, installedAgents);
        return 0;
    }

    private static async Task<int> RunAgentInstallAsync(string[] args)
    {
        var requestedAgents = new List<string>();
        string? targetPath = null;
        string? projectDirectory = null;
        var installAll = false;
        var force = false;
        var autoDetectAll = false;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--all":
                    installAll = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--auto":
                    autoDetectAll = true;
                    break;
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                default:
                    requestedAgents.Add(args[index]);
                    break;
            }
        }

        await MaybeShowToolUpdateAsync(null);

        if (autoDetectAll && !string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException("Use either --auto or --target for agent installation, not both.");
        }

        var agentCatalog = AgentCatalogPackage.LoadBundled();
        var installer = new AgentInstaller(agentCatalog);
        var selectedAgents = installer.SelectAgents(requestedAgents, installAll);

        if (selectedAgents.Count == 0)
        {
            Console.WriteLine("No agents selected for installation.");
            return 0;
        }

        AgentInstallSummary summary;
        IReadOnlyList<AgentInstallLayout> layouts;

        if (autoDetectAll)
        {
            // Install to all detected platforms
            layouts = AgentInstallTarget.ResolveAllDetected(projectDirectory, scope);
            if (layouts.Count == 0)
            {
                Console.WriteLine("No native agent platforms detected. Create a native agent directory first or use --agent/--target.");
                return 1;
            }

            summary = installer.InstallToMultiple(selectedAgents, layouts, force);
            ConsoleUi.RenderAgentInstallSummaryMultiple(agentCatalog, layouts, selectedAgents, summary);
        }
        else
        {
            // Install to single platform
            var layout = AgentInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
            summary = installer.Install(selectedAgents, layout, force);
            ConsoleUi.RenderAgentInstallSummary(agentCatalog, layout, selectedAgents, summary);
        }

        return 0;
    }

    private static async Task<int> RunAgentRemoveAsync(string[] args)
    {
        var requestedAgents = new List<string>();
        string? targetPath = null;
        string? projectDirectory = null;
        var removeAll = false;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--all":
                    removeAll = true;
                    break;
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                default:
                    requestedAgents.Add(args[index]);
                    break;
            }
        }

        if (!removeAll && requestedAgents.Count == 0)
        {
            throw new InvalidOperationException($"Specify one or more agents to remove, or use `{ToolIdentity.AgentDisplayCommand} remove --all`.");
        }

        await MaybeShowToolUpdateAsync(null);

        var agentCatalog = AgentCatalogPackage.LoadBundled();
        var layout = AgentInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        var installer = new AgentInstaller(agentCatalog);
        var installedAgents = installer.GetInstalledAgents(layout);

        var selectedAgents = removeAll
            ? installedAgents.Select(record => record.Agent).ToArray()
            : installer.SelectAgents(requestedAgents, installAll: false);

        if (selectedAgents.Count == 0)
        {
            Console.WriteLine("No agents selected for removal.");
            return 0;
        }

        var summary = installer.Remove(selectedAgents, layout);
        ConsoleUi.RenderAgentRemoveSummary(agentCatalog, layout, selectedAgents, summary);
        return 0;
    }

    private static async Task<int> RunAgentWhereAsync(string[] args)
    {
        string? targetPath = null;
        string? projectDirectory = null;
        var agent = AgentPlatform.Auto;
        var scope = InstallScope.Project;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--target":
                    targetPath = ReadValue(args, ++index, "--target");
                    break;
                case "--agent":
                    agent = SkillInstallTarget.ParseAgent(ReadValue(args, ++index, "--agent"));
                    break;
                case "--scope":
                    scope = SkillInstallTarget.ParseScope(ReadValue(args, ++index, "--scope"));
                    break;
                case "--project-dir":
                    projectDirectory = ReadValue(args, ++index, "--project-dir");
                    break;
                default:
                    return UnknownCommand($"where {string.Join(' ', args)}");
            }
        }

        await MaybeShowToolUpdateAsync(cachePath: null);

        var layout = AgentInstallTarget.Resolve(targetPath, agent, scope, projectDirectory);
        Console.WriteLine(layout.PrimaryPath);
        return 0;
    }
}
