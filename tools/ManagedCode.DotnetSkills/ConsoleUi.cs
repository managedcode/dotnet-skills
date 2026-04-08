using ManagedCode.DotnetSkills.Runtime;
using Spectre.Console;

namespace ManagedCode.DotnetSkills;

internal static class ConsoleUi
{
    public static void RenderList(
        SkillCatalogPackage catalog,
        SkillInstallLayout layout,
        IReadOnlyList<InstalledSkillRecord> installedSkills,
        IReadOnlyList<ScopeInventoryRow> scopeInventory,
        string? projectRoot,
        bool showInstalledSection,
        bool showAvailableSection)
    {
        WriteTitle("dotnet skills list");
        AnsiConsole.Write(BuildSessionPanel(catalog, layout, installedSkills.Count, projectRoot));
        AnsiConsole.WriteLine();

        if (scopeInventory.Count > 1)
        {
            AnsiConsole.Write(BuildScopeInventoryTable(scopeInventory));
            AnsiConsole.WriteLine();
        }

        if (showInstalledSection)
        {
            if (installedSkills.Count == 0)
            {
                AnsiConsole.Write(new Panel(new Markup("No catalog skills are installed in this target yet."))
                    .Header("Installed skills")
                    .Expand());
            }
            else
            {
                AnsiConsole.Write(BuildInstalledSkillsTable(installedSkills));
            }

            AnsiConsole.WriteLine();
        }

        var availableSkills = catalog.Skills
            .Where(skill => installedSkills.All(installed => !string.Equals(installed.Skill.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .ToArray();
        var renderDetailedAvailableGroups = showAvailableSection && !showInstalledSection;

        if (showAvailableSection)
        {
            if (availableSkills.Length == 0)
            {
                AnsiConsole.Write(new Panel(new Markup("All catalog skills are already installed in this target."))
                    .Header("Available catalog skills")
                    .Expand());
            }
            else
            {
                AnsiConsole.Write(BuildAvailableCategorySummaryTable(availableSkills));

                if (renderDetailedAvailableGroups)
                {
                    AnsiConsole.WriteLine();
                    RenderAvailableSkillGroups(availableSkills);
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Panel(new Markup("Use [green]dotnet skills list --available-only[/] to expand categories into per-skill tables with short summaries."))
                        .Header("Explore skills")
                        .Expand());
                }
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(BuildQuickCommandPanel(
            layout,
            scopeInventory,
            installedSkills.Select(record => record.Skill).ToArray(),
            availableSkills,
            catalog.Packages));
    }

    public static void RenderInstallSummary(
        SkillCatalogPackage catalog,
        SkillInstallLayout layout,
        IReadOnlyList<SkillActionRow> rows,
        SkillInstallSummary summary)
    {
        WriteTitle("dotnet skills install");
        AnsiConsole.Write(BuildOperationPanel(
            catalog,
            layout,
            summary.InstalledCount,
            summary.SkippedExisting.Count,
            summary.GeneratedAdapters));
        AnsiConsole.WriteLine();

        if (rows.Count > 0)
        {
            AnsiConsole.Write(BuildOperationTable("Install results", rows));
            AnsiConsole.WriteLine();
        }

        if (summary.SkippedExisting.Count > 0)
        {
            AnsiConsole.Write(new Panel(new Markup(Escape(string.Join(", ", summary.SkippedExisting))))
                .Header("Skipped existing")
                .Expand());
            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("Next step").Expand());
    }

    public static void RenderInstallSummaryMultiple(
        SkillCatalogPackage catalog,
        IReadOnlyList<SkillInstallBatchResult> results)
    {
        WriteTitle("dotnet skills install");

        var totalWritten = results.Sum(result => result.Summary.InstalledCount);
        var totalSkipped = results.Sum(result => result.Summary.SkippedExisting.Count);
        var totalAdapters = results.Sum(result => result.Summary.GeneratedAdapters);

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [grey]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[grey]Targets[/]"), new Markup(results.Count.ToString()));
        grid.AddRow(new Markup("[grey]Written[/]"), new Markup(totalWritten.ToString()));
        grid.AddRow(new Markup("[grey]Skipped[/]"), new Markup(totalSkipped.ToString()));

        if (totalAdapters > 0)
        {
            grid.AddRow(new Markup("[grey]Generated adapters[/]"), new Markup(totalAdapters.ToString()));
        }

        AnsiConsole.Write(new Panel(grid).Header("Summary").Expand());
        AnsiConsole.WriteLine();

        var targetTable = new Table().Expand();
        targetTable.Title = new TableTitle("Install targets");
        targetTable.AddColumn("Platform");
        targetTable.AddColumn("Mode");
        targetTable.AddColumn("Path");
        targetTable.AddColumn("Written");
        targetTable.AddColumn("Skipped");

        foreach (var result in results)
        {
            targetTable.AddRow(
                Escape(result.Layout.Agent.ToString()),
                FormatInstallMode(result.Layout.Mode),
                Escape(result.Layout.PrimaryRoot.FullName),
                result.Summary.InstalledCount.ToString(),
                result.Summary.SkippedExisting.Count.ToString());
        }

        AnsiConsole.Write(targetTable);
        AnsiConsole.WriteLine();

        var resultTable = new Table().Expand();
        resultTable.Title = new TableTitle("Install results");
        resultTable.AddColumn("Target");
        resultTable.AddColumn("Skill");
        resultTable.AddColumn("Action");

        foreach (var result in results)
        {
            foreach (var row in result.Rows.OrderBy(item => item.Skill.Name, StringComparer.Ordinal))
            {
                resultTable.AddRow(
                    Escape(result.Layout.PrimaryRoot.FullName),
                    BuildSkillCell(row.Skill),
                    FormatAction(row.Action));
            }
        }

        AnsiConsole.Write(resultTable);
        AnsiConsole.WriteLine();

        var hints = string.Join(
            Environment.NewLine,
            results.Select(result => $"{result.Layout.Agent} [{result.Layout.PrimaryRoot.FullName}] : {result.Layout.ReloadHint}"));
        AnsiConsole.Write(new Panel(new Markup(Escape(hints))).Header("Next steps").Expand());
    }

    public static void RenderRemoveSummary(
        SkillCatalogPackage catalog,
        SkillInstallLayout layout,
        IReadOnlyList<SkillActionRow> rows,
        int removedCount,
        string? statusMessage)
    {
        WriteTitle("dotnet skills remove");
        AnsiConsole.Write(BuildOperationPanel(catalog, layout, removedCount, rows.Count(row => row.Action == SkillAction.Missing), generatedAdapters: 0));
        AnsiConsole.WriteLine();

        if (rows.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup(Escape(statusMessage ?? "No matching catalog skills were removed.")))
                .Header("Status")
                .Expand());
        }
        else
        {
            AnsiConsole.Write(BuildOperationTable("Remove results", rows));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("Next step").Expand());
    }

    public static void RenderUpdateSummary(
        SkillCatalogPackage catalog,
        SkillInstallLayout layout,
        IReadOnlyList<SkillActionRow> rows,
        int updatedCount,
        string? statusMessage)
    {
        WriteTitle("dotnet skills update");
        AnsiConsole.Write(BuildOperationPanel(catalog, layout, updatedCount, skippedCount: 0, generatedAdapters: 0));
        AnsiConsole.WriteLine();

        if (rows.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup(Escape(statusMessage ?? "All installed catalog skills already match the selected catalog version.")))
                .Header("Status")
                .Expand());
        }
        else
        {
            AnsiConsole.Write(BuildOperationTable("Updated skills", rows));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("Next step").Expand());
    }

