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

        AnsiConsole.Write(BuildQuickCommandPanel(layout, scopeInventory, installedSkills.Select(record => record.Skill).ToArray(), availableSkills));
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
            AnsiConsole.Write(new Panel(new Markup("No direct package-based matches were found. Nothing was installed. Start with [bold]dotnet[/] and [bold]dotnet-modern-csharp[/] if you want a baseline .NET skill set."))
                .Header("Recommendations")
                .Expand());
            return;
        }

        var table = new Table().Expand();
        table.Title = new TableTitle("Recommendations");
        table.AddColumn("Skill");
        table.AddColumn("Confidence");
        table.AddColumn("Status");
        table.AddColumn("Signals");

        foreach (var recommendation in scanResult.Recommendations)
        {
            installedSkills.TryGetValue(recommendation.Skill.Name, out var installed);
            table.AddRow(
                BuildSkillCell(recommendation.Skill),
                FormatConfidence(recommendation.Confidence),
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

        if (installableSkills.Length > 0)
        {
            var command = $"dotnet skills install {string.Join(' ', installableSkills)}";
            var text = string.Join(
                Environment.NewLine,
                "Scan complete. Nothing is installed automatically.",
                "Review the proposed skills and run install only if the list looks right.",
                string.Empty,
                "Suggested command:",
                $"[green]{Escape(command)}[/]");

            AnsiConsole.Write(new Panel(new Markup(text))
                .Header("Review and confirm")
                .Expand());
        }
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
        WriteTitle("dotnet skills version");

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[grey]Package[/]"), new Markup(ToolVersionInfo.PackageId));
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

    public static void RenderUsage()
    {
        WriteTitle("dotnet-skills");

        var table = new Table().Expand();
        table.AddColumn("Command");
        table.AddColumn("Purpose");
        table.AddRow("[green]dotnet skills list[/]", "Show the current inventory, compare project/global scope when relevant, and render available skills in grouped category tables.");
        table.AddRow("[green]dotnet skills version[/]", "Show the current tool version and check whether NuGet has a newer release.");
        table.AddRow("[green]dotnet skills recommend[/]", "Scan `*.csproj` files, propose relevant `dotnet-*` skills, and let you decide what to install.");
        table.AddRow("[green]dotnet skills install aspire orleans[/]", "Install one or more skills by slug or short alias.");
        table.AddRow("[green]dotnet skills remove --all[/]", "Remove installed catalog skills from the selected target.");
        table.AddRow("[green]dotnet skills update[/]", "Refresh already installed catalog skills to the selected catalog version.");
        table.AddRow("[green]dotnet skills sync --force[/]", "Refresh the cached remote catalog payload.");
        table.AddRow("[green]dotnet skills where[/]", "Print the resolved install path.");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var notes = string.Join(
            Environment.NewLine,
            "- `list`, `recommend`, `install`, and `update` use the latest `catalog-v*` GitHub release by default.",
            "- `dotnet skills version` and `dotnet skills --version` both show the current tool version.",
            "- Use `dotnet skills version --no-check` when you only want the local installed version without a NuGet lookup.",
            "- `list` stays compact: it shows the current installed inventory and a grouped category summary for the remaining catalog.",
            "- `list --installed-only` and `list --local` are equivalent shortcuts for the installed inventory view; `list --available-only` expands the remaining catalog into per-category skill tables with short summaries.",
            "- `--bundled` skips the network and uses the catalog packaged with the tool.",
            "- `--catalog-version <version>` pins a specific remote catalog release.",
            "- `--refresh` forces `install` or `update` to redownload the selected remote catalog first.",
            "- Short aliases work everywhere: `aspire` resolves to `dotnet-aspire`.",
            "- Set `DOTNET_SKILLS_SKIP_UPDATE_CHECK=1` to suppress automatic tool update notices on startup.",
            "- Auto target detection probes `.codex`, `.claude`, `.github`, `.gemini`, and `.agents`; if none exist, it falls back to `./skills`.");

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
        Console.Error.WriteLine("Update: dotnet tool update --global dotnet-skills");
        Console.Error.WriteLine("If installed via a local tool manifest: dotnet tool update dotnet-skills");
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
            grid.AddRow(new Markup("[grey]Claude adapters[/]"), new Markup(generatedAdapters.ToString()));
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
            $"[green]{Escape("dotnet tool update --global dotnet-skills")}[/]",
            string.Empty,
            "Local tool manifest:",
            $"[green]{Escape("dotnet tool update dotnet-skills")}[/]");

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
                Escape(row.Skill.Description));
        }

        return table;
    }

    private static Panel BuildQuickCommandPanel(
        SkillInstallLayout layout,
        IReadOnlyList<ScopeInventoryRow> scopeInventory,
        IReadOnlyList<SkillEntry> installedSkills,
        IReadOnlyList<SkillEntry> availableSkills)
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
        SkillInstallMode.RawSkillPayloads => "Raw skill payloads",
        SkillInstallMode.ClaudeSubagents => "Claude subagents",
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
}

internal sealed record ScopeInventoryRow(
    InstallScope Scope,
    DirectoryInfo TargetRoot,
    IReadOnlyList<InstalledSkillRecord> InstalledSkills);

internal sealed record SkillActionRow(SkillEntry Skill, string FromVersion, string ToVersion, SkillAction Action);

internal enum SkillAction
{
    Installed,
    Removed,
    Updated,
    Missing,
    Skipped,
}
