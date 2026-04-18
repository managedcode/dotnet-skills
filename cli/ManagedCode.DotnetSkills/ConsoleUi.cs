using ManagedCode.DotnetSkills.Runtime;

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
                    .Header("[dim]installed skills[/]")
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
                    .Header("[dim]available[/]")
                    .Expand());
            }
            else
            {
                AnsiConsole.Write(BuildAvailableStackSummaryTable(availableSkills));

                if (renderDetailedAvailableGroups)
                {
                    AnsiConsole.WriteLine();
                    RenderAvailableSkillGroups(availableSkills);
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Panel(new Markup("Use [green]dotnet skills list --available-only[/] to expand stacks into stack/lane skill tables with short summaries."))
                        .Header("[dim]explore[/]")
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
            catalog.Packages.Where(CatalogOrganization.IsPrimaryBundle).ToArray()));
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
                .Header("[dim]skipped[/]")
                .Expand());
            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("[dim]next step[/]").Border(BoxBorder.Rounded).Expand());
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
        grid.AddRow(new Markup("[dim]catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [dim]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[dim]targets[/]"), new Markup($"{results.Count}"));
        grid.AddRow(new Markup("[green]\u2714[/] [dim]written[/]"), new Markup($"[green]{totalWritten}[/]"));
        grid.AddRow(new Markup("[dim]\u2500[/] [dim]skipped[/]"), new Markup($"[dim]{totalSkipped}[/]"));

        if (totalAdapters > 0)
        {
            grid.AddRow(new Markup("[yellow]\u25c6[/] [dim]adapters[/]"), new Markup($"[yellow]{totalAdapters}[/]"));
        }

        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]summary[/]").Border(BoxBorder.Rounded).Expand());
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
        AnsiConsole.Write(new Panel(new Markup(Escape(hints))).Header("[dim]next steps[/]").Border(BoxBorder.Rounded).Expand());
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
                .Header("[dim]status[/]")
                .Expand());
        }
        else
        {
            AnsiConsole.Write(BuildOperationTable("Remove results", rows));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("[dim]next step[/]").Border(BoxBorder.Rounded).Expand());
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
                .Header("[dim]status[/]")
                .Expand());
        }
        else
        {
            AnsiConsole.Write(BuildOperationTable("Updated skills", rows));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("[dim]next step[/]").Border(BoxBorder.Rounded).Expand());
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
            AnsiConsole.MarkupLine("[dim]No direct package or project-signal matches found.[/]");
            AnsiConsole.MarkupLine("[dim]Start with[/] [green]dotnet skills install dotnet modern-csharp[/] [dim]for a baseline.[/]");
            return;
        }

        // Recommendation table with grouped confidence sections
        var highRecs = scanResult.Recommendations.Where(r => r.Confidence == RecommendationConfidence.High).ToArray();
        var medRecs = scanResult.Recommendations.Where(r => r.Confidence == RecommendationConfidence.Medium).ToArray();
        var lowRecs = scanResult.Recommendations.Where(r => r.Confidence == RecommendationConfidence.Low).ToArray();

        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle("[bold]Skill recommendations[/]");
        table.AddColumn("Skill");
        table.AddColumn("Confidence");
        table.AddColumn("Auto");
        table.AddColumn("Status");
        table.AddColumn("Signals");

        void AddRecommendationRows(IReadOnlyList<ProjectSkillRecommendation> recs)
        {
            foreach (var recommendation in recs)
            {
                installedSkills.TryGetValue(recommendation.Skill.Name, out var installed);
                table.AddRow(
                    $"[bold]{Escape(ToAlias(recommendation.Skill.Name))}[/]",
                    FormatConfidence(recommendation.Confidence),
                    FormatAutoSyncCandidate(recommendation),
                    FormatRecommendationStatus(installed),
                    Escape(string.Join("; ", recommendation.Reasons)));
            }
        }

        AddRecommendationRows(highRecs);
        if (highRecs.Length > 0 && (medRecs.Length > 0 || lowRecs.Length > 0))
            table.AddEmptyRow();
        AddRecommendationRows(medRecs);
        if (medRecs.Length > 0 && lowRecs.Length > 0)
            table.AddEmptyRow();
        AddRecommendationRows(lowRecs);

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
            var lines = new List<string>();

            if (installableSkills.Length > 0)
            {
                lines.Add($"[green]{Escape($"dotnet skills install {string.Join(' ', installableSkills)}")}[/]");
            }

            if (autoInstallableSkills.Length > 0)
            {
                lines.Add($"[green]{Escape("dotnet skills install --auto")}[/] [dim]project-driven sync[/]");
                lines.Add($"[green]{Escape("dotnet skills install --auto --prune")}[/] [dim]+ remove stale[/]");
            }

            AnsiConsole.Write(new Panel(new Markup(string.Join(Environment.NewLine, lines)))
                .Header("[dim]next steps[/]")
                .Border(BoxBorder.Rounded)
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
        grid.AddRow(new Markup("[dim]catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [dim]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[dim]project[/]"), new Markup($"[dim]{Escape(plan.ScanResult.ProjectRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]scanned[/]"), new Markup($"{plan.ScanResult.ProjectFiles.Count} projects"));
        grid.AddRow(new Markup("[dim]auto-managed[/]"), new Markup($"{plan.DesiredSkills.Count}"));
        grid.AddRow(new Markup("[green]\u2714[/] [dim]written[/]"), new Markup($"[green]{installSummary.InstalledCount}[/]"));
        grid.AddRow(new Markup("[dim]\u2500[/] [dim]skipped[/]"), new Markup($"[dim]{installSummary.SkippedExisting.Count}[/]"));

        if (pruneRequested)
        {
            grid.AddRow(new Markup("[red]\u2716[/] [dim]removed[/]"), new Markup($"[red]{removeSummary.RemovedCount}[/]"));
            grid.AddRow(new Markup("[yellow]\u25c6[/] [dim]protected[/]"), new Markup($"[yellow]{plan.ProtectedStaleSkills.Count}[/]"));
        }

        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]auto sync[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        // Install section
        WriteSubTitle("Install");
        if (installRows.Count == 0)
        {
            AnsiConsole.Write(new Markup("  [grey]No auto-installable skills matched the detected project signals.[/]"));
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Markup("  [grey]Manual baseline skills such as[/] [bold]dotnet[/] [grey]and[/] [bold]dotnet-modern-csharp[/] [grey]remain opt-in.[/]"));
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.Write(BuildOperationTable("Auto-managed skills", installRows));
        }

        AnsiConsole.WriteLine();

        // Prune section
        if (pruneRequested)
        {
            WriteSubTitle("Prune");
            if (!plan.MatchedPreviousProject)
            {
                AnsiConsole.Write(new Markup("  [grey]No previous auto-managed state matched this project root.[/]"));
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Markup("  Run [green]dotnet skills install --auto[/] once first, then use [green]--prune[/] on later syncs."));
                AnsiConsole.WriteLine();
            }
            else if (removeRows.Count == 0)
            {
                AnsiConsole.Write(new Markup("  [grey]No stale auto-managed skills needed removal.[/]"));
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.Write(BuildOperationTable("Pruned skills", removeRows));
            }

            if (plan.ProtectedStaleSkills.Count > 0)
            {
                AnsiConsole.WriteLine();
                var protectedNames = plan.ProtectedStaleSkills
                    .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                    .Select(skill => $"[yellow]\u25c6[/] {Escape(ToAlias(skill.Name))}");
                AnsiConsole.Write(new Panel(new Markup(string.Join(Environment.NewLine, protectedNames)))
                    .Header("[yellow]protected[/]")
                    .Border(BoxBorder.Rounded)
                    .Expand());
            }

            AnsiConsole.WriteLine();
        }

        // Next steps
        var lines = new List<string>
        {
            Escape(layout.ReloadHint),
            "[green]dotnet skills recommend[/] [dim]to inspect the scan[/]",
        };

        if (!pruneRequested)
        {
            lines.Add("[green]dotnet skills install --auto --prune[/] [dim]to remove stale skills[/]");
        }

        AnsiConsole.Write(new Panel(new Markup(string.Join(Environment.NewLine, lines)))
            .Header("[dim]next steps[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    public static void RenderSyncSummary(SkillCatalogPackage catalog)
    {
        WriteTitle("dotnet skills sync");
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[green]\u2714[/] [dim]catalog[/]"), new Markup(Escape(catalog.CatalogVersion)));
        grid.AddRow(new Markup("[dim]source[/]"), new Markup(Escape(catalog.SourceLabel)));
        grid.AddRow(new Markup("[dim]cache[/]"), new Markup($"[dim]{Escape(catalog.CatalogRoot.FullName)}[/]"));
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]synced[/]").Border(BoxBorder.Rounded).Expand());
    }

    public static void RenderVersionSummary(string currentVersion, ToolUpdateStatusInfo? status)
    {
        WriteBanner();

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]package[/]"), new Markup(ToolIdentity.PackageId));
        grid.AddRow(new Markup("[dim]current[/]"), new Markup(Escape(currentVersion)));
        grid.AddRow(new Markup("[dim]build[/]"), new Markup(ToolVersionInfo.IsDevelopmentBuild ? "[dim]local development[/]" : "[green]published[/]"));

        if (status is not null)
        {
            grid.AddRow(new Markup("[dim]latest[/]"), new Markup(Escape(status.LatestVersion ?? "unknown")));
            grid.AddRow(new Markup("[dim]status[/]"), new Markup(FormatToolUpdateState(status.State)));

            if (status.CheckedAt is not null)
            {
                grid.AddRow(
                    new Markup("[dim]checked[/]"),
                    new Markup($"{Escape(status.CheckedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"))}{(status.UsedCachedValue ? " [dim](cached)[/]" : string.Empty)}"));
            }
        }

        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]version[/]").Border(BoxBorder.Rounded).Expand());

        if (status?.HasUpdate == true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(BuildUpdateCommandPanel(status));
        }
    }

    public static void RenderPackageList(SkillCatalogPackage catalog)
    {
        WriteTitle("dotnet skills bundle list");
        var visibleBundles = catalog.Packages
            .Where(CatalogOrganization.IsPrimaryBundle)
            .OrderBy(CatalogOrganization.FormatBundleSortKey, StringComparer.Ordinal)
            .ToArray();
        var skillTokens = catalog.Skills.ToDictionary(skill => skill.Name, skill => skill.TokenCount, StringComparer.OrdinalIgnoreCase);

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [dim]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[dim]bundles[/]"), new Markup($"{visibleBundles.Length}"));
        grid.AddRow(new Markup("[dim]skills covered[/]"), new Markup($"{catalog.Skills.Count}"));
        grid.AddRow(new Markup("[dim]skill tokens[/]"), new Markup(FormatTokenCount(catalog.Skills.Sum(skill => skill.TokenCount))));
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]bundles[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        if (visibleBundles.Length == 0)
        {
            AnsiConsole.Write(new Panel(new Markup("No focused bundles are available in this catalog version yet."))
                .Header("[dim]bundles[/]")
                .Expand());
            return;
        }

        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle("[bold]Focused bundles[/]");
        table.AddColumn("Bundle");
        table.AddColumn("Area");
        table.AddColumn("Skills");
        table.AddColumn("Tokens");
        table.AddColumn("Command");
        table.AddColumn("Includes");

        foreach (var package in visibleBundles)
        {
            var skillAliases = package.Skills
                .Select(ToAlias)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var tokenCount = package.Skills.Sum(skillName => skillTokens.TryGetValue(skillName, out var value) ? value : 0);

            table.AddRow(
                $"[bold]{Escape(package.Name)}[/]",
                Escape(CatalogOrganization.ResolveBundleAreaLabel(package)),
                skillAliases.Length.ToString(),
                FormatTokenCount(tokenCount),
                Escape($"install bundle {package.Name}"),
                Escape(string.Join(", ", skillAliases.Take(4)))
                    + (skillAliases.Length > 4 ? $" [grey](+{skillAliases.Length - 4} more)[/]" : string.Empty));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var suggested = visibleBundles
            .Take(3)
            .Select(package => $"dotnet skills install bundle {package.Name}")
            .ToArray();

        AnsiConsole.Write(new Panel(new Markup(
                string.Join(Environment.NewLine, suggested.Select(command => $"[green]{Escape(command)}[/]"))))
            .Header("[dim]commands[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    public static void RenderUsage()
    {
        if (ToolIdentity.IsAgentFirstTool)
        {
            RenderAgentToolUsage();
            return;
        }

        WriteBanner();

        // Command reference — compact, scannable, grouped
        var cmdTable = new Table().Border(TableBorder.None).Expand().HideHeaders();
        cmdTable.AddColumn(new TableColumn("cmd").NoWrap().PadLeft(2).PadRight(2));
        cmdTable.AddColumn(new TableColumn("desc"));

        void Section(string title)
        {
            cmdTable.AddEmptyRow();
            cmdTable.AddRow(new Markup($"[dim]{Escape(title)}[/]"), new Markup(string.Empty));
        }

        void Cmd(string command, string desc)
        {
            cmdTable.AddRow(
                new Markup($"[green]{Escape(command)}[/]"),
                new Markup($"[dim]{Escape(desc)}[/]"));
        }

        Section("Getting started");
        Cmd(ToolIdentity.DisplayCommand, "Launch the interactive shell");
        Cmd($"{ToolIdentity.DisplayCommand} help", "This command reference");
        Cmd($"{ToolIdentity.DisplayCommand} version", "Version and update check");

        Section("Catalog");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} list", "Inventory with scope comparison");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} bundle list", "Focused bundles by stack and workflow");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} recommend", "Scan .csproj and propose skills");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} catalog tokens --catalog-root .", "Export per-skill token counts as JSON");

        Section("Install");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} install aspire orleans", "Install by alias");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} install --auto", "Auto-install from project signals");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} install --auto --prune", "Reconcile stale auto-managed skills");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} install bundle dotnet-quality", "Install a focused multi-skill bundle");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} remove --all", "Remove all installed skills");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} update", "Update to latest catalog version");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} sync --force", "Refresh cached catalog");
        Cmd($"{ToolIdentity.SkillsDisplayCommand} where", "Print resolved install path");

        Section("Agents");
        Cmd($"{ToolIdentity.AgentDisplayCommand} list", "List orchestration agents");
        Cmd($"{ToolIdentity.AgentDisplayCommand} install router ai", "Install agents by name");
        Cmd($"{ToolIdentity.AgentDisplayCommand} install --all --auto", "All agents to detected platforms");
        Cmd($"{ToolIdentity.AgentDisplayCommand} install router --target /path", "Explicit target path");

        AnsiConsole.Write(cmdTable);
        AnsiConsole.WriteLine();

        // Compact notes — key flags and behavior
        var noteLines = new[]
        {
            $"[dim]Bare[/] [green]{Escape(ToolIdentity.DisplayCommand)}[/] [dim]opens the interactive shell.[/]",
            "[dim]The shell exposes[/] [green]Stack -> Lane -> Skill[/] [dim]browse, analysis views, and install preview.[/]",
            "[dim]Short aliases work everywhere:[/] [green]aspire[/] [dim]resolves to[/] [green]dotnet-aspire[/][dim].[/]",
            "[dim]--bundled skips the network. --catalog-version pins a release. --refresh redownloads.[/]",
            "[dim]Auto-detect probes .codex, .claude, .github, .gemini, .junie; falls back to .agents/skills.[/]",
            $"[dim]Set[/] [green]{Escape(ToolIdentity.SkipUpdateEnvironmentVariable)}=1[/] [dim]to suppress update notices.[/]",
        };

        AnsiConsole.Write(new Panel(new Markup(string.Join(Environment.NewLine, noteLines)))
            .Header("[dim]notes[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
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
            $"- Use `{ToolIdentity.SkillsDisplayCommand} ...` when you want catalog skills or bundle installs.",
            $"- `{ToolIdentity.DisplayCommand} list` and bare `{ToolIdentity.DisplayCommand}` both show the bundled agent catalog.",
            $"- `{ToolIdentity.DisplayCommand} version` and `{ToolIdentity.DisplayCommand} --version` both show the current tool version.",
            $"- Set `{ToolIdentity.SkipUpdateEnvironmentVariable}=1` to suppress automatic tool update notices on startup.",
            "- Agent auto-detect uses only native agent roots. If none exist yet, specify `--agent` or `--target`.",
            "- Explicit `--target` still requires `--agent`, because the generated file format depends on the selected platform.");

        AnsiConsole.Write(new Panel(new Markup(Escape(notes))).Header("[dim]notes[/]").Border(BoxBorder.Rounded).Expand());
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

    private static void WriteBanner()
    {
        AnsiConsole.MarkupLine("[bold deepskyblue1]dotnet skills[/] [dim]v{0}[/]", Escape(ToolVersionInfo.CurrentVersion));
        AnsiConsole.MarkupLine("[dim].NET skill catalog for AI-assisted development[/]");
        AnsiConsole.WriteLine();
    }

    private static void WriteTitle(string title)
    {
        AnsiConsole.MarkupLine($"[bold deepskyblue1]{Escape(title)}[/]");
        AnsiConsole.Write(new Rule { Style = Style.Parse("dim") });
        AnsiConsole.WriteLine();
    }

    private static void WriteSubTitle(string title)
    {
        AnsiConsole.MarkupLine($"[dim]{Escape(title)}[/]");
    }

    private static Panel BuildSessionPanel(SkillCatalogPackage catalog, SkillInstallLayout layout, int installedCount, string? projectRoot)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [dim]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[dim]platform[/]"), new Markup(Escape(layout.Agent.ToString())));
        grid.AddRow(new Markup("[dim]scope[/]"), new Markup(Escape(layout.Scope.ToString())));
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            grid.AddRow(new Markup("[dim]project[/]"), new Markup($"[dim]{Escape(projectRoot)}[/]"));
        }

        grid.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));

        var ratio = catalog.Skills.Count > 0 ? (double)installedCount / catalog.Skills.Count : 0;
        var barWidth = 20;
        var filled = (int)(ratio * barWidth);
        var bar = new string('\u2588', filled) + new string('\u2591', barWidth - filled);
        grid.AddRow(new Markup("[dim]installed[/]"), new Markup($"[green]{bar}[/] {installedCount}/{catalog.Skills.Count}"));

        return new Panel(grid).Header("[deepskyblue1]session[/]").Border(BoxBorder.Rounded).Expand();
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
        grid.AddRow(new Markup("[dim]catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [dim]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[green]\u2714[/] [dim]written[/]"), new Markup($"[green]{writtenCount}[/]"));
        grid.AddRow(new Markup("[dim]\u2500[/] [dim]skipped[/]"), new Markup($"[dim]{skippedCount}[/]"));

        if (generatedAdapters > 0)
        {
            grid.AddRow(new Markup("[yellow]\u25c6[/] [dim]adapters[/]"), new Markup($"[yellow]{generatedAdapters}[/]"));
        }

        return new Panel(grid).Header("[deepskyblue1]summary[/]").Border(BoxBorder.Rounded).Expand();
    }

    private static Panel BuildUpdateCommandPanel(ToolUpdateStatusInfo status)
    {
        var lines = string.Join(
            Environment.NewLine,
            $"[yellow]\u26a1[/] A newer [bold]{Escape(status.LatestVersion ?? "unknown")}[/] release is available on NuGet. Current: [bold]{Escape(status.CurrentVersion)}[/].",
            string.Empty,
            "[deepskyblue1]\u25b8[/] Global tool:",
            $"  [green]{Escape(GlobalToolUpdateCommand)}[/]",
            string.Empty,
            "[deepskyblue1]\u25b8[/] Local tool manifest:",
            $"  [green]{Escape(LocalToolUpdateCommand)}[/]");

        return new Panel(new Markup(lines)).Header("[yellow]update available[/]").Border(BoxBorder.Rounded).Expand();
    }

    private static Panel BuildRecommendationPanel(ProjectScanResult scanResult, SkillCatalogPackage catalog, SkillInstallLayout layout)
    {
        var autoCount = scanResult.Recommendations.Count(recommendation => recommendation.IsAutoInstallCandidate);
        var highCount = scanResult.Recommendations.Count(r => r.Confidence == RecommendationConfidence.High);
        var medCount = scanResult.Recommendations.Count(r => r.Confidence == RecommendationConfidence.Medium);
        var lowCount = scanResult.Recommendations.Count(r => r.Confidence == RecommendationConfidence.Low);

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]project[/]"), new Markup($"[dim]{Escape(scanResult.ProjectRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]scanned[/]"), new Markup($"{scanResult.ProjectFiles.Count} projects"));
        grid.AddRow(new Markup("[dim]frameworks[/]"), new Markup(scanResult.TargetFrameworks.Count == 0 ? "[dim]unknown[/]" : Escape(string.Join(", ", scanResult.TargetFrameworks))));
        grid.AddRow(new Markup("[dim]catalog[/]"), new Markup($"{Escape(catalog.SourceLabel)} [dim]({Escape(catalog.CatalogVersion)})[/]"));
        grid.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));

        // Recommendation breakdown — inline
        var breakdownParts = new List<string>();
        if (highCount > 0) breakdownParts.Add($"[green]{highCount} high[/]");
        if (medCount > 0) breakdownParts.Add($"[yellow]{medCount} med[/]");
        if (lowCount > 0) breakdownParts.Add($"[dim]{lowCount} low[/]");
        var breakdown = breakdownParts.Count > 0 ? $" [dim]([/]{string.Join("[dim] \u00b7 [/]", breakdownParts)}[dim])[/]" : string.Empty;

        grid.AddRow(new Markup("[dim]found[/]"), new Markup($"{scanResult.Recommendations.Count} recommendations{breakdown}"));
        grid.AddRow(new Markup("[dim]auto-eligible[/]"), new Markup($"{autoCount}"));
        return new Panel(grid).Header("[deepskyblue1]project scan[/]").Border(BoxBorder.Rounded).Expand();
    }

    private static Table BuildInstalledSkillsTable(IReadOnlyList<InstalledSkillRecord> installedSkills)
    {
        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle("[bold]Installed skills[/]");
        table.AddColumn("Alias");
        table.AddColumn("Area");
        table.AddColumn("Installed");
        table.AddColumn("Latest");
        table.AddColumn("Tokens");
        table.AddColumn("Status");

        foreach (var record in installedSkills.OrderBy(item => item.Skill.Name, StringComparer.Ordinal))
        {
            table.AddRow(
                $"[bold]{Escape(ToAlias(record.Skill.Name))}[/]",
                Escape($"{record.Skill.Stack} / {record.Skill.Lane}"),
                Escape(record.InstalledVersion),
                Escape(record.Skill.Version),
                FormatTokenCount(record.Skill.TokenCount),
                record.IsCurrent ? "[green]\u2714 Current[/]" : "[yellow]\u21bb Update available[/]");
        }

        return table;
    }

    private static Table BuildAvailableStackSummaryTable(IReadOnlyList<SkillEntry> availableSkills)
    {
        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle("[bold]Available stacks[/]");
        table.AddColumn("Stack");
        table.AddColumn("Lanes");
        table.AddColumn("Skills");
        table.AddColumn("Tokens");
        table.AddColumn("Examples");

        foreach (var group in availableSkills
                     .GroupBy(skill => skill.Stack, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => CatalogOrganization.GetStackRank(group.Key))
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            var aliases = group
                .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                .Take(4)
                .Select(skill => ToAlias(skill.Name))
                .ToArray();
            var lanes = group
                .Select(skill => skill.Lane)
                .Where(lane => !string.IsNullOrWhiteSpace(lane))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(CatalogOrganization.GetLaneRank)
                .ThenBy(lane => lane, StringComparer.Ordinal)
                .ToArray();

            table.AddRow(
                Escape(group.Key),
                Escape(string.Join(", ", lanes.Take(3))) + (lanes.Length > 3 ? $" [grey](+{lanes.Length - 3})[/]" : string.Empty),
                group.Count().ToString(),
                FormatTokenCount(group.Sum(skill => skill.TokenCount)),
                Escape(string.Join(", ", aliases)) + (group.Count() > aliases.Length ? $" [grey](+{group.Count() - aliases.Length} more)[/]" : string.Empty));
        }

        return table;
    }

    private static void RenderAvailableSkillGroups(IReadOnlyList<SkillEntry> availableSkills)
    {
        foreach (var group in availableSkills
                     .GroupBy(skill => skill.Stack, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => CatalogOrganization.GetStackRank(group.Key))
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            var table = new Table().Expand();
            table.Title = new TableTitle($"{group.Key} skills");
            table.AddColumn("Lane");
            table.AddColumn("Alias");
            table.AddColumn("Skill");
            table.AddColumn("Tokens");
            table.AddColumn("Summary");

            foreach (var skill in group
                         .OrderBy(item => CatalogOrganization.GetLaneRank(item.Lane))
                         .ThenBy(item => item.Lane, StringComparer.Ordinal)
                         .ThenBy(item => item.Name, StringComparer.Ordinal))
            {
                table.AddRow(
                    Escape(skill.Lane),
                    $"[bold]{Escape(ToAlias(skill.Name))}[/]",
                    Escape(skill.Name),
                    FormatTokenCount(skill.TokenCount),
                    Escape(CompactDescription(skill.Description)));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    private static Table BuildOperationTable(string title, IReadOnlyList<SkillActionRow> rows)
    {
        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle($"[bold]{Escape(title)}[/]");
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

        if (availableSkills.Count > 0)
        {
            lines.Add($"[green]{Escape($"dotnet skills install {string.Join(' ', availableSkills.Take(3).Select(skill => ToAlias(skill.Name)))}")}[/]");
        }

        if (installedSkills.Count > 0)
        {
            lines.Add($"[green]{Escape($"dotnet skills update {string.Join(' ', installedSkills.Take(3).Select(skill => ToAlias(skill.Name)))}")}[/]");
        }

        if (packages.Count > 0)
        {
            var featuredPackage = packages.OrderBy(CatalogOrganization.FormatBundleSortKey, StringComparer.Ordinal).First();
            lines.Add($"[green]{Escape($"dotnet skills install bundle {featuredPackage.Name}")}[/]");
        }

        lines.Add($"[green]dotnet skills install --auto[/] [dim]project-driven sync[/]");
        lines.Add($"[green]dotnet skills recommend[/] [dim]scan .csproj signals[/]");

        var alternateScope = scopeInventory.FirstOrDefault(row => row.Scope != layout.Scope);
        if (alternateScope is not null)
        {
            var scopeCmd = alternateScope.Scope == InstallScope.Global
                ? "dotnet skills list --scope global"
                : "dotnet skills list --scope project";
            lines.Add($"[green]{Escape(scopeCmd)}[/] [dim]compare scopes[/]");
        }

        return new Panel(new Markup(string.Join(Environment.NewLine, lines)))
            .Header("[dim]commands[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
    }

private static string BuildSkillCell(SkillEntry skill)
    {
        return $"[bold]{Escape(skill.Name)}[/] [dim]({Escape(ToAlias(skill.Name))})[/]";
    }

    private static string FormatAction(SkillAction action) => action switch
    {
        SkillAction.Installed => "[green]\u2714 Installed[/]",
        SkillAction.Removed => "[red]\u2716 Removed[/]",
        SkillAction.Updated => "[yellow]\u21bb Updated[/]",
        SkillAction.Missing => "[grey]\u2205 Missing[/]",
        SkillAction.Skipped => "[grey]\u2500 Skipped[/]",
        _ => Escape(action.ToString()),
    };

    private static string FormatConfidence(RecommendationConfidence confidence) => confidence switch
    {
        RecommendationConfidence.High => "[green]\u2588\u2588\u2588 High[/]",
        RecommendationConfidence.Medium => "[yellow]\u2588\u2588\u2591 Medium[/]",
        RecommendationConfidence.Low => "[grey]\u2588\u2591\u2591 Low[/]",
        _ => Escape(confidence.ToString()),
    };

    private static string FormatAutoSyncCandidate(ProjectSkillRecommendation recommendation)
    {
        return recommendation.IsAutoInstallCandidate
            ? "[green]\u2714 Yes[/]"
            : "[grey]\u2500 Manual[/]";
    }

    private static string FormatRecommendationStatus(InstalledSkillRecord? installed)
    {
        if (installed is null)
        {
            return "[deepskyblue1]\u25cb Not installed[/]";
        }

        return installed.IsCurrent
            ? $"[green]\u2714 {Escape(installed.InstalledVersion)}[/]"
            : $"[yellow]\u21bb Update to {Escape(installed.Skill.Version)}[/]";
    }

    private static string FormatToolUpdateState(ToolUpdateState state) => state switch
    {
        ToolUpdateState.Current => "[green]\u2714 Current[/]",
        ToolUpdateState.UpdateAvailable => "[yellow]\u26a1 Update available[/]",
        ToolUpdateState.DevelopmentBuild => "[grey]\u2500 Local development build[/]",
        ToolUpdateState.Unknown => "[grey]\u2500 Latest version unavailable[/]",
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

    private static string FormatTokenCount(int tokenCount) => tokenCount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string value) => Markup.Escape(value);

    private static string ToAlias(string skillName) => skillName.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase)
        ? skillName["dotnet-".Length..]
        : skillName;

    private static string GlobalToolUpdateCommand => $"dotnet tool update --global {ToolIdentity.PackageId}";

    private static string LocalToolUpdateCommand => $"dotnet tool update {ToolIdentity.PackageId}";

    public static void RenderAgentList(
        AgentCatalogPackage catalog,
        AgentInstallLayout layout,
        IReadOnlyList<InstalledAgentRecord> installedAgents)
    {
        WriteTitle($"{ToolIdentity.AgentDisplayCommand} list");

        var agentRatio = catalog.Agents.Count > 0 ? (double)installedAgents.Count / catalog.Agents.Count : 0;
        var barWidth = 20;
        var agentFilled = (int)(agentRatio * barWidth);
        var agentBar = new string('\u2588', agentFilled) + new string('\u2591', barWidth - agentFilled);

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]agents[/]"), new Markup($"{catalog.Agents.Count}"));
        grid.AddRow(new Markup("[dim]platform[/]"), new Markup(Escape(layout.Agent.ToString())));
        grid.AddRow(new Markup("[dim]scope[/]"), new Markup(Escape(layout.Scope.ToString())));
        grid.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]installed[/]"), new Markup($"[green]{agentBar}[/] {installedAgents.Count}/{catalog.Agents.Count}"));
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]session[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        if (catalog.Agents.Count == 0)
        {
            AnsiConsole.Write(new Panel(new Markup("No agents available in the catalog."))
                .Header("[dim]agents[/]")
                .Expand());
            return;
        }

        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle("[bold]Available orchestration agents[/]");
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
                isInstalled ? "[green]\u2714 Installed[/]" : "[grey]\u25cb Not installed[/]",
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
            var lines = new[]
            {
                $"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install {string.Join(' ', notInstalled)}")}[/]",
                $"[green]{Escape($"{ToolIdentity.AgentDisplayCommand} install {string.Join(' ', notInstalled)} --auto")}[/] [dim]all platforms[/]",
            };
            AnsiConsole.Write(new Panel(new Markup(string.Join(Environment.NewLine, lines)))
                .Header("[dim]commands[/]")
                .Border(BoxBorder.Rounded)
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
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]summary[/]").Border(BoxBorder.Rounded).Expand());
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

        AnsiConsole.Write(new Panel(new Markup(Escape(layout.ReloadHint))).Header("[dim]next step[/]").Border(BoxBorder.Rounded).Expand());
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
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]summary[/]").Border(BoxBorder.Rounded).Expand());
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
        AnsiConsole.Write(new Panel(new Markup(Escape(hints))).Header("[dim]next steps[/]").Border(BoxBorder.Rounded).Expand());
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
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]summary[/]").Border(BoxBorder.Rounded).Expand());
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