    public static void RenderRecommendationSummary(
        ProjectScanResult scanResult,
        SkillCatalogPackage catalog,
        SkillInstallLayout layout,
        IReadOnlyDictionary<string, InstalledSkillRecord> installedSkills)
    {
        WriteTitle("dotnet skills recommend");
        AnsiConsole.Write(BuildRecommendationPanel(scanResult, catalog, layout));
        AnsiConsole.WriteLine();

        if (scanResult.Recommendations.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup("No direct package or project-signal matches were found. Nothing was installed. Start with [bold]dotnet[/] and [bold]dotnet-modern-csharp[/] if you want a baseline .NET skill set."))
                .Header("Recommendations")
                .Expand());
            return;
        }

        var table = new Table().Expand();
        table.Title = new TableTitle("Recommendations");
        table.AddColumn("Skill");
        table.AddColumn("Confidence");
        table.AddColumn("Auto");
        table.AddColumn("Status");
        table.AddColumn("Signals");

        foreach (var recommendation in scanResult.Recommendations)
        {
            installedSkills.TryGetValue(recommendation.Skill.Name, out var installed);
            table.AddRow(
                BuildSkillCell(recommendation.Skill),
                FormatConfidence(recommendation.Confidence),
                FormatAutoSyncCandidate(recommendation),
                FormatRecommendationStatus(installed),
                Escape(string.Join(Environment.NewLine, recommendation.Reasons)));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var installableSkills = scanResult.Recommendations
            .Where(recommendation => !installedSkills.TryGetValue(recommendation.Skill.Name, out var installed) || !installed.IsCurrent)
            .Select(recommendation => ToAlias(recommendation.Skill.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();
        var autoInstallableSkills = scanResult.Recommendations
            .Where(recommendation => recommendation.IsAutoInstallCandidate)
            .Select(recommendation => ToAlias(recommendation.Skill.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        if (installableSkills.Length > 0 || autoInstallableSkills.Length > 0)
        {
            var lines = new List<string>
            {
                "Scan complete. Nothing is installed automatically unless you opt into auto mode.",
                "Use the manual install command when you want exact control, or auto mode when you want package- and app-model-driven syncing."
            };

            if (installableSkills.Length > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Manual install:");
                lines.Add($"[green]{Escape($"dotnet skills install {string.Join(' ', installableSkills)}")}[/]");
            }

            if (autoInstallableSkills.Length > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Auto-manage project-matched skills:");
                lines.Add($"[green]{Escape("dotnet skills install --auto")}[/]");
                lines.Add($"[green]{Escape("dotnet skills install --auto --prune")}[/]");
                lines.Add("[grey]`--prune` removes stale auto-managed skills but keeps protected diagnostics and graphify-dotnet.[/]");
            }

            AnsiConsole.Write(new Panel(new Markup(string.Join(Environment.NewLine, lines)))
                .Header("Review and confirm")
                .Expand());
        }
    }

    public static void RenderAutoSyncSummary(
        SkillCatalogPackage catalog,
        SkillInstallLayout layout,
        ProjectSkillAutoSyncPlan plan,
        IReadOnlyList<SkillActionRow> installRows,
        SkillInstallSummary installSummary,
        IReadOnlyList<SkillActionRow> removeRows,
        SkillRemoveSummary removeSummary,
        bool pruneRequested)
    {
        WriteTitle("dotnet skills install --auto");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [grey]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[grey]Project root[/]"), new Markup(Escape(plan.ScanResult.ProjectRoot.FullName)));
        grid.AddRow(new Markup("[grey]Target[/]"), new Markup(Escape(layout.PrimaryRoot.FullName)));
        grid.AddRow(new Markup("[grey]Projects scanned[/]"), new Markup(plan.ScanResult.ProjectFiles.Count.ToString()));
        grid.AddRow(new Markup("[grey]Auto-managed matches[/]"), new Markup(plan.DesiredSkills.Count.ToString()));
        grid.AddRow(new Markup("[grey]Written[/]"), new Markup(installSummary.InstalledCount.ToString()));
        grid.AddRow(new Markup("[grey]Skipped[/]"), new Markup(installSummary.SkippedExisting.Count.ToString()));

        if (pruneRequested)
        {
            grid.AddRow(new Markup("[grey]Removed stale[/]"), new Markup(removeSummary.RemovedCount.ToString()));
            grid.AddRow(new Markup("[grey]Protected kept[/]"), new Markup(plan.ProtectedStaleSkills.Count.ToString()));
        }

        AnsiConsole.Write(new Panel(grid).Header("Auto sync").Expand());
        AnsiConsole.WriteLine();

        if (installRows.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup(
                    "No auto-installable skills matched the detected project signals."
                    + Environment.NewLine
                    + "Manual baseline skills such as [bold]dotnet[/] and [bold]dotnet-modern-csharp[/] remain opt-in."))
                .Header("Install results")
                .Expand());
        }
        else
        {
            AnsiConsole.Write(BuildOperationTable("Auto-managed skills", installRows));
        }

        AnsiConsole.WriteLine();

        if (pruneRequested)
        {
            if (!plan.MatchedPreviousProject)
            {
                AnsiConsole.Write(new Panel(new Markup(
                        "No previous auto-managed state matched this project root, so prune did not remove anything."
                        + Environment.NewLine
                        + "Run [green]dotnet skills install --auto[/] once first, then use [green]--prune[/] on later syncs."))
                    .Header("Prune state")
                    .Expand());
            }
            else if (removeRows.Count == 0)
            {
                AnsiConsole.Write(new Panel(new Markup("No stale auto-managed skills needed removal."))
                    .Header("Prune results")
                    .Expand());
            }
            else
            {
                AnsiConsole.Write(BuildOperationTable("Pruned skills", removeRows));
            }

            if (plan.ProtectedStaleSkills.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(new Markup(
                        Escape(string.Join(", ", plan.ProtectedStaleSkills
                            .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                            .Select(skill => ToAlias(skill.Name))))))
                    .Header("Protected skills kept")
                    .Expand());
            }

            AnsiConsole.WriteLine();
        }

        var nextSteps = new List<string>
        {
            layout.ReloadHint,
            "Use [green]dotnet skills recommend[/] when you want to inspect the scan before changing installs."
        };

        if (!pruneRequested)
        {
            nextSteps.Add("Use [green]dotnet skills install --auto --prune[/] on later runs to remove stale auto-managed skills.");
        }

        AnsiConsole.Write(new Panel(new Markup(string.Join(Environment.NewLine, nextSteps)))
            .Header("Next steps")
            .Expand());
    }

    public static void RenderSyncSummary(SkillCatalogPackage catalog)
    {
        WriteTitle("dotnet skills sync");
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Catalog[/]"), new Markup(Escape(catalog.CatalogVersion)));
        grid.AddRow(new Markup("[grey]Source[/]"), new Markup(Escape(catalog.SourceLabel)));
        grid.AddRow(new Markup("[grey]Cache[/]"), new Markup(Escape(catalog.CatalogRoot.FullName)));
        AnsiConsole.Write(new Panel(grid).Header("Synced catalog").Expand());
    }

    public static void RenderVersionSummary(string currentVersion, ToolUpdateStatusInfo? status)
    {
        WriteTitle($"{ToolIdentity.DisplayCommand} version");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Package[/]"), new Markup(ToolIdentity.PackageId));
        grid.AddRow(new Markup("[grey]Current[/]"), new Markup(Escape(currentVersion)));
        grid.AddRow(new Markup("[grey]Build[/]"), new Markup(ToolVersionInfo.IsDevelopmentBuild ? "local development build" : "published tool build"));

        if (status is not null)
        {
            grid.AddRow(new Markup("[grey]Latest NuGet[/]"), new Markup(Escape(status.LatestVersion ?? "unknown")));
            grid.AddRow(new Markup("[grey]Status[/]"), new Markup(FormatToolUpdateState(status.State)));

            if (status.CheckedAt is not null)
            {
                grid.AddRow(
                    new Markup("[grey]Checked[/]"),
                    new Markup($"{Escape(status.CheckedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"))}{(status.UsedCachedValue ? " [grey](cached)[/]" : string.Empty)}"));
            }
        }

        AnsiConsole.Write(new Panel(grid).Header("Version").Expand());

        if (status?.HasUpdate == true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(BuildUpdateCommandPanel(status));
        }
    }

    public static void RenderPackageList(SkillCatalogPackage catalog)
    {
        WriteTitle("dotnet skills package list");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [grey]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[grey]Packages[/]"), new Markup(catalog.Packages.Count.ToString()));
        grid.AddRow(new Markup("[grey]Skills covered[/]"), new Markup(catalog.Skills.Count.ToString()));
        AnsiConsole.Write(new Panel(grid).Header("Package catalog").Expand());
        AnsiConsole.WriteLine();

        if (catalog.Packages.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup("No packages are available in this catalog version yet."))
                .Header("Packages")
                .Expand());
            return;
        }

        var table = new Table().Expand();
        table.Title = new TableTitle("Available skill packages");
        table.AddColumn("Package");
        table.AddColumn("Type");
        table.AddColumn("Skills");
        table.AddColumn("Install");
        table.AddColumn("Includes");

        foreach (var package in catalog.Packages.OrderBy(FormatPackageSortKey, StringComparer.Ordinal))
        {
            var skillAliases = package.Skills
                .Select(ToAlias)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            table.AddRow(
                $"[bold]{Escape(package.Name)}[/]",
                Escape(FormatPackageKind(package)),
                skillAliases.Length.ToString(),
                Escape($"dotnet skills install package {package.Name}"),
                Escape(string.Join(", ", skillAliases.Take(4)))
                    + (skillAliases.Length > 4 ? $" [grey](+{skillAliases.Length - 4} more)[/]" : string.Empty));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var suggested = catalog.Packages
            .OrderBy(FormatPackageSortKey, StringComparer.Ordinal)
            .Take(3)
            .Select(package => $"dotnet skills install package {package.Name}")
            .ToArray();

        AnsiConsole.Write(new Panel(new Markup(
                "Install a package when you want one command to expand into a related skill set."
                + Environment.NewLine
                + Environment.NewLine
                + string.Join(Environment.NewLine, suggested.Select(command => $"[green]{Escape(command)}[/]"))))
            .Header("Quick commands")
            .Expand());
    }

    public static void RenderUsage()
    {
        if (ToolIdentity.IsAgentFirstTool)
        {
            RenderAgentToolUsage();
            return;
        }

        WriteTitle(ToolIdentity.PackageId);

        var table = new Table().Expand();
        table.AddColumn("Command");
        table.AddColumn("Purpose");
        table.AddRow($"[green]{Escape(ToolIdentity.DisplayCommand)}[/]", "Launch the interactive catalog shell for browsing, installing, removing, and updating skills or agents.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.DisplayCommand} help")}[/]", "Render the direct command reference without entering the interactive shell.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} list")}[/]", "Show the current inventory, compare project/global scope when relevant, and render available skills in grouped category tables.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} package list")}[/]", "List curated and category-based packages that expand into multiple related skills.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.DisplayCommand} version")}[/]", "Show the current tool version and check whether NuGet has a newer release.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} recommend")}[/]", "Scan `*.csproj` files, propose relevant `dotnet-*` skills, and let you decide what to install.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} install aspire orleans")}[/]", "Install one or more skills by slug or short alias.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} install --auto")}[/]", "Scan the project and install skills that match detected packages or strong app-model signals.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} install --auto --prune")}[/]", "Reconcile auto-managed skills with the current project and remove stale ones from the project scope.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} install package ai")}[/]", "Install a package that expands into a related multi-skill set.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} package install orleans")}[/]", "Alias for package installation when you prefer the package-first command shape.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} remove --all")}[/]", "Remove installed catalog skills from the selected target.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} update")}[/]", "Refresh already installed catalog skills to the selected catalog version.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} sync --force")}[/]", "Refresh the cached remote catalog payload.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.SkillsDisplayCommand} where")}[/]", "Print the resolved install path.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} list")}[/]", "List available orchestration agents.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install router ai")}[/]", "Install orchestration agents by name.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install --all --auto")}[/]", "Install all agents to all detected platforms.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install router --target /path/to/agents")}[/]", "Install agents to an explicit custom target.");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var notes = string.Join(
            Environment.NewLine,
            "- `list`, `package list`, `recommend`, `install`, and `update` use the latest `catalog-v*` GitHub release by default.",
            $"- Bare `{ToolIdentity.DisplayCommand}` starts the interactive shell; use `{ToolIdentity.DisplayCommand} help` when you want the direct command reference only.",
            $"- `{ToolIdentity.DisplayCommand} version` and `{ToolIdentity.DisplayCommand} --version` both show the current tool version.",
            "- `help` and the interactive startup path both run the automatic tool update check unless it is suppressed.",
            $"- Use `{ToolIdentity.DisplayCommand} version --no-check` when you only want the local installed version without a NuGet lookup.",
            "- `list` stays compact: it shows the current installed inventory and a grouped category summary for the remaining catalog.",
            "- `list --installed-only` and `list --local` are equivalent shortcuts for the installed inventory view; `list --available-only` expands the remaining catalog into per-category skill tables with short summaries.",
            "- `--bundled` skips the network and uses the catalog packaged with the tool.",
            "- `--catalog-version <version>` pins a specific remote catalog release.",
            "- `--refresh` forces `install` or `update` to redownload the selected remote catalog first.",
            "- `install --auto` uses package, SDK, and strong project-property signals from local `*.csproj` files to install matching skills.",
            "- `install --auto --prune` is project-scope only and removes stale auto-managed skills while keeping protected diagnostics and `graphify-dotnet`.",
            "- Short aliases work everywhere: `aspire` resolves to `dotnet-aspire`.",
            $"- Package installs expand into multiple skills. Example: `{ToolIdentity.SkillsDisplayCommand} install package code-quality`.",
            $"- Set `{ToolIdentity.SkipUpdateEnvironmentVariable}=1` to suppress automatic tool update notices on startup.",
            "- Auto skill target detection probes `.codex`, `.claude`, `.github`, `.gemini`, and `.junie`; it writes to every existing native platform target it finds, and falls back to `.agents/skills` only when no native platform folder exists.",
            "- Agent auto-detect uses only native agent roots. If none exist yet, specify `--agent` or `--target`.");

        AnsiConsole.Write(new Panel(new Markup(Escape(notes))).Header("Notes").Expand());
    }

    private static void RenderAgentToolUsage()
    {
        WriteTitle(ToolIdentity.PackageId);

        var table = new Table().Expand();
        table.AddColumn("Command");
        table.AddColumn("Purpose");
        table.AddRow($"[green]{Escape(ToolIdentity.DisplayCommand)}[/]", "List bundled orchestration agents and show the current install target.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.DisplayCommand} help")}[/]", "Render the direct command reference for the agent-only tool.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} list")}[/]", "List available orchestration agents.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install router ai")}[/]", "Install one or more orchestration agents by name.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install --all --auto")}[/]", "Install all agents to every detected native agent directory.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} remove router")}[/]", "Remove one or more installed orchestration agents.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} where --agent codex")}[/]", "Print the resolved native agent install path.");
        table.AddRow($"[green]{Escape($"{ToolIdentity.DisplayCommand} version")}[/]", $"Show the current `{ToolIdentity.PackageId}` version and check whether NuGet has a newer release.");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var notes = string.Join(
            Environment.NewLine,
            $"- `{ToolIdentity.DisplayCommand}` is the dedicated agent-only CLI. It does not install skills.",
            $"- Use `{ToolIdentity.SkillsDisplayCommand} ...` when you want catalog skills or package installs.",
            $"- `{ToolIdentity.DisplayCommand} list` and bare `{ToolIdentity.DisplayCommand}` both show the bundled agent catalog.",
            $"- `{ToolIdentity.DisplayCommand} version` and `{ToolIdentity.DisplayCommand} --version` both show the current tool version.",
            $"- Set `{ToolIdentity.SkipUpdateEnvironmentVariable}=1` to suppress automatic tool update notices on startup.",
            "- Agent auto-detect uses only native agent roots. If none exist yet, specify `--agent` or `--target`.",
            "- Explicit `--target` still requires `--agent`, because the generated file format depends on the selected platform.");

        AnsiConsole.Write(new Panel(new Markup(Escape(notes))).Header("Notes").Expand());
    }

    public static void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Escape(message)}");
    }

    public static void RenderToolUpdateNotice(ToolUpdateStatusInfo status)
    {
        if (!status.HasUpdate)
        {
            return;
        }

        Console.Error.WriteLine($"Tool update available: current {status.CurrentVersion}, latest {status.LatestVersion}.");
        Console.Error.WriteLine($"Update: {GlobalToolUpdateCommand}");
        Console.Error.WriteLine($"If installed via a local tool manifest: {LocalToolUpdateCommand}");
    }

    private static void WriteTitle(string title)
    {
        AnsiConsole.Write(new Rule($"[deepskyblue1]{Escape(title)}[/]"));
    }

    private static Panel BuildSessionPanel(SkillCatalogPackage catalog, SkillInstallLayout layout, int installedCount, string? projectRoot)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [grey]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[grey]Agent[/]"), new Markup(Escape(layout.Agent.ToString())));
        grid.AddRow(new Markup("[grey]Scope[/]"), new Markup(Escape(layout.Scope.ToString())));
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            grid.AddRow(new Markup("[grey]Project root[/]"), new Markup(Escape(projectRoot)));
        }

        grid.AddRow(new Markup("[grey]Target[/]"), new Markup(Escape(layout.PrimaryRoot.FullName)));
        grid.AddRow(new Markup("[grey]Mode[/]"), new Markup(FormatInstallMode(layout.Mode)));
        grid.AddRow(new Markup("[grey]Installed[/]"), new Markup($"{installedCount} of {catalog.Skills.Count} catalog skills"));
        return new Panel(grid).Header("Session").Expand();
    }

    private static Table BuildScopeInventoryTable(IReadOnlyList<ScopeInventoryRow> scopeInventory)
    {
        var table = new Table().Expand();
        table.Title = new TableTitle("Scope inventory");
        table.AddColumn("Scope");
        table.AddColumn("Installed");
        table.AddColumn("Target");
        table.AddColumn("Skills");

        foreach (var row in scopeInventory)
        {
            var aliases = row.InstalledSkills.Count == 0
                ? "[grey]none[/]"
                : Escape(string.Join(", ", row.InstalledSkills
                    .OrderBy(skill => skill.Skill.Name, StringComparer.Ordinal)
                    .Take(8)
                    .Select(skill => ToAlias(skill.Skill.Name))))
                    + (row.InstalledSkills.Count > 8 ? $" [grey](+{row.InstalledSkills.Count - 8} more)[/]" : string.Empty);

            table.AddRow(
                row.Scope == InstallScope.Project ? "[bold]Project[/]" : "[bold]Global[/]",
                row.InstalledSkills.Count.ToString(),
                Escape(row.TargetRoot.FullName),
                aliases);
        }

        return table;
    }

    private static Panel BuildOperationPanel(
        SkillCatalogPackage catalog,
        SkillInstallLayout layout,
        int writtenCount,
        int skippedCount,
        int generatedAdapters)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [grey]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[grey]Target[/]"), new Markup(Escape(layout.PrimaryRoot.FullName)));
        grid.AddRow(new Markup("[grey]Written[/]"), new Markup(writtenCount.ToString()));
        grid.AddRow(new Markup("[grey]Skipped[/]"), new Markup(skippedCount.ToString()));

        if (generatedAdapters > 0)
        {
            grid.AddRow(new Markup("[grey]Generated adapters[/]"), new Markup(generatedAdapters.ToString()));
        }

        return new Panel(grid).Header("Summary").Expand();
    }

    private static Panel BuildUpdateCommandPanel(ToolUpdateStatusInfo status)
    {
        var lines = string.Join(
            Environment.NewLine,
            $"A newer [bold]{Escape(status.LatestVersion ?? "unknown")}[/] release is available on NuGet. Current: [bold]{Escape(status.CurrentVersion)}[/].",
            string.Empty,
            "Global tool:",
            $"[green]{Escape(GlobalToolUpdateCommand)}[/]",
            string.Empty,
            "Local tool manifest:",
            $"[green]{Escape(LocalToolUpdateCommand)}[/]");

        return new Panel(new Markup(lines)).Expand();
    }

    private static Panel BuildRecommendationPanel(ProjectScanResult scanResult, SkillCatalogPackage catalog, SkillInstallLayout layout)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Project root[/]"), new Markup(Escape(scanResult.ProjectRoot.FullName)));
        grid.AddRow(new Markup("[grey]Projects[/]"), new Markup(scanResult.ProjectFiles.Count.ToString()));
        grid.AddRow(new Markup("[grey]Frameworks[/]"), new Markup(scanResult.TargetFrameworks.Count == 0 ? "unknown" : Escape(string.Join(", ", scanResult.TargetFrameworks))));
        grid.AddRow(new Markup("[grey]Auto-manageable[/]"), new Markup(scanResult.Recommendations.Count(recommendation => recommendation.IsAutoInstallCandidate).ToString()));
        grid.AddRow(new Markup("[grey]Catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [grey]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[grey]Install target[/]"), new Markup(Escape(layout.PrimaryRoot.FullName)));
        return new Panel(grid).Header("Scan").Expand();
    }

    private static Table BuildInstalledSkillsTable(IReadOnlyList<InstalledSkillRecord> installedSkills)
    {
        var table = new Table().Expand();
        table.Title = new TableTitle("Installed skills");
        table.AddColumn("Alias");
        table.AddColumn("Installed");
        table.AddColumn("Latest");
        table.AddColumn("Status");
        table.AddColumn("Category");

        foreach (var record in installedSkills.OrderBy(item => item.Skill.Name, StringComparer.Ordinal))
        {
            table.AddRow(
                $"[bold]{Escape(ToAlias(record.Skill.Name))}[/]",
                Escape(record.InstalledVersion),
                Escape(record.Skill.Version),
                record.IsCurrent ? "[green]Current[/]" : "[yellow]Update available[/]",
                Escape(record.Skill.Category));
        }

        return table;
    }

    private static Table BuildAvailableCategorySummaryTable(IReadOnlyList<SkillEntry> availableSkills)
    {
        var table = new Table().Expand();
        table.Title = new TableTitle("Available catalog skills");
        table.AddColumn("Category");
        table.AddColumn("Available");
        table.AddColumn("Examples");

        foreach (var group in availableSkills
                     .GroupBy(skill => skill.Category, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var aliases = group
                .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Take(4)
                .Select(skill => ToAlias(skill.Name))
                .ToArray();

            table.AddRow(
                Escape(group.Key),
                group.Count().ToString(),
                Escape(string.Join(", ", aliases)) + (group.Count() > aliases.Length ? $" [grey](+{group.Count() - aliases.Length} more)[/]" : string.Empty));
        }

        return table;
    }

    private static void RenderAvailableSkillGroups(IReadOnlyList<SkillEntry> availableSkills)
    {
        foreach (var group in availableSkills
                     .GroupBy(skill => skill.Category, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var table = new Table().Expand();
            table.Title = new TableTitle($"{group.Key} skills");
            table.AddColumn("Alias");
            table.AddColumn("Skill");
            table.AddColumn("Summary");

            foreach (var skill in group.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                table.AddRow(
                    $"[bold]{Escape(ToAlias(skill.Name))}[/]",
                    Escape(skill.Name),
                    Escape(CompactDescription(skill.Description)));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    private static Table BuildOperationTable(string title, IReadOnlyList<SkillActionRow> rows)
    {
        var table = new Table().Expand();
        table.Title = new TableTitle(title);
        table.AddColumn("Skill");
        table.AddColumn("From");
        table.AddColumn("To");
        table.AddColumn("Action");
        table.AddColumn("Description");

        foreach (var row in rows.OrderBy(item => item.Skill.Name, StringComparer.Ordinal))
        {
            table.AddRow(
                BuildSkillCell(row.Skill),
                Escape(row.FromVersion),
                Escape(row.ToVersion),
                FormatAction(row.Action),
                Escape(CompactDescription(row.Skill.Description)));
        }

        return table;
    }

    private static Panel BuildQuickCommandPanel(
        SkillInstallLayout layout,
        IReadOnlyList<ScopeInventoryRow> scopeInventory,
        IReadOnlyList<SkillEntry> installedSkills,
        IReadOnlyList<SkillEntry> availableSkills,
        IReadOnlyList<SkillPackageEntry> packages)
    {
        var lines = new List<string>();

        if (installedSkills.Count > 0)
        {
            lines.Add($"Update installed skills:{Environment.NewLine}[green]{Escape($"dotnet skills update {string.Join(' ', installedSkills.Take(3).Select(skill => ToAlias(skill.Name)))}")}[/]");
            lines.Add($"Remove installed skills:{Environment.NewLine}[green]{Escape("dotnet skills remove --all")}[/]");
        }

        if (availableSkills.Count > 0)
        {
            lines.Add($"Install more skills:{Environment.NewLine}[green]{Escape($"dotnet skills install {string.Join(' ', availableSkills.Take(3).Select(skill => ToAlias(skill.Name)))}")}[/]");
        }

        if (packages.Count > 0)
        {
            var featuredPackage = packages.OrderBy(FormatPackageSortKey, StringComparer.Ordinal).First();
            lines.Add($"Install a package:{Environment.NewLine}[green]{Escape($"dotnet skills install package {featuredPackage.Name}")}[/]");
            lines.Add($"Browse all packages:{Environment.NewLine}[green]{Escape("dotnet skills package list")}[/]");
        }

        lines.Add($"Auto-sync package-matched skills:{Environment.NewLine}[green]{Escape("dotnet skills install --auto")}[/]");
        lines.Add($"Auto-sync and prune stale project skills:{Environment.NewLine}[green]{Escape("dotnet skills install --auto --prune")}[/]");

        var alternateScope = scopeInventory.FirstOrDefault(row => row.Scope != layout.Scope);
        if (alternateScope is not null)
        {
            var command = alternateScope.Scope == InstallScope.Global
                ? "dotnet skills list --scope global"
                : "dotnet skills list --scope project";
            lines.Add($"Check the {alternateScope.Scope.ToString().ToLowerInvariant()} inventory:{Environment.NewLine}[green]{Escape(command)}[/]");
        }

        lines.Add($"Get dependency-based recommendations:{Environment.NewLine}[green]{Escape("dotnet skills recommend")}[/]");
        lines.Add($"Refresh the remote cache:{Environment.NewLine}[green]{Escape("dotnet skills sync --force")}[/]");

        return new Panel(new Markup(string.Join($"{Environment.NewLine}{Environment.NewLine}", lines)))
            .Header("Quick commands")
            .Expand();
    }

    private static string BuildSkillCell(SkillEntry skill)
    {
        return $"[bold]{Escape(skill.Name)}[/] [dim]({Escape(ToAlias(skill.Name))})[/]";
    }

    private static string FormatAction(SkillAction action) => action switch
    {
        SkillAction.Installed => "[green]Installed[/]",
        SkillAction.Removed => "[red]Removed[/]",
        SkillAction.Updated => "[yellow]Updated[/]",
        SkillAction.Missing => "[grey]Missing[/]",
        SkillAction.Skipped => "[grey]Skipped[/]",
        _ => Escape(action.ToString()),
    };

    private static string FormatConfidence(RecommendationConfidence confidence) => confidence switch
    {
        RecommendationConfidence.High => "[green]High[/]",
        RecommendationConfidence.Medium => "[yellow]Medium[/]",
        RecommendationConfidence.Low => "[grey]Low[/]",
        _ => Escape(confidence.ToString()),
    };

    private static string FormatAutoSyncCandidate(ProjectSkillRecommendation recommendation)
    {
        return recommendation.IsAutoInstallCandidate
            ? "[green]Yes[/]"
            : "[grey]Manual only[/]";
    }

    private static string FormatRecommendationStatus(InstalledSkillRecord? installed)
    {
        if (installed is null)
        {
            return "[blue]Not installed[/]";
        }

        return installed.IsCurrent
            ? $"[green]Installed {Escape(installed.InstalledVersion)}[/]"
            : $"[yellow]Update to {Escape(installed.Skill.Version)}[/]";
    }

    private static string FormatToolUpdateState(ToolUpdateState state) => state switch
    {
        ToolUpdateState.Current => "[green]Current[/]",
        ToolUpdateState.UpdateAvailable => "[yellow]Update available[/]",
        ToolUpdateState.DevelopmentBuild => "[grey]Local development build[/]",
        ToolUpdateState.Unknown => "[grey]Latest version unavailable[/]",
        _ => Escape(state.ToString()),
    };

    private static string FormatInstallMode(SkillInstallMode mode) => mode switch
    {
        SkillInstallMode.SkillDirectories => "Skill directories",
        _ => Escape(mode.ToString()),
    };

    private static string CompactDescription(string description)
    {
        const string useWhenMarker = ". Use when";
        var markerIndex = description.IndexOf(useWhenMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            return description[..(markerIndex + 1)];
        }

        var firstSentenceIndex = description.IndexOf(". ", StringComparison.Ordinal);
        if (firstSentenceIndex >= 0)
        {
            return description[..(firstSentenceIndex + 1)];
        }

        return description.Length <= 100
            ? description
            : $"{description[..97]}...";
    }

    private static string Escape(string value) => Markup.Escape(value);

    private static string ToAlias(string skillName) => skillName.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase)
        ? skillName["dotnet-".Length..]
        : skillName;

    private static string FormatPackageKind(SkillPackageEntry package)
    {
        if (string.Equals(package.Kind, "category", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(package.SourceCategory))
        {
            return $"Category: {package.SourceCategory}";
        }

        return string.Equals(package.Kind, "curated", StringComparison.OrdinalIgnoreCase)
            ? "Curated"
            : package.Kind;
    }

    private static string FormatPackageSortKey(SkillPackageEntry package)
    {
        var rank = string.Equals(package.Kind, "curated", StringComparison.OrdinalIgnoreCase) ? "0" : "1";
        return $"{rank}:{package.Name}";
    }

    private static string GlobalToolUpdateCommand => $"dotnet tool update --global {ToolIdentity.PackageId}";

    private static string LocalToolUpdateCommand => $"dotnet tool update {ToolIdentity.PackageId}";

    public static void RenderAgentList(
        AgentCatalogPackage catalog,
        AgentInstallLayout layout,
        IReadOnlyList<InstalledAgentRecord> installedAgents)
    {
        WriteTitle($"{ToolIdentity.AgentDisplayCommand} list");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Catalog agents[/]"), new Markup(catalog.Agents.Count.ToString()));
        grid.AddRow(new Markup("[grey]Agent platform[/]"), new Markup(Escape(layout.Agent.ToString())));
        grid.AddRow(new Markup("[grey]Scope[/]"), new Markup(Escape(layout.Scope.ToString())));
        grid.AddRow(new Markup("[grey]Target[/]"), new Markup(Escape(layout.PrimaryRoot.FullName)));
        grid.AddRow(new Markup("[grey]Mode[/]"), new Markup(FormatAgentInstallMode(layout.Mode)));
        grid.AddRow(new Markup("[grey]Installed[/]"), new Markup($"{installedAgents.Count} of {catalog.Agents.Count} agents"));
        AnsiConsole.Write(new Panel(grid).Header("Session").Expand());
        AnsiConsole.WriteLine();

        if (catalog.Agents.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup("No agents available in the catalog."))
                .Header("Agents")
                .Expand());
            return;
        }

        var table = new Table().Expand();
        table.Title = new TableTitle("Available orchestration agents");
        table.AddColumn("Agent");
        table.AddColumn("Status");
        table.AddColumn("Skills");
        table.AddColumn("Description");

        foreach (var agent in catalog.Agents.OrderBy(a => a.Name, StringComparer.Ordinal))
        {
            var isInstalled = installedAgents.Any(i => string.Equals(i.Agent.Name, agent.Name, StringComparison.OrdinalIgnoreCase));
            var skillsList = agent.Skills.Count > 0
                ? string.Join(", ", agent.Skills.Take(3).Select(ToAlias))
                  + (agent.Skills.Count > 3 ? $" (+{agent.Skills.Count - 3})" : "")
                : "-";

            table.AddRow(
                $"[bold]{Escape(ToAlias(agent.Name))}[/]",
                isInstalled ? "[green]Installed[/]" : "[grey]Not installed[/]",
                Escape(skillsList),
                Escape(CompactDescription(agent.Description)));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var notInstalled = catalog.Agents
            .Where(a => !installedAgents.Any(i => string.Equals(i.Agent.Name, a.Name, StringComparison.OrdinalIgnoreCase)))
            .Take(3)
            .Select(a => ToAlias(a.Name))
            .ToArray();

        if (notInstalled.Length > 0)
        {
            AnsiConsole.Write(new Panel(new Markup(
                $"Install agents:{Environment.NewLine}[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install {string.Join(' ', notInstalled)}")}[/]{Environment.NewLine}{Environment.NewLine}" +
                $"Install to all detected platforms:{Environment.NewLine}[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install {string.Join(' ', notInstalled)} --auto")}[/]"))
                .Header("Quick commands")
                .Expand());
        }
    }

    public static void RenderAgentInstallSummary(
        AgentCatalogPackage catalog,
        AgentInstallLayout layout,
        IReadOnlyList<AgentEntry> agents,
        AgentInstallSummary summary)
    {
        WriteTitle($"{ToolIdentity.AgentDisplayCommand} install");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Agent platform[/]"), new Markup(Escape(layout.Agent.ToString())));
        grid.AddRow(new Markup("[grey]Target[/]"), new Markup(Escape(layout.PrimaryRoot.FullName)));
        grid.AddRow(new Markup("[grey]Mode[/]"), new Markup(FormatAgentInstallMode(layout.Mode)));
        grid.AddRow(new Markup("[grey]Installed[/]"), new Markup(summary.InstalledCount.ToString()));
        grid.AddRow(new Markup("[grey]Skipped[/]"), new Markup(summary.SkippedExisting.Count.ToString()));
        AnsiConsole.Write(new Panel(grid).Header("Summary").Expand());
        AnsiConsole.WriteLine();

        if (agents.Count > 0)
        {
            var table = new Table().Expand();
            table.Title = new TableTitle("Installed agents");
            table.AddColumn("Agent");
            table.AddColumn("Skills");
            table.AddColumn("Status");

            foreach (var agent in agents.OrderBy(a => a.Name, StringComparer.Ordinal))
            {
                var wasSkipped = summary.SkippedExisting.Contains(agent.Name, StringComparer.OrdinalIgnoreCase);
                var skillsList = agent.Skills.Count > 0
                    ? string.Join(", ", agent.Skills.Take(4).Select(ToAlias))
                    : "-";

                table.AddRow(
                    $"[bold]{Escape(ToAlias(agent.Name))}[/]",
                    Escape(skillsList),
                    wasSkipped ? "[grey]Skipped (exists)[/]" : "[green]Installed[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("Next step").Expand());
    }

    public static void RenderAgentInstallSummaryMultiple(
        AgentCatalogPackage catalog,
        IReadOnlyList<AgentInstallLayout> layouts,
        IReadOnlyList<AgentEntry> agents,
        AgentInstallSummary summary)
    {
        WriteTitle($"{ToolIdentity.AgentDisplayCommand} install");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Platforms detected[/]"), new Markup(layouts.Count.ToString()));
        grid.AddRow(new Markup("[grey]Agents selected[/]"), new Markup(agents.Count.ToString()));
        grid.AddRow(new Markup("[grey]Total installed[/]"), new Markup(summary.InstalledCount.ToString()));
        AnsiConsole.Write(new Panel(grid).Header("Summary").Expand());
        AnsiConsole.WriteLine();

        var table = new Table().Expand();
        table.Title = new TableTitle("Install targets");
        table.AddColumn("Platform");
        table.AddColumn("Mode");
        table.AddColumn("Path");

        foreach (var layout in layouts)
        {
            table.AddRow(
                Escape(layout.Agent.ToString()),
                FormatAgentInstallMode(layout.Mode),
                Escape(layout.PrimaryRoot.FullName));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var agentTable = new Table().Expand();
        agentTable.Title = new TableTitle("Installed agents");
        agentTable.AddColumn("Agent");
        agentTable.AddColumn("Skills");

        foreach (var agent in agents.OrderBy(a => a.Name, StringComparer.Ordinal))
        {
            var skillsList = agent.Skills.Count > 0
                ? string.Join(", ", agent.Skills.Take(4).Select(ToAlias))
                : "-";

            agentTable.AddRow(
                $"[bold]{Escape(ToAlias(agent.Name))}[/]",
                Escape(skillsList));
        }

        AnsiConsole.Write(agentTable);
        AnsiConsole.WriteLine();

        var hints = string.Join(Environment.NewLine, layouts.Select(l => $"• {l.Agent}: {l.ReloadHint}"));
        AnsiConsole.Write(new Panel(new Markup(Escape(hints))).Header("Next steps").Expand());
    }

    public static void RenderAgentRemoveSummary(
        AgentCatalogPackage catalog,
        AgentInstallLayout layout,
        IReadOnlyList<AgentEntry> agents,
        AgentRemoveSummary summary)
    {
        WriteTitle($"{ToolIdentity.AgentDisplayCommand} remove");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Agent platform[/]"), new Markup(Escape(layout.Agent.ToString())));
        grid.AddRow(new Markup("[grey]Target[/]"), new Markup(Escape(layout.PrimaryRoot.FullName)));
        grid.AddRow(new Markup("[grey]Removed[/]"), new Markup(summary.RemovedCount.ToString()));
        grid.AddRow(new Markup("[grey]Missing[/]"), new Markup(summary.MissingAgents.Count.ToString()));
        AnsiConsole.Write(new Panel(grid).Header("Summary").Expand());
        AnsiConsole.WriteLine();

        if (agents.Count > 0)
        {
            var table = new Table().Expand();
            table.Title = new TableTitle("Remove results");
            table.AddColumn("Agent");
            table.AddColumn("Status");

            foreach (var agent in agents.OrderBy(a => a.Name, StringComparer.Ordinal))
            {
                var wasMissing = summary.MissingAgents.Contains(agent.Name, StringComparer.OrdinalIgnoreCase);

                table.AddRow(
                    $"[bold]{Escape(ToAlias(agent.Name))}[/]",
                    wasMissing ? "[grey]Missing[/]" : "[red]Removed[/]");
            }

            AnsiConsole.Write(table);
        }
    }

    private static string FormatAgentInstallMode(AgentInstallMode mode) => mode switch
    {
        AgentInstallMode.MarkdownAgentFiles => "Markdown agent files",
        AgentInstallMode.CopilotAgentFiles => "Copilot agents (.agent.md)",
        AgentInstallMode.CodexRoleFiles => "Codex role files (.toml)",
        _ => Escape(mode.ToString()),
    };
}

internal sealed record ScopeInventoryRow(
    InstallScope Scope,
    DirectoryInfo TargetRoot,
    IReadOnlyList<InstalledSkillRecord> InstalledSkills);

internal sealed record SkillInstallBatchResult(
    SkillInstallLayout Layout,
    IReadOnlyList<SkillActionRow> Rows,
    SkillInstallSummary Summary);

internal sealed record SkillActionRow(SkillEntry Skill, string FromVersion, string ToVersion, SkillAction Action);

internal enum SkillAction
{
    Installed,
    Removed,
    Updated,
    Missing,
    Skipped,
}
