using ManagedCode.DotnetSkills.Runtime;
using Spectre.Console;
using SpectreConsole = Spectre.Console.AnsiConsole;

namespace ManagedCode.DotnetSkills;

internal sealed class InteractiveConsoleApp
{
    private readonly IInteractivePrompts prompts;
    private readonly Func<bool, string?, string?, bool, Task<SkillCatalogPackage>> loadSkillCatalogAsync;
    private readonly Func<AgentCatalogPackage> loadAgentCatalog;
    private readonly Func<string?, Task<ToolUpdateStatusInfo?>> getToolUpdateStatusAsync;
    private readonly string? cachePath;
    private readonly string? catalogVersion;
    private SkillCatalogPackage skillCatalog = null!;
    private AgentCatalogPackage agentCatalog = null!;
    private ToolUpdateStatusInfo? toolUpdateStatus;

    public InteractiveConsoleApp(
        IInteractivePrompts? prompts = null,
        Func<bool, string?, string?, bool, Task<SkillCatalogPackage>>? loadSkillCatalogAsync = null,
        Func<AgentCatalogPackage>? loadAgentCatalog = null,
        Func<string?, Task<ToolUpdateStatusInfo?>>? getToolUpdateStatusAsync = null,
        string? cachePath = null,
        string? catalogVersion = null,
        bool bundledOnly = false,
        AgentPlatform initialAgent = AgentPlatform.Auto,
        InstallScope initialScope = InstallScope.Project,
        string? projectDirectory = null)
    {
        this.prompts = prompts ?? new CommandCenterInteractivePrompts();
        this.loadSkillCatalogAsync = loadSkillCatalogAsync ?? Program.ResolveCatalogForInstallAsync;
        this.loadAgentCatalog = loadAgentCatalog ?? AgentCatalogPackage.LoadBundled;
        this.getToolUpdateStatusAsync = getToolUpdateStatusAsync ?? (cache => Program.GetToolUpdateStatusAsync(cache));
        this.cachePath = cachePath;
        this.catalogVersion = catalogVersion;

        Session = new InteractiveSessionState
        {
            Agent = initialAgent,
            Scope = initialScope,
            ProjectDirectory = projectDirectory,
            BundledOnly = bundledOnly,
        };
    }

    internal InteractiveSessionState Session { get; }

    public async Task<int> RunAsync()
    {
        toolUpdateStatus = await getToolUpdateStatusAsync(cachePath);
        await LoadCatalogsAsync(refreshCatalog: false);

        while (true)
        {
            try
            {
                RenderDashboard();

                var homeActions = GetHomeActions(GetOutdatedSkillCount());
                var action = prompts.Select(
                    "Section",
                    homeActions,
                    option => option.Label);

                switch (action.Action)
                {
                    case HomeAction.SyncProject:
                        ShowProjectSync();
                        break;
                    case HomeAction.ManageBundles:
                        ShowPackages();
                        break;
                    case HomeAction.InstallSkills:
                        await ShowCatalogSkillsAsync();
                        break;
                    case HomeAction.Analysis:
                        ShowCatalogAnalysis();
                        break;
                    case HomeAction.ManageInstalled:
                        ShowInstalledSkills();
                        break;
                    case HomeAction.UpdateAll:
                        UpdateAllOutdatedSkillsForCurrentTarget();
                        break;
                    case HomeAction.Agents:
                        ShowAgents();
                        break;
                    case HomeAction.Settings:
                        await ShowSettingsAsync();
                        break;
                    case HomeAction.Exit:
                        return 0;
                }
            }
            catch (Exception exception)
            {
                RenderError(exception.Message);
            }
        }
    }

    private int GetOutdatedSkillCount()
    {
        var installer = new SkillInstaller(skillCatalog);
        return installer.GetInstalledSkills(ResolveSkillLayout()).Count(record => !record.IsCurrent);
    }

    private async Task LoadCatalogsAsync(bool refreshCatalog)
    {
        skillCatalog = await loadSkillCatalogAsync(Session.BundledOnly, cachePath, catalogVersion, refreshCatalog);
        agentCatalog = loadAgentCatalog();
    }

    private async Task RefreshCatalogAsync()
    {
        AnsiConsole.Clear();
        SpectreConsole.Write(BuildRichShellPanel("refreshing", new Spectre.Console.Markup("[dim]Refreshing catalog...[/]")));
        AnsiConsole.WriteLine();
        await LoadCatalogsAsync(refreshCatalog: true);

        var summary = BuildRichPropertyGrid(
            ("catalog", $"[green]{Escape(skillCatalog.CatalogVersion)}[/]"),
            ("source", Escape(skillCatalog.SourceLabel)),
            ("skills", skillCatalog.Skills.Count.ToString()),
            ("focused bundles", GetPrimaryBundles().Count.ToString()));
        SpectreConsole.Write(BuildRichShellPanel("refreshed", summary));
        prompts.Pause("Press any key to continue...");
    }

    private void RenderDashboard()
    {
        AnsiConsole.Clear();

        var skillLayout = ResolveSkillLayout();
        var skillInstaller = new SkillInstaller(skillCatalog);
        var installedSkills = skillInstaller.GetInstalledSkills(skillLayout);
        var outdatedSkills = installedSkills.Count(record => !record.IsCurrent);
        var agentStatus = ResolveAgentStatus();
        var primaryBundleCount = GetPrimaryBundles().Count;
        var collectionViews = BuildCollectionViews(installedSkills);
        var packageSignals = BuildPackageSignals();
        var totalTokens = skillCatalog.Skills.Sum(skill => skill.TokenCount);
        var largestSkills = skillCatalog.Skills
            .OrderByDescending(skill => skill.TokenCount)
            .ThenBy(skill => skill.Name, StringComparer.Ordinal)
            .Take(5)
            .ToArray();
        var featuredBundles = GetPrimaryBundles()
            .Take(5)
            .ToArray();
        var homeActions = GetHomeActions(outdatedSkills)
            .Where(action => action.Action != HomeAction.Exit)
            .ToArray();
        var consoleWidth = GetConsoleWidth();
        var wideMode = consoleWidth >= 132;
        var splitPanes = consoleWidth >= 165;
        var compactHeader = consoleWidth < 100;

        var header = compactHeader
            ? BuildRichStack(
                new Spectre.Console.Markup($"[bold deepskyblue1]dotnet skills[/] [dim]v{Escape(ToolVersionInfo.CurrentVersion)}[/]"),
                new Spectre.Console.Markup("[grey]collection-first control center[/]"),
                new Spectre.Console.Markup($"[bold springgreen3]{Escape(skillCatalog.CatalogVersion)}[/] [dim]{Escape(skillCatalog.SourceLabel)}[/]"))
            : BuildRichTwoColumn(
                BuildRichStack(
                    new Spectre.Console.Markup($"[bold deepskyblue1]dotnet skills[/] [dim]v{Escape(ToolVersionInfo.CurrentVersion)}[/]"),
                    new Spectre.Console.Markup("[grey]collection-first control center[/]")),
                BuildRichStack(
                    new Spectre.Console.Markup($"[bold springgreen3]{Escape(skillCatalog.CatalogVersion)}[/]"),
                    new Spectre.Console.Markup($"[dim]{Escape(skillCatalog.SourceLabel)}[/]")),
                gap: 4,
                noWrapLeft: false,
                noWrapRight: true);
        SpectreConsole.Write(BuildRichShellPanel("control center", header));
        AnsiConsole.WriteLine();

        Spectre.Console.Rendering.IRenderable navigation = wideMode
            ? BuildRichNavigationTable(homeActions)
            : BuildRichNavigationList(homeActions);

        var workspace = new Spectre.Console.Grid { Expand = true };
        workspace.AddColumn(new Spectre.Console.GridColumn { NoWrap = true, Padding = new Spectre.Console.Padding(0, 0, 2, 0) });
        workspace.AddColumn();
        workspace.AddRow(new Spectre.Console.Markup("[dim]catalog[/]"), new Spectre.Console.Markup($"{Escape(skillCatalog.SourceLabel)} [dim]({Escape(skillCatalog.CatalogVersion)})[/]"));
        workspace.AddRow(new Spectre.Console.Markup("[dim]session[/]"), new Spectre.Console.Markup($"{Escape(Session.Agent.ToString())} [dim]/[/] {Escape(Session.Scope.ToString())}"));
        workspace.AddRow(new Spectre.Console.Markup("[dim]project[/]"), new Spectre.Console.Markup($"[dim]{Escape(CompactPath(Program.ResolveProjectRoot(Session.ProjectDirectory)))}[/]"));
        workspace.AddRow(new Spectre.Console.Markup("[dim]target[/]"), new Spectre.Console.Markup($"[dim]{Escape(CompactPath(skillLayout.PrimaryRoot.FullName))}[/]"));
        workspace.AddRow(new Spectre.Console.Markup("[dim]agents[/]"), new Spectre.Console.Markup(Escape(CompactText(agentStatus.Summary, 64))));
        workspace.AddRow(new Spectre.Console.Markup("[dim]tokenizer[/]"), new Spectre.Console.Markup($"{Escape(SkillTokenCounter.ModelName)} [dim]({FormatTokenCount(totalTokens)})[/]"));

        var metricGrid = new Spectre.Console.Grid { Expand = true };
        metricGrid.AddColumn(new Spectre.Console.GridColumn { Padding = new Spectre.Console.Padding(0, 0, 2, 0) });
        metricGrid.AddColumn(new Spectre.Console.GridColumn { Padding = new Spectre.Console.Padding(0, 0, 2, 0) });
        metricGrid.AddColumn();
        metricGrid.AddRow(
            BuildRichMetricCard("Installed", $"{installedSkills.Count}/{skillCatalog.Skills.Count}", outdatedSkills == 0 ? "up to date" : $"{outdatedSkills} outdated", "deepskyblue1"),
            BuildRichMetricCard("Bundles", primaryBundleCount.ToString(), "focused installs", "springgreen3"),
            BuildRichMetricCard("Collections", collectionViews.Count.ToString(), $"{collectionViews.Sum(collection => collection.Lanes.Count)} lanes", "gold1"));
        metricGrid.AddRow(
            BuildRichMetricCard("Signals", packageSignals.Count.ToString(), "NuGet entry points", "turquoise2"),
            BuildRichMetricCard("Outdated", outdatedSkills.ToString(), outdatedSkills == 0 ? "installed skills current" : "update all available", outdatedSkills == 0 ? "green3" : "yellow"),
            BuildRichMetricCard("Tokens", FormatTokenCount(totalTokens), SkillTokenCounter.ModelName, "green3"));

        var stackTable = new Spectre.Console.Table().Border(Spectre.Console.TableBorder.None).Expand();
        stackTable.AddColumn("Collection");
        stackTable.AddColumn("In");
        stackTable.AddColumn("Tokens");
        foreach (var collection in collectionViews.Take(5))
        {
            stackTable.AddRow(
                Escape(CompactText(collection.Collection, 22)),
                $"{collection.InstalledCount}/{collection.SkillCount}",
                FormatTokenCount(collection.TokenCount));
        }

        var bundleTable = new Spectre.Console.Table().Border(Spectre.Console.TableBorder.None).Expand();
        bundleTable.AddColumn("Bundle");
        bundleTable.AddColumn("Area");
        bundleTable.AddColumn("Tokens");
        foreach (var bundle in featuredBundles)
        {
            var bundleTokens = bundle.Skills
                .Select(skillName => skillCatalog.Skills.FirstOrDefault(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase)))
                .Where(skill => skill is not null)
                .Sum(skill => skill!.TokenCount);
            bundleTable.AddRow(
                Escape(bundle.Name),
                Escape(CompactText(CatalogOrganization.ResolveBundleAreaLabel(bundle), 26)),
                FormatTokenCount(bundleTokens));
        }

        var surfaces = splitPanes
            ? BuildRichTwoColumn(stackTable, bundleTable, gap: 3)
            : BuildRichStack(stackTable, bundleTable);

        var heavyTable = new Spectre.Console.Table().Border(Spectre.Console.TableBorder.None).Expand();
        heavyTable.AddColumn("Skill");
        heavyTable.AddColumn("Area");
        heavyTable.AddColumn("Tokens");
        foreach (var skill in largestSkills)
        {
            heavyTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape(CompactText($"{skill.Stack} / {skill.Lane}", 28)),
                FormatTokenCount(skill.TokenCount));
        }

        var signalTable = new Spectre.Console.Table().Border(Spectre.Console.TableBorder.None).Expand();
        signalTable.AddColumn("Signal");
        signalTable.AddColumn("Skill");
        foreach (var signal in packageSignals.Take(5))
        {
            signalTable.AddRow(
                Escape(CompactText(signal.Signal, 24)),
                Escape(ToAlias(signal.Skill.Name)));
        }

        var analysis = splitPanes
            ? BuildRichTwoColumn(heavyTable, signalTable, gap: 3)
            : BuildRichStack(heavyTable, signalTable);

        var controlLines = new List<Spectre.Console.Rendering.IRenderable>
        {
            new Spectre.Console.Markup("[bold grey]Enter[/] [dim]select[/]   [bold grey]Space[/] [dim]multi-select[/]"),
            new Spectre.Console.Markup("[dim]Collections narrow before install[/]   [dim]Bundles stay focused[/]"),
            new Spectre.Console.Markup("[dim]Install preview stays mandatory before writes[/]"),
        };

        controlLines.Add(
            outdatedSkills > 0
                ? new Spectre.Console.Markup($"[yellow]Update all skills[/] [dim]is available for {outdatedSkills} outdated install(s)[/]")
                : new Spectre.Console.Markup("[yellow]Update all skills[/] [dim]stays available and returns a clear no-op when everything is current[/]"));

        var leftPanels = new List<Spectre.Console.Rendering.IRenderable>
        {
            BuildRichShellPanel("navigation", navigation),
            BuildRichShellPanel("workspace", workspace),
        };

        var toolUpdatePanel = BuildToolUpdatePanel(toolUpdateStatus);
        if (toolUpdatePanel is not null)
        {
            leftPanels.Add(toolUpdatePanel);
        }

        leftPanels.Add(BuildRichShellPanel("controls", BuildRichStack([.. controlLines])));
        var leftColumn = BuildRichStack([.. leftPanels]);

        var rightColumn = BuildRichStack(
            BuildRichShellPanel("catalog telemetry", metricGrid),
            BuildRichShellPanel("install surfaces", surfaces),
            BuildRichShellPanel("analysis", analysis));

        var content = wideMode
            ? BuildRichTwoColumn(leftColumn, rightColumn, gap: 3, noWrapLeft: true, noWrapRight: false, leftWidth: 58)
            : BuildRichStack(leftColumn, rightColumn);
        SpectreConsole.Write(content);
        AnsiConsole.WriteLine();
    }

    private static IReadOnlyList<HomeActionView> GetHomeActions(int outdatedSkillCount)
    {
        var actions = new List<HomeActionView>
        {
            new HomeActionView(HomeAction.SyncProject, "Project", "sync from .csproj signals", "dotnet skills install --auto", "deepskyblue1"),
            new HomeActionView(HomeAction.InstallSkills, "Collections", "browse Collection -> Lane -> Skill", "dotnet skills list --available-only", "springgreen3"),
            new HomeActionView(HomeAction.Analysis, "Analysis", "tree, tokens, package signals", "dotnet skills catalog tokens", "gold1"),
            new HomeActionView(HomeAction.ManageBundles, "Bundles", "focused multi-skill installs", "dotnet skills bundle list", "turquoise2"),
            new HomeActionView(HomeAction.ManageInstalled, "Installed", "keep, remove, clear, repair, move", "dotnet skills list --installed-only", "orange3"),
        };

        actions.Add(
            new HomeActionView(
                HomeAction.UpdateAll,
                "Update all skills",
                outdatedSkillCount == 0 ? "0 outdated installed skills" : $"{outdatedSkillCount} outdated installed skills",
                "dotnet skills update",
                "yellow"));

        actions.AddRange(
        [
            new HomeActionView(HomeAction.Agents, "Agents", "native agent lifecycle", "dotnet agents list", "green3"),
            new HomeActionView(HomeAction.Settings, "Workspace", "platform, scope, catalog source", "dotnet skills where", "deepskyblue1"),
            new HomeActionView(HomeAction.Exit, "Exit", "leave the control center", "exit", "grey"),
        ]);

        return actions;
    }

    private static Spectre.Console.Grid BuildRichStack(params Spectre.Console.Rendering.IRenderable[] items)
    {
        var grid = new Spectre.Console.Grid();
        grid.AddColumn();
        foreach (var item in items)
        {
            grid.AddRow(item);
        }

        return grid;
    }

    private static Spectre.Console.Panel BuildRichShellPanel(string title, Spectre.Console.Rendering.IRenderable content)
    {
        var panel = new Spectre.Console.Panel(content)
            .Header($"[bold deepskyblue1]{Escape(title)}[/]")
            .Border(Spectre.Console.BoxBorder.Rounded)
            .Expand();
        panel.Padding = new Spectre.Console.Padding(1, 0, 1, 0);
        return panel;
    }

    private static Spectre.Console.Panel BuildRichMetricCard(string title, string value, string detail, string accent)
    {
        var body = BuildRichStack(
            new Spectre.Console.Markup($"[{accent}]{Escape(title)}[/]"),
            new Spectre.Console.Markup($"[bold]{Escape(value)}[/]"),
            new Spectre.Console.Markup($"[grey]{Escape(detail)}[/]"));

        var panel = new Spectre.Console.Panel(body)
            .Border(Spectre.Console.BoxBorder.Rounded)
            .Expand();
        panel.Padding = new Spectre.Console.Padding(1, 0, 1, 0);
        return panel;
    }

    private static Spectre.Console.Panel? BuildToolUpdatePanel(ToolUpdateStatusInfo? status)
    {
        if (status?.HasUpdate != true)
        {
            return null;
        }

        var freshness = status.CheckedAt is null
            ? "[dim]latest release detected[/]"
            : status.UsedCachedValue
                ? $"[dim]cached[/] [grey]{Escape(status.CheckedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))}[/]"
                : $"[dim]checked[/] [grey]{Escape(status.CheckedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))}[/]";

        return BuildRichShellPanel(
            "tool update",
            BuildRichStack(
                new Spectre.Console.Markup("[bold yellow]New dotnet-skills version available[/]"),
                new Spectre.Console.Markup($"[dim]current[/] [grey]{Escape(status.CurrentVersion)}[/] [dim]-> latest[/] [green]{Escape(status.LatestVersion ?? "?")}[/]"),
                new Spectre.Console.Markup($"[green]{Escape(GlobalToolUpdateCommand)}[/]"),
                new Spectre.Console.Markup($"[dim]local tool manifest[/] [green]{Escape(LocalToolUpdateCommand)}[/]"),
                new Spectre.Console.Markup(freshness)));
    }

    private static Spectre.Console.Grid BuildRichTwoColumn(
        Spectre.Console.Rendering.IRenderable left,
        Spectre.Console.Rendering.IRenderable right,
        int gap,
        bool noWrapLeft = false,
        bool noWrapRight = false,
        int? leftWidth = null)
    {
        var grid = new Spectre.Console.Grid { Expand = true };
        var leftColumn = new Spectre.Console.GridColumn
        {
            Padding = new Spectre.Console.Padding(0, 0, gap, 0),
            NoWrap = noWrapLeft,
        };

        if (leftWidth.HasValue)
        {
            leftColumn.Width = leftWidth.Value;
        }

        grid.AddColumn(leftColumn);
        grid.AddColumn(new Spectre.Console.GridColumn { NoWrap = noWrapRight });
        grid.AddRow(left, right);
        return grid;
    }

    private static Spectre.Console.Table BuildRichNavigationTable(IReadOnlyList<HomeActionView> homeActions)
    {
        var table = new Spectre.Console.Table().Border(Spectre.Console.TableBorder.None).Expand();
        table.AddColumn("Area");
        table.AddColumn("Use");
        table.AddColumn("Command");

        foreach (var action in homeActions)
        {
            table.AddRow(
                $"[{action.Accent}]{Escape(action.Label)}[/]",
                $"[dim]{Escape(action.Summary)}[/]",
                $"[grey]{Escape(action.Command)}[/]");
        }

        return table;
    }

    private static Spectre.Console.Grid BuildRichNavigationList(IReadOnlyList<HomeActionView> homeActions)
    {
        var grid = new Spectre.Console.Grid { Expand = true };
        grid.AddColumn();
        foreach (var action in homeActions)
        {
            grid.AddRow(new Spectre.Console.Markup($"[{action.Accent}]{Escape(action.Label)}[/] [dim]{Escape(action.Summary)}[/]"));
            grid.AddRow(new Spectre.Console.Markup($"[grey]{Escape(action.Command)}[/]"));
        }

        return grid;
    }

    private static Spectre.Console.Grid BuildRichPropertyGrid(params (string Label, string Value)[] rows)
    {
        var grid = new Spectre.Console.Grid { Expand = true };
        grid.AddColumn(new Spectre.Console.GridColumn { NoWrap = true, Padding = new Spectre.Console.Padding(0, 0, 2, 0) });
        grid.AddColumn();

        foreach (var (label, value) in rows)
        {
            grid.AddRow(
                new Spectre.Console.Markup($"[dim]{Escape(label)}[/]"),
                new Spectre.Console.Markup(value));
        }

        return grid;
    }

    private static string FormatSkillActionMarkup(SkillAction action) => action switch
    {
        SkillAction.Installed => "[green]Installed[/]",
        SkillAction.Removed => "[red]Removed[/]",
        SkillAction.Updated => "[yellow]Updated[/]",
        SkillAction.Missing => "[grey]Missing[/]",
        SkillAction.Skipped => "[grey]Skipped[/]",
        _ => Escape(action.ToString()),
    };

    private static string FormatConfidenceMarkup(RecommendationConfidence confidence) => confidence switch
    {
        RecommendationConfidence.High => "[green]High[/]",
        RecommendationConfidence.Medium => "[yellow]Medium[/]",
        RecommendationConfidence.Low => "[grey]Low[/]",
        _ => Escape(confidence.ToString()),
    };

    private static string SummarizeAliases(IEnumerable<string> values, int take = 4)
    {
        var aliases = values
            .Select(ToAlias)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (aliases.Length == 0)
        {
            return "-";
        }

        var preview = aliases.Take(take).ToArray();
        return string.Join(", ", preview) + (aliases.Length > preview.Length ? $" (+{aliases.Length - preview.Length})" : string.Empty);
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth > 0 ? Console.WindowWidth : 120;
        }
        catch
        {
            return 120;
        }
    }

    private static string CompactPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = value.Replace('\\', '/');
        if (normalized.Length <= 54)
        {
            return normalized;
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(4)
            .ToArray();

        return segments.Length == 0
            ? CompactText(normalized, 54)
            : $".../{string.Join("/", segments)}";
    }

    private static string CompactText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return maxLength <= 1
            ? value[..maxLength]
            : $"{value[..(maxLength - 1)]}…";
    }

    private static string GlobalToolUpdateCommand => $"dotnet tool update --global {ToolIdentity.PackageId}";

    private static string LocalToolUpdateCommand => $"dotnet tool update {ToolIdentity.PackageId}";

    private async Task ShowCatalogSkillsAsync()
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var collectionViews = BuildCollectionViews(installedSkills);

            AnsiConsole.Clear();
            RenderCollectionBrowserPanel(collectionViews, layout);

            var actions = new List<MenuOption<SkillCatalogAction>>
            {
                new("Browse a collection", SkillCatalogAction.Inspect),
            };

            if (installedSkills.Any(record => !record.IsCurrent))
            {
                actions.Add(new MenuOption<SkillCatalogAction>("Update all outdated skills", SkillCatalogAction.UpdateAllOutdated));
                actions.Add(new MenuOption<SkillCatalogAction>("Review outdated skills", SkillCatalogAction.UpdateOutdated));
            }

            actions.Add(new MenuOption<SkillCatalogAction>("Back", SkillCatalogAction.Back));

            var action = prompts.Select("Collection actions", actions, option => option.Label);
            switch (action.Value)
            {
                case SkillCatalogAction.Inspect:
                {
                    var selectedCollection = prompts.Select(
                        "Browse a collection",
                        collectionViews,
                        BuildCollectionChoiceLabel);
                    ShowCollectionDetail(selectedCollection.Collection);
                    break;
                }
                case SkillCatalogAction.UpdateOutdated:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent)
                        .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    ReviewOutdatedSkills(
                        outdatedSkills,
                        layout,
                        "No outdated skills are installed in this target.",
                        "Review outdated skills",
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion} -> {record.Skill.Version})");

                    break;
                }
                case SkillCatalogAction.UpdateAllOutdated:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent)
                        .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    UpdateAllOutdatedSkills(
                        outdatedSkills,
                        layout,
                        "No outdated skills are installed in this target.");

                    break;
                }
                case SkillCatalogAction.Back:
                    return;
            }
        }
    }

    private void ShowCatalogAnalysis()
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var collectionViews = BuildCollectionViews(installedSkills);
            var packageSignals = BuildPackageSignals();

            AnsiConsole.Clear();
            RenderCatalogAnalysisPanel(collectionViews, layout, packageSignals);

            var action = prompts.Select(
                "Catalog analysis",
                new[]
                {
                    new MenuOption<CatalogAnalysisAction>("View full skill tree", CatalogAnalysisAction.Tree),
                    new MenuOption<CatalogAnalysisAction>("Inspect heaviest skill", CatalogAnalysisAction.HeavySkill),
                    new MenuOption<CatalogAnalysisAction>("Browse package signals", CatalogAnalysisAction.PackageSignals),
                    new MenuOption<CatalogAnalysisAction>("Back", CatalogAnalysisAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case CatalogAnalysisAction.Tree:
                    ShowCatalogTree(collectionViews);
                    break;
                case CatalogAnalysisAction.HeavySkill:
                {
                    var selectedSkill = prompts.Select(
                        "Inspect heaviest skill",
                        skillCatalog.Skills
                            .OrderByDescending(skill => skill.TokenCount)
                            .ThenBy(skill => skill.Name, StringComparer.Ordinal)
                            .Take(24)
                            .ToArray(),
                        skill => $"{ToAlias(skill.Name)} [{skill.Stack} / {skill.Lane}] ({FormatTokenCount(skill.TokenCount)} tokens)");
                    ShowSkillDetail(selectedSkill);
                    break;
                }
                case CatalogAnalysisAction.PackageSignals:
                    ShowPackageSignals();
                    break;
                case CatalogAnalysisAction.Back:
                    return;
            }
        }
    }

    private void ShowCatalogTree(IReadOnlyList<CollectionCatalogView> collectionViews)
    {
        while (true)
        {
            AnsiConsole.Clear();
            RenderCatalogTreePanel(collectionViews);

            var action = prompts.Select(
                "Tree view",
                new[]
                {
                    new MenuOption<CatalogTreeAction>("Back", CatalogTreeAction.Back),
                },
                option => option.Label);

            if (action.Value == CatalogTreeAction.Back)
            {
                return;
            }
        }
    }

    private void ShowPackageSignals()
    {
        while (true)
        {
            var packageSignals = BuildPackageSignals();
            AnsiConsole.Clear();
            RenderPackageSignalPanel(packageSignals);

            var action = prompts.Select(
                "Package signals",
                new[]
                {
                    new MenuOption<PackageSignalAction>("Inspect a linked skill", PackageSignalAction.InspectSkill),
                    new MenuOption<PackageSignalAction>("Back", PackageSignalAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case PackageSignalAction.InspectSkill:
                {
                    var signal = prompts.Select(
                        "Inspect a linked skill",
                        packageSignals.ToArray(),
                        entry => $"{entry.Signal} [{entry.Kind}] -> {ToAlias(entry.Skill.Name)} [{entry.Skill.Stack} / {entry.Skill.Lane}]");
                    ShowSkillDetail(signal.Skill);
                    break;
                }
                case PackageSignalAction.Back:
                    return;
            }
        }
    }

    private void ShowCollectionDetail(string collectionName)
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var collectionView = BuildCollectionViews(installedSkills)
                .FirstOrDefault(view => string.Equals(view.Collection, collectionName, StringComparison.OrdinalIgnoreCase));

            if (collectionView is null)
            {
                RenderInfo($"Collection {collectionName} is not available in this catalog version.");
                return;
            }

            AnsiConsole.Clear();
            RenderCollectionDetailPanel(collectionView, layout);

            var actions = new List<MenuOption<SkillCatalogAction>>
            {
                new("Inspect a lane", SkillCatalogAction.Inspect),
                new("Install from a lane", SkillCatalogAction.Install),
            };

            if (installedSkills.Any(record => !record.IsCurrent && string.Equals(record.Skill.Stack, collectionView.Collection, StringComparison.OrdinalIgnoreCase)))
            {
                actions.Add(new MenuOption<SkillCatalogAction>("Update all outdated skills in this collection", SkillCatalogAction.UpdateAllOutdated));
                actions.Add(new MenuOption<SkillCatalogAction>("Review outdated skills in this collection", SkillCatalogAction.UpdateOutdated));
            }

            actions.Add(new MenuOption<SkillCatalogAction>("Back", SkillCatalogAction.Back));

            var action = prompts.Select("Collection actions", actions, option => option.Label);
            switch (action.Value)
            {
                case SkillCatalogAction.Inspect:
                {
                    var selectedLane = prompts.Select(
                        "Inspect a lane",
                        collectionView.Lanes,
                        BuildLaneChoiceLabel);
                    var selectedSkill = prompts.Select(
                        "Inspect a skill",
                        selectedLane.Skills.OrderBy(skill => skill.Name, StringComparer.Ordinal).ToArray(),
                        skill => BuildSkillChoiceLabel(skill, installedSkills));
                    ShowSkillDetail(selectedSkill);
                    break;
                }
                case SkillCatalogAction.Install:
                {
                    var selectedLane = prompts.Select(
                        "Install from a lane",
                        collectionView.Lanes,
                        BuildLaneChoiceLabel);
                    var installableSkills = selectedLane.Skills
                        .Where(skill => installedSkills.All(record => !string.Equals(record.Skill.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                        .ToArray();

                    if (installableSkills.Length == 0)
                    {
                        RenderInfo($"Everything in {collectionView.Collection} / {selectedLane.Lane} is already installed in this target.");
                        break;
                    }

                    var selectedSkills = prompts.MultiSelect(
                        "Install skills",
                        installableSkills,
                        skill => $"{ToAlias(skill.Name)} [{skill.Lane}] ({FormatTokenCount(skill.TokenCount)} tokens)");
                    if (selectedSkills.Count == 0)
                    {
                        break;
                    }

                    if (ConfirmSkillInstallPreview($"Lane install: {collectionView.Collection} / {selectedLane.Lane}", selectedSkills, layout, force: false))
                    {
                        InstallSkills(selectedSkills, layout, force: false);
                    }

                    break;
                }
                case SkillCatalogAction.UpdateOutdated:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent && string.Equals(record.Skill.Stack, collectionView.Collection, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(record => record.Skill.Lane, StringComparer.Ordinal)
                        .ThenBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    ReviewOutdatedSkills(
                        outdatedSkills,
                        layout,
                        $"No outdated skills are installed in the {collectionView.Collection} collection.",
                        "Review outdated skills",
                        record => $"{ToAlias(record.Skill.Name)} [{record.Skill.Lane}] ({record.InstalledVersion} -> {record.Skill.Version})");

                    break;
                }
                case SkillCatalogAction.UpdateAllOutdated:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent && string.Equals(record.Skill.Stack, collectionView.Collection, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(record => record.Skill.Lane, StringComparer.Ordinal)
                        .ThenBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    UpdateAllOutdatedSkills(
                        outdatedSkills,
                        layout,
                        $"No outdated skills are installed in the {collectionView.Collection} collection.");

                    break;
                }
                case SkillCatalogAction.Back:
                    return;
            }
        }
    }

    private void ShowInstalledSkills()
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var scopeInventory = Program.BuildScopeInventory(layout, Session.ProjectDirectory, installer, installedSkills);

            AnsiConsole.Clear();
            RenderInstalledSkillsPanel(layout, installedSkills, scopeInventory);

            var actions = new List<MenuOption<InstalledSkillsAction>>
            {
                new("Inspect an installed skill", InstalledSkillsAction.Inspect),
            };

            if (installedSkills.Count > 0)
            {
                actions.Add(new MenuOption<InstalledSkillsAction>("Review installed set", InstalledSkillsAction.ReviewState));
                actions.Add(new MenuOption<InstalledSkillsAction>("Repair/optimize installed skills", InstalledSkillsAction.Repair));
                actions.Add(new MenuOption<InstalledSkillsAction>("Copy or move skills to another target", InstalledSkillsAction.CopyOrMove));
                actions.Add(new MenuOption<InstalledSkillsAction>("Remove selected installed skills", InstalledSkillsAction.Remove));
                actions.Add(new MenuOption<InstalledSkillsAction>("Clear this target", InstalledSkillsAction.RemoveAll));
            }

            if (installedSkills.Any(record => !record.IsCurrent))
            {
                actions.Add(new MenuOption<InstalledSkillsAction>("Update all outdated skills", InstalledSkillsAction.UpdateAll));
                actions.Add(new MenuOption<InstalledSkillsAction>("Review outdated skills", InstalledSkillsAction.Update));
            }

            actions.Add(new MenuOption<InstalledSkillsAction>("Back", InstalledSkillsAction.Back));

            var action = prompts.Select("Installed skill actions", actions, option => option.Label);
            switch (action.Value)
            {
                case InstalledSkillsAction.Inspect:
                {
                    if (installedSkills.Count == 0)
                    {
                        RenderInfo("No catalog skills are installed in this target yet.");
                        break;
                    }

                    var selected = prompts.Select(
                        "Inspect an installed skill",
                        installedSkills.OrderBy(record => record.Skill.Name, StringComparer.Ordinal).ToArray(),
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion})");
                    ShowSkillDetail(selected.Skill);
                    break;
                }
                case InstalledSkillsAction.Repair:
                {
                    if (installedSkills.Count == 0)
                    {
                        RenderInfo("No catalog skills are installed in this target yet.");
                        break;
                    }

                    var selected = prompts.MultiSelect(
                        "Repair/optimize installed skills",
                        installedSkills.OrderBy(record => record.Skill.Name, StringComparer.Ordinal).ToArray(),
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion})");
                    if (selected.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Force reinstall {selected.Count} skill(s) in {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        RepairSkills(selected);
                    }

                    break;
                }
                case InstalledSkillsAction.ReviewState:
                {
                    if (installedSkills.Count == 0)
                    {
                        RenderInfo("No catalog skills are installed in this target yet.");
                        break;
                    }

                    var orderedInstalled = installedSkills
                        .OrderBy(record => record.Skill.Stack, StringComparer.Ordinal)
                        .ThenBy(record => record.Skill.Lane, StringComparer.Ordinal)
                        .ThenBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();

                    var kept = prompts.MultiSelect(
                        "Review installed set",
                        orderedInstalled,
                        BuildInstalledSkillChoiceLabel,
                        orderedInstalled);
                    var keptNames = kept
                        .Select(record => record.Skill.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var removed = orderedInstalled
                        .Where(record => !keptNames.Contains(record.Skill.Name))
                        .Select(record => record.Skill)
                        .ToArray();

                    if (removed.Length == 0)
                    {
                        RenderInfo("No removal plan was created. The reviewed installed set is unchanged.");
                        break;
                    }

                    var confirmation = removed.Length == orderedInstalled.Length
                        ? $"Clear {layout.PrimaryRoot.FullName} by removing all {removed.Length} installed skill(s)?"
                        : $"Apply the reviewed installed set by removing {removed.Length} skill(s) from {layout.PrimaryRoot.FullName}?";
                    if (prompts.Confirm(confirmation, defaultValue: false))
                    {
                        RemoveSkills(removed, layout, pause: true);
                    }

                    break;
                }
                case InstalledSkillsAction.CopyOrMove:
                {
                    if (installedSkills.Count == 0)
                    {
                        RenderInfo("No catalog skills are installed in this target yet.");
                        break;
                    }

                    var selected = prompts.MultiSelect(
                        "Copy or move skills",
                        installedSkills.OrderBy(record => record.Skill.Name, StringComparer.Ordinal).ToArray(),
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion})");
                    if (selected.Count == 0)
                    {
                        break;
                    }

                    CopyOrMoveSkills(selected.Select(record => record.Skill).ToArray(), layout);
                    break;
                }
                case InstalledSkillsAction.Remove:
                {
                    if (installedSkills.Count == 0)
                    {
                        RenderInfo("No catalog skills are installed in this target yet.");
                        break;
                    }

                    var selected = prompts.MultiSelect(
                        "Remove installed skills",
                        installedSkills.OrderBy(record => record.Skill.Name, StringComparer.Ordinal).ToArray(),
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion})");
                    if (selected.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Remove {selected.Count} skill(s) from {layout.PrimaryRoot.FullName}?", defaultValue: false))
                    {
                        RemoveSkills(selected.Select(record => record.Skill).ToArray());
                    }

                    break;
                }
                case InstalledSkillsAction.RemoveAll:
                {
                    if (installedSkills.Count == 0)
                    {
                        RenderInfo("No catalog skills are installed in this target yet.");
                        break;
                    }

                    if (prompts.Confirm($"Clear {layout.PrimaryRoot.FullName} by removing all {installedSkills.Count} installed skill(s)?", defaultValue: false))
                    {
                        RemoveSkills(installedSkills.Select(record => record.Skill).ToArray(), layout, pause: true);
                    }

                    break;
                }
                case InstalledSkillsAction.Update:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent)
                        .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    ReviewOutdatedSkills(
                        outdatedSkills,
                        layout,
                        "No outdated skills are installed in this target.",
                        "Review outdated skills",
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion} -> {record.Skill.Version})");

                    break;
                }
                case InstalledSkillsAction.UpdateAll:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent)
                        .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    UpdateAllOutdatedSkills(
                        outdatedSkills,
                        layout,
                        "No outdated skills are installed in this target.");

                    break;
                }
                case InstalledSkillsAction.Back:
                    return;
            }
        }
    }

    private void ShowSkillDetail(SkillEntry skill)
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installed = installer.GetInstalledSkills(layout)
                .FirstOrDefault(record => string.Equals(record.Skill.Name, skill.Name, StringComparison.OrdinalIgnoreCase));

            AnsiConsole.Clear();
            RenderSkillDetailPanel(skill, installed, layout);

            var actions = new List<MenuOption<SkillDetailAction>>();
            if (installed is null)
            {
                actions.Add(new MenuOption<SkillDetailAction>("Install this skill", SkillDetailAction.Install));
            }
            else
            {
                if (!installed.IsCurrent)
                {
                    actions.Add(new MenuOption<SkillDetailAction>("Update this skill", SkillDetailAction.Update));
                }

                actions.Add(new MenuOption<SkillDetailAction>("Repair/optimize this skill", SkillDetailAction.Repair));
                actions.Add(new MenuOption<SkillDetailAction>("Copy or move this skill", SkillDetailAction.CopyOrMove));
                actions.Add(new MenuOption<SkillDetailAction>("Remove this skill", SkillDetailAction.Remove));
            }

            actions.Add(new MenuOption<SkillDetailAction>("Back", SkillDetailAction.Back));

            var action = prompts.Select("Skill actions", actions, option => option.Label);
            switch (action.Value)
            {
                case SkillDetailAction.Install:
                    if (ConfirmSkillInstallPreview($"Install skill: {ToAlias(skill.Name)}", [skill], layout, force: false))
                    {
                        InstallSkills([skill], layout, force: false);
                    }

                    break;
                case SkillDetailAction.Update:
                    if (installed is not null && prompts.Confirm($"Update {ToAlias(skill.Name)} to {skill.Version}?", defaultValue: true))
                    {
                        UpdateSkills([installed]);
                    }

                    break;
                case SkillDetailAction.Remove:
                    if (prompts.Confirm($"Remove {ToAlias(skill.Name)} from {layout.PrimaryRoot.FullName}?", defaultValue: false))
                    {
                        RemoveSkills([skill]);
                    }

                    break;
                case SkillDetailAction.Repair:
                    if (prompts.Confirm($"Force reinstall {ToAlias(skill.Name)}?", defaultValue: true))
                    {
                        InstallSkills([skill], force: true);
                    }

                    break;
                case SkillDetailAction.CopyOrMove:
                    CopyOrMoveSkills([skill], layout);
                    break;
                case SkillDetailAction.Back:
                    return;
            }
        }
    }

    private void ShowPackages()
    {
        while (true)
        {
            var visibleBundles = GetPrimaryBundles();
            AnsiConsole.Clear();
            RenderBundleBrowserPanel(visibleBundles);

            var action = prompts.Select(
                "Focused bundle actions",
                new[]
                {
                    new MenuOption<PackageAction>("Inspect a focused bundle", PackageAction.Inspect),
                    new MenuOption<PackageAction>("Install focused bundles", PackageAction.Install),
                    new MenuOption<PackageAction>("Back", PackageAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case PackageAction.Inspect:
                {
                    if (visibleBundles.Count == 0)
                    {
                        RenderInfo("No focused bundles are available in this catalog version yet.");
                        break;
                    }

                    var selectedPackage = prompts.Select(
                        "Inspect a focused bundle",
                        visibleBundles.ToArray(),
                        package => $"{package.Name} [{CatalogOrganization.ResolveBundleAreaLabel(package)}] ({package.Skills.Count} skills)");
                    ShowPackageDetail(selectedPackage);
                    break;
                }
                case PackageAction.Install:
                {
                    if (visibleBundles.Count == 0)
                    {
                        RenderInfo("No focused bundles are available in this catalog version yet.");
                        break;
                    }

                    var selectedPackages = prompts.MultiSelect(
                        "Install focused bundles",
                        visibleBundles.ToArray(),
                        package => $"{package.Name} [{CatalogOrganization.ResolveBundleAreaLabel(package)}] ({package.Skills.Count} skills)");
                    if (selectedPackages.Count == 0)
                    {
                        break;
                    }

                    var layout = ResolveSkillLayout();
                    var installer = new SkillInstaller(skillCatalog);
                    var selectedSkills = installer.SelectSkillsFromPackages(selectedPackages.Select(package => package.Name).ToArray());
                    if (ConfirmSkillInstallPreview("Bundle install", selectedSkills, layout, force: false, bundles: selectedPackages))
                    {
                        InstallSkills(selectedSkills, layout, force: false);
                    }

                    break;
                }
                case PackageAction.Back:
                    return;
            }
        }
    }

    private void ShowPackageDetail(SkillPackageEntry package)
    {
        while (true)
        {
            AnsiConsole.Clear();
            RenderPackageDetailPanel(package);

            var action = prompts.Select(
                "Focused bundle actions",
                new[]
                {
                    new MenuOption<PackageDetailAction>("Install this bundle", PackageDetailAction.Install),
                    new MenuOption<PackageDetailAction>("Back", PackageDetailAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case PackageDetailAction.Install:
                {
                    var layout = ResolveSkillLayout();
                    var installer = new SkillInstaller(skillCatalog);
                    var selectedSkills = installer.SelectSkillsFromPackages([package.Name]);
                    if (ConfirmSkillInstallPreview($"Bundle install: {package.Name}", selectedSkills, layout, force: false, bundles: [package]))
                    {
                        InstallSkills(selectedSkills, layout, force: false);
                    }

                    break;
                }
                case PackageDetailAction.Back:
                    return;
            }
        }
    }

    private void ShowProjectSync()
    {
        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var autoSyncService = new ProjectSkillAutoSyncService(skillCatalog);

        ProjectSkillAutoSyncPlan plan;
        try
        {
            plan = autoSyncService.BuildPlan(Session.ProjectDirectory, layout, installer, prune: false);
        }
        catch (InvalidOperationException exception)
        {
            RenderInfo(exception.Message);
            return;
        }

        if (plan.DesiredSkills.Count == 0)
        {
            RenderInfo("No NuGet packages in this project matched catalog skills for auto-install.");
            return;
        }

        var installedBefore = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);

        var newSkills = plan.DesiredSkills
            .Where(skill => !installedBefore.ContainsKey(skill.Name))
            .ToArray();

        AnsiConsole.Clear();
        RenderProjectSyncPanel(plan, layout, installedBefore, newSkills);

        if (newSkills.Length == 0)
        {
            prompts.Pause("Press any key to continue...");
            return;
        }

        if (ConfirmSkillInstallPreview("Project sync install", newSkills, layout, force: false))
        {
            InstallSkills(newSkills, layout, force: false);
            autoSyncService.SaveState(layout, plan);
        }
    }

    private void ShowAgents()
    {
        while (true)
        {
            var layout = TryResolveAgentLayout(out var layoutError);
            var installer = new AgentInstaller(agentCatalog);
            var installedAgents = layout is null ? [] : installer.GetInstalledAgents(layout);

            AnsiConsole.Clear();
            RenderAgentBrowserPanel(layout, layoutError, installedAgents);

            var actions = new List<MenuOption<AgentAction>>
            {
                new("Inspect an agent", AgentAction.Inspect),
            };

            if (layout is not null)
            {
                actions.Add(new MenuOption<AgentAction>("Install agents", AgentAction.Install));

                if (installedAgents.Count > 0)
                {
                    actions.Add(new MenuOption<AgentAction>("Repair/optimize installed agents", AgentAction.Repair));
                    actions.Add(new MenuOption<AgentAction>("Copy or move agents to another target", AgentAction.CopyOrMove));
                    actions.Add(new MenuOption<AgentAction>("Remove installed agents", AgentAction.Remove));
                }
            }

            actions.Add(new MenuOption<AgentAction>("Back", AgentAction.Back));

            var action = prompts.Select("Agent actions", actions, option => option.Label);
            switch (action.Value)
            {
                case AgentAction.Inspect:
                {
                    if (agentCatalog.Agents.Count == 0)
                    {
                        RenderInfo("No agents are available in the bundled catalog.");
                        break;
                    }

                    var agent = prompts.Select(
                        "Inspect an agent",
                        agentCatalog.Agents.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray(),
                        entry => $"{ToAlias(entry.Name)} ({entry.Model})");
                    ShowAgentDetail(agent);
                    break;
                }
                case AgentAction.Install:
                {
                    if (layout is null)
                    {
                        RenderInfo(layoutError ?? "Select a concrete agent platform before installing agents.");
                        break;
                    }

                    var selectedAgents = prompts.MultiSelect(
                        "Install agents",
                        agentCatalog.Agents.OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray(),
                        entry => $"{ToAlias(entry.Name)} ({entry.Model})");
                    if (selectedAgents.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Install {selectedAgents.Count} agent(s) into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallAgents(selectedAgents, layout, force: false);
                    }

                    break;
                }
                case AgentAction.Repair:
                {
                    if (layout is null || installedAgents.Count == 0)
                    {
                        RenderInfo("No installed agents are available in the current target.");
                        break;
                    }

                    var selectedAgents = prompts.MultiSelect(
                        "Repair/optimize installed agents",
                        installedAgents.OrderBy(record => record.Agent.Name, StringComparer.Ordinal).ToArray(),
                        record => ToAlias(record.Agent.Name));
                    if (selectedAgents.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Force reinstall {selectedAgents.Count} agent(s) into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallAgents(selectedAgents.Select(record => record.Agent).ToArray(), layout, force: true);
                    }

                    break;
                }
                case AgentAction.CopyOrMove:
                {
                    if (layout is null || installedAgents.Count == 0)
                    {
                        RenderInfo("No installed agents are available in the current target.");
                        break;
                    }

                    var selectedAgents = prompts.MultiSelect(
                        "Copy or move agents",
                        installedAgents.OrderBy(record => record.Agent.Name, StringComparer.Ordinal).ToArray(),
                        record => ToAlias(record.Agent.Name));
                    if (selectedAgents.Count == 0)
                    {
                        break;
                    }

                    CopyOrMoveAgents(selectedAgents.Select(record => record.Agent).ToArray(), layout);
                    break;
                }
                case AgentAction.Remove:
                {
                    if (layout is null || installedAgents.Count == 0)
                    {
                        RenderInfo("No installed agents are available in the current target.");
                        break;
                    }

                    var selectedAgents = prompts.MultiSelect(
                        "Remove installed agents",
                        installedAgents.OrderBy(record => record.Agent.Name, StringComparer.Ordinal).ToArray(),
                        record => ToAlias(record.Agent.Name));
                    if (selectedAgents.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Remove {selectedAgents.Count} agent(s) from {layout.PrimaryRoot.FullName}?", defaultValue: false))
                    {
                        RemoveAgents(selectedAgents.Select(record => record.Agent).ToArray(), layout);
                    }

                    break;
                }
                case AgentAction.Back:
                    return;
            }
        }
    }

    private void ShowAgentDetail(AgentEntry agent)
    {
        while (true)
        {
            var layout = TryResolveAgentLayout(out var layoutError);
            var installer = new AgentInstaller(agentCatalog);
            var installed = layout is not null && installer.IsInstalled(agent, layout);

            AnsiConsole.Clear();
            RenderAgentDetailPanel(agent, layout, layoutError, installed);

            var actions = new List<MenuOption<AgentDetailAction>>();
            if (layout is not null)
            {
                if (installed)
                {
                    actions.Add(new MenuOption<AgentDetailAction>("Repair/optimize this agent", AgentDetailAction.Repair));
                    actions.Add(new MenuOption<AgentDetailAction>("Copy or move this agent", AgentDetailAction.CopyOrMove));
                    actions.Add(new MenuOption<AgentDetailAction>("Remove this agent", AgentDetailAction.Remove));
                }
                else
                {
                    actions.Add(new MenuOption<AgentDetailAction>("Install this agent", AgentDetailAction.Install));
                }
            }

            actions.Add(new MenuOption<AgentDetailAction>("Back", AgentDetailAction.Back));

            var action = prompts.Select("Agent actions", actions, option => option.Label);
            switch (action.Value)
            {
                case AgentDetailAction.Install:
                    if (layout is not null && prompts.Confirm($"Install {ToAlias(agent.Name)} into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallAgents([agent], layout, force: false);
                    }

                    break;
                case AgentDetailAction.Remove:
                    if (layout is not null && prompts.Confirm($"Remove {ToAlias(agent.Name)} from {layout.PrimaryRoot.FullName}?", defaultValue: false))
                    {
                        RemoveAgents([agent], layout);
                    }

                    break;
                case AgentDetailAction.Repair:
                    if (layout is not null && prompts.Confirm($"Force reinstall {ToAlias(agent.Name)} into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallAgents([agent], layout, force: true);
                    }

                    break;
                case AgentDetailAction.CopyOrMove:
                    if (layout is not null)
                    {
                        CopyOrMoveAgents([agent], layout);
                    }

                    break;
                case AgentDetailAction.Back:
                    return;
            }
        }
    }

    private void ShowSessionTarget()
    {
        while (true)
        {
            AnsiConsole.Clear();
            RenderSessionTargetPanel();

            var action = prompts.Select(
                "Destination settings",
                new[]
                {
                    new MenuOption<SessionTargetAction>("Platform", SessionTargetAction.Platform),
                    new MenuOption<SessionTargetAction>("Scope", SessionTargetAction.Scope),
                    new MenuOption<SessionTargetAction>("Back", SessionTargetAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case SessionTargetAction.Platform:
                {
                    var selectedPlatform = prompts.Select(
                        "Select a platform",
                        Enum.GetValues<AgentPlatform>(),
                        platform => platform.ToString());
                    Session.Agent = selectedPlatform;
                    break;
                }
                case SessionTargetAction.Scope:
                {
                    var selectedScope = prompts.Select(
                        "Select a scope",
                        Enum.GetValues<InstallScope>(),
                        scope => scope.ToString());
                    Session.Scope = selectedScope;
                    break;
                }
                case SessionTargetAction.Back:
                    return;
            }
        }
    }

    private async Task ShowSettingsAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            RenderSessionTargetPanel();

            var action = prompts.Select(
                "Settings",
                new[]
                {
                    new MenuOption<SettingsAction>("Install destination", SettingsAction.InstallDestination),
                    new MenuOption<SettingsAction>("Refresh catalog", SettingsAction.RefreshCatalog),
                    new MenuOption<SettingsAction>("Help", SettingsAction.Help),
                    new MenuOption<SettingsAction>("Back", SettingsAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case SettingsAction.InstallDestination:
                    ShowSessionTarget();
                    break;
                case SettingsAction.RefreshCatalog:
                    await RefreshCatalogAsync();
                    break;
                case SettingsAction.Help:
                    AnsiConsole.Clear();
                    RenderHelpPanel();
                    prompts.Pause("Press any key to return to the interactive shell...");
                    break;
                case SettingsAction.Back:
                    return;
            }
        }
    }

    private void InstallSkills(IReadOnlyList<SkillEntry> skills, bool force)
    {
        var layout = ResolveSkillLayout();
        InstallSkills(skills, layout, force);
    }

    private void InstallSkills(IReadOnlyList<SkillEntry> skills, SkillInstallLayout layout, bool force)
    {
        var installer = new SkillInstaller(skillCatalog);
        var installedBefore = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var summary = installer.Install(skills, layout, force);
        var rows = Program.BuildInstallRows(skills, installedBefore, force, summary);

        AnsiConsole.Clear();
        RenderSkillOperationSummary(
            force ? "Repair results" : "Install results",
            force ? "rewritten" : "installed",
            force ? $"[yellow]{summary.InstalledCount}[/]" : $"[green]{summary.InstalledCount}[/]",
            layout,
            rows,
            skipped: summary.SkippedExisting,
            generatedAdapters: summary.GeneratedAdapters);
        prompts.Pause("Press any key to return to the interactive shell...");
    }

    private void RepairSkills(IReadOnlyList<InstalledSkillRecord> skills)
    {
        var layout = ResolveSkillLayout();
        InstallSkills(skills.Select(record => record.Skill).ToArray(), layout, force: true);
    }

    private void InstallPackages(IReadOnlyList<SkillPackageEntry> packages)
    {
        var installer = new SkillInstaller(skillCatalog);
        var skills = installer.SelectSkillsFromPackages(packages.Select(package => package.Name).ToArray());
        InstallSkills(skills, force: false);
    }

    private void RemoveSkills(IReadOnlyList<SkillEntry> skills)
    {
        var layout = ResolveSkillLayout();
        RemoveSkills(skills, layout, pause: true);
    }

    private void RemoveSkills(IReadOnlyList<SkillEntry> skills, SkillInstallLayout layout, bool pause)
    {
        var installer = new SkillInstaller(skillCatalog);
        var installedBefore = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var summary = installer.Remove(skills, layout);
        var rows = Program.BuildRemoveRows(skills, installedBefore, summary);

        AnsiConsole.Clear();
        RenderSkillOperationSummary(
            "Remove results",
            "removed",
            summary.RemovedCount == 0 ? "[grey]0[/]" : $"[red]{summary.RemovedCount}[/]",
            layout,
            rows,
            statusMessage: rows.Count == 0 ? "No matching catalog skills were removed from this target." : null);
        if (pause)
        {
            prompts.Pause("Press any key to return to the interactive shell...");
        }
    }

    private void UpdateAllOutdatedSkillsForCurrentTarget()
    {
        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var outdatedSkills = installer.GetInstalledSkills(layout)
            .Where(record => !record.IsCurrent)
            .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
            .ToArray();

        UpdateAllOutdatedSkills(
            outdatedSkills,
            layout,
            "No outdated skills are installed in this target.");
    }

    private void UpdateAllOutdatedSkills(
        IReadOnlyList<InstalledSkillRecord> outdatedSkills,
        SkillInstallLayout layout,
        string emptyMessage)
    {
        if (outdatedSkills.Count == 0)
        {
            RenderInfo(emptyMessage);
            return;
        }

        if (prompts.Confirm($"Update all {outdatedSkills.Count} outdated skill(s) in {layout.PrimaryRoot.FullName}?", defaultValue: true))
        {
            UpdateSkills(outdatedSkills);
        }
    }

    private void ReviewOutdatedSkills(
        IReadOnlyList<InstalledSkillRecord> outdatedSkills,
        SkillInstallLayout layout,
        string emptyMessage,
        string title,
        Func<InstalledSkillRecord, string> formatter)
    {
        if (outdatedSkills.Count == 0)
        {
            RenderInfo(emptyMessage);
            return;
        }

        var selected = prompts.MultiSelect(
            title,
            outdatedSkills,
            formatter,
            outdatedSkills);
        if (selected.Count == 0)
        {
            return;
        }

        if (prompts.Confirm($"Update {selected.Count} skill(s) in {layout.PrimaryRoot.FullName}?", defaultValue: true))
        {
            UpdateSkills(selected);
        }
    }

    private void UpdateSkills(IReadOnlyList<InstalledSkillRecord> skills)
    {
        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        installer.Install(skills.Select(record => record.Skill).ToArray(), layout, force: true);

        var rows = skills
            .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
            .Select(record => new SkillActionRow(record.Skill, record.InstalledVersion, record.Skill.Version, SkillAction.Updated))
            .ToArray();

        AnsiConsole.Clear();
        RenderSkillOperationSummary(
            "Update results",
            "updated",
            rows.Length == 0 ? "[grey]0[/]" : $"[yellow]{rows.Length}[/]",
            layout,
            rows,
            statusMessage: rows.Length == 0 ? "All selected installed skills already match the catalog version." : null);
        prompts.Pause("Press any key to return to the interactive shell...");
    }

    private void CopyOrMoveSkills(IReadOnlyList<SkillEntry> skills, SkillInstallLayout sourceLayout)
    {
        var targetLayout = SelectSkillTransferTarget(sourceLayout);
        if (targetLayout is null)
        {
            return;
        }

        if (PathsEqual(sourceLayout.PrimaryRoot, targetLayout.PrimaryRoot))
        {
            RenderInfo("The destination target is the same as the current target.");
            return;
        }

        var removeFromSource = prompts.Confirm("Remove these skills from the current target after copying?", defaultValue: false);
        if (!prompts.Confirm($"Copy/refresh {skills.Count} skill(s) into {targetLayout.PrimaryRoot.FullName}?", defaultValue: true))
        {
            return;
        }

        InstallSkills(skills, targetLayout, force: true);

        if (removeFromSource && prompts.Confirm($"Remove {skills.Count} skill(s) from {sourceLayout.PrimaryRoot.FullName} now?", defaultValue: false))
        {
            RemoveSkills(skills, sourceLayout, pause: true);
        }
    }

    private void InstallAgents(IReadOnlyList<AgentEntry> agents, AgentInstallLayout layout, bool force)
    {
        var installer = new AgentInstaller(agentCatalog);
        var summary = installer.Install(agents, layout, force);

        AnsiConsole.Clear();
        RenderAgentOperationSummary(
            force ? "Repair agents" : "Install agents",
            force ? "rewritten" : "installed",
            force ? $"[yellow]{summary.InstalledCount}[/]" : $"[green]{summary.InstalledCount}[/]",
            layout,
            agents,
            skipped: summary.SkippedExisting);
        prompts.Pause("Press any key to return to the interactive shell...");
    }

    private void RemoveAgents(IReadOnlyList<AgentEntry> agents, AgentInstallLayout layout)
    {
        var installer = new AgentInstaller(agentCatalog);
        var summary = installer.Remove(agents, layout);

        AnsiConsole.Clear();
        RenderAgentOperationSummary(
            "Remove agents",
            "removed",
            summary.RemovedCount == 0 ? "[grey]0[/]" : $"[red]{summary.RemovedCount}[/]",
            layout,
            agents,
            missing: summary.MissingAgents);
        prompts.Pause("Press any key to return to the interactive shell...");
    }

    private void CopyOrMoveAgents(IReadOnlyList<AgentEntry> agents, AgentInstallLayout sourceLayout)
    {
        var targetLayout = SelectAgentTransferTarget(sourceLayout);
        if (targetLayout is null)
        {
            return;
        }

        if (PathsEqual(sourceLayout.PrimaryRoot, targetLayout.PrimaryRoot))
        {
            RenderInfo("The destination target is the same as the current target.");
            return;
        }

        var removeFromSource = prompts.Confirm("Remove these agents from the current target after copying?", defaultValue: false);
        if (!prompts.Confirm($"Copy/refresh {agents.Count} agent(s) into {targetLayout.PrimaryRoot.FullName}?", defaultValue: true))
        {
            return;
        }

        InstallAgents(agents, targetLayout, force: true);

        if (removeFromSource && prompts.Confirm($"Remove {agents.Count} agent(s) from {sourceLayout.PrimaryRoot.FullName} now?", defaultValue: false))
        {
            RemoveAgents(agents, sourceLayout);
        }
    }

    private SkillInstallLayout? SelectSkillTransferTarget(SkillInstallLayout sourceLayout)
    {
        var platform = prompts.Select(
            "Copy/move skills to platform",
            Enum.GetValues<AgentPlatform>().Where(platform => platform != AgentPlatform.Auto).ToArray(),
            platform => platform.ToString());
        var scope = prompts.Select(
            "Copy/move skills to scope",
            Enum.GetValues<InstallScope>(),
            value => value.ToString());

        try
        {
            return SkillInstallTarget.Resolve(
                explicitTargetPath: null,
                platform,
                scope,
                Session.ProjectDirectory);
        }
        catch (Exception exception)
        {
            RenderInfo($"Could not resolve target for {platform}/{scope}: {exception.Message}");
            return null;
        }
    }

    private AgentInstallLayout? SelectAgentTransferTarget(AgentInstallLayout sourceLayout)
    {
        var platform = prompts.Select(
            "Copy/move agents to platform",
            Enum.GetValues<AgentPlatform>().Where(platform => platform != AgentPlatform.Auto).ToArray(),
            platform => platform.ToString());
        var scope = prompts.Select(
            "Copy/move agents to scope",
            Enum.GetValues<InstallScope>(),
            value => value.ToString());

        try
        {
            return AgentInstallTarget.Resolve(
                explicitTargetPath: null,
                platform,
                scope,
                Session.ProjectDirectory);
        }
        catch (Exception exception)
        {
            RenderInfo($"Could not resolve target for {platform}/{scope}: {exception.Message}");
            return null;
        }
    }

    private IReadOnlyList<CollectionCatalogView> BuildCollectionViews(IReadOnlyList<InstalledSkillRecord> installedSkills)
    {
        var installedNames = installedSkills
            .Select(record => record.Skill.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return skillCatalog.Skills
            .GroupBy(skill => skill.Stack, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => CatalogOrganization.GetStackRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var lanes = group
                    .GroupBy(skill => skill.Lane, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(laneGroup => CatalogOrganization.GetLaneRank(laneGroup.Key))
                    .ThenBy(laneGroup => laneGroup.Key, StringComparer.Ordinal)
                    .Select(laneGroup =>
                    {
                        var laneSkills = laneGroup
                            .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                            .ToArray();
                        return new CollectionLaneView(
                            group.Key,
                            laneGroup.Key,
                            laneSkills,
                            laneSkills.Count(skill => installedNames.Contains(skill.Name)),
                            laneSkills.Sum(skill => skill.TokenCount));
                    })
                    .ToArray();

                var stackSkills = group.ToArray();
                return new CollectionCatalogView(
                    group.Key,
                    lanes,
                    stackSkills.Length,
                    stackSkills.Count(skill => installedNames.Contains(skill.Name)),
                    stackSkills.Sum(skill => skill.TokenCount));
            })
            .ToArray();
    }

    private IReadOnlyList<PackageSignalView> BuildPackageSignals()
    {
        var signals = new List<PackageSignalView>();

        foreach (var skill in skillCatalog.Skills.OrderBy(skill => skill.Name, StringComparer.Ordinal))
        {
            foreach (var package in skill.Packages
                         .Where(package => !string.IsNullOrWhiteSpace(package))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                signals.Add(new PackageSignalView(package, "Exact", skill));
            }

            if (!string.IsNullOrWhiteSpace(skill.PackagePrefix))
            {
                signals.Add(new PackageSignalView($"{skill.PackagePrefix}.*", "Prefix", skill));
            }
        }

        return signals
            .OrderBy(entry => entry.Kind, StringComparer.Ordinal)
            .ThenBy(entry => CatalogOrganization.GetStackRank(entry.Skill.Stack))
            .ThenBy(entry => entry.Signal, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Skill.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private void RenderCollectionBrowserPanel(IReadOnlyList<CollectionCatalogView> collectionViews, SkillInstallLayout layout)
    {
        var splitPanes = GetConsoleWidth() >= 155;

        var overview = new Spectre.Console.Grid();
        overview.AddColumn(new Spectre.Console.GridColumn { NoWrap = true });
        overview.AddColumn();
        overview.AddRow(new Spectre.Console.Markup("[dim]target[/]"), new Spectre.Console.Markup($"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"));
        overview.AddRow(new Spectre.Console.Markup("[dim]collections[/]"), new Spectre.Console.Markup(collectionViews.Count.ToString()));
        overview.AddRow(new Spectre.Console.Markup("[dim]skills[/]"), new Spectre.Console.Markup(skillCatalog.Skills.Count.ToString()));
        overview.AddRow(new Spectre.Console.Markup("[dim]tokens[/]"), new Spectre.Console.Markup(FormatTokenCount(skillCatalog.Skills.Sum(skill => skill.TokenCount))));

        var flow = BuildRichStack(
            new Spectre.Console.Markup("[deepskyblue1]1[/] [bold]Choose a collection[/] [dim]to narrow the catalog surface[/]"),
            new Spectre.Console.Markup("[deepskyblue1]2[/] [bold]Inspect a lane[/] [dim]to see concrete skills and size[/]"),
            new Spectre.Console.Markup("[deepskyblue1]3[/] [bold]Install from a lane[/] [dim]without broad mixed bundles[/]"));

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle("[bold]Collection -> Lane -> Skill[/]");
        table.AddColumn("Collection");
        table.AddColumn("Lanes");
        table.AddColumn("Installed");
        table.AddColumn("Tokens");
        table.AddColumn("Sample lanes");
        table.AddColumn("Included skills");

        foreach (var collection in collectionViews)
        {
            var sampleLanes = collection.Lanes.Take(3).Select(lane => lane.Lane).ToArray();
            var includedSkills = collection.Lanes
                .SelectMany(lane => lane.Skills)
                .Select(skill => skill.Name)
                .ToArray();
            table.AddRow(
                Escape(collection.Collection),
                collection.Lanes.Count.ToString(),
                $"{collection.InstalledCount}/{collection.SkillCount}",
                FormatTokenCount(collection.TokenCount),
                Escape(string.Join(", ", sampleLanes)) + (collection.Lanes.Count > sampleLanes.Length ? $" [grey](+{collection.Lanes.Count - sampleLanes.Length})[/]" : string.Empty),
                Escape(SummarizeAliases(includedSkills, take: 5)));
        }

        var spotlight = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.None);
        spotlight.AddColumn("Focus");
        spotlight.AddColumn("Value");

        foreach (var collection in collectionViews.OrderByDescending(entry => entry.TokenCount).ThenBy(entry => entry.Collection, StringComparer.Ordinal).Take(5))
        {
            spotlight.AddRow(
                Escape(collection.Collection),
                $"{FormatTokenCount(collection.TokenCount)} [dim]({collection.InstalledCount}/{collection.SkillCount})[/]");
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("navigation", flow),
                    BuildRichShellPanel("collection browser", overview),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("navigation", flow),
                    BuildRichShellPanel("collection browser", overview)));
        AnsiConsole.WriteLine();

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("collection matrix", table),
                    BuildRichShellPanel("heaviest collections", spotlight),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("collection matrix", table),
                    BuildRichShellPanel("heaviest collections", spotlight)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Choose a collection first. Install surfaces only appear after the lane boundary is explicit.[/]")));
    }

    private void RenderCollectionDetailPanel(CollectionCatalogView collectionView, SkillInstallLayout layout)
    {
        var splitPanes = GetConsoleWidth() >= 155;

        var summary = BuildRichPropertyGrid(
            ("collection", Escape(collectionView.Collection)),
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("lanes", collectionView.Lanes.Count.ToString()),
            ("skills", $"{collectionView.InstalledCount}/{collectionView.SkillCount}"),
            ("tokens", FormatTokenCount(collectionView.TokenCount)));

        var flow = BuildRichStack(
            new Spectre.Console.Markup("[deepskyblue1]Inspect a lane[/] [dim]to drill into concrete skill cards[/]"),
            new Spectre.Console.Markup("[deepskyblue1]Install from a lane[/] [dim]to keep the write set explicit[/]"),
            new Spectre.Console.Markup("[yellow]Update all outdated[/] [dim]to refresh this collection in one action[/]"),
            new Spectre.Console.Markup("[deepskyblue1]Review outdated[/] [dim]only when the update set needs pruning[/]"));

        var laneTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        laneTable.Title = new Spectre.Console.TableTitle("[bold]Lanes[/]");
        laneTable.AddColumn("Lane");
        laneTable.AddColumn("Skills");
        laneTable.AddColumn("Installed");
        laneTable.AddColumn("Tokens");
        laneTable.AddColumn("Examples");

        foreach (var lane in collectionView.Lanes)
        {
            var examples = lane.Skills.Take(3).Select(skill => ToAlias(skill.Name)).ToArray();
            laneTable.AddRow(
                Escape(lane.Lane),
                lane.Skills.Count.ToString(),
                lane.InstalledCount.ToString(),
                FormatTokenCount(lane.TokenCount),
                Escape(string.Join(", ", examples)) + (lane.Skills.Count > examples.Length ? $" [grey](+{lane.Skills.Count - examples.Length})[/]" : string.Empty));
        }

        var skillTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        skillTable.Title = new Spectre.Console.TableTitle("[bold]Heaviest skills in collection[/]");
        skillTable.AddColumn("Alias");
        skillTable.AddColumn("Lane");
        skillTable.AddColumn("Tokens");
        skillTable.AddColumn("Summary");

        foreach (var skill in collectionView.Lanes
                     .SelectMany(lane => lane.Skills)
                     .OrderByDescending(skill => skill.TokenCount)
                     .ThenBy(skill => skill.Name, StringComparer.Ordinal)
                     .Take(8))
        {
            skillTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape(skill.Lane),
                FormatTokenCount(skill.TokenCount),
                Escape(CompactDescription(skill.Description)));
        }

        var relatedBundles = GetPrimaryBundles()
            .Where(package => string.Equals(package.Stack, collectionView.Collection, StringComparison.OrdinalIgnoreCase)
                              || package.Skills.Any(skillName =>
                                  collectionView.Lanes.SelectMany(lane => lane.Skills)
                                      .Any(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase))))
            .DistinctBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(CatalogOrganization.FormatBundleSortKey, StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        var relatedBundleTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.None);
        relatedBundleTable.AddColumn("Bundle");
        relatedBundleTable.AddColumn("Area");
        relatedBundleTable.AddColumn("Tokens");

        foreach (var bundle in relatedBundles)
        {
            var tokenCount = bundle.Skills
                .Select(skillName => skillCatalog.Skills.FirstOrDefault(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase)))
                .Where(skill => skill is not null)
                .Sum(skill => skill!.TokenCount);
            relatedBundleTable.AddRow(
                Escape(bundle.Name),
                Escape(CatalogOrganization.ResolveBundleAreaLabel(bundle)),
                FormatTokenCount(tokenCount));
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("collection modes", flow),
                    BuildRichShellPanel(collectionView.Collection, summary),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("collection modes", flow),
                    BuildRichShellPanel(collectionView.Collection, summary)));
        AnsiConsole.WriteLine();

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("lane map", laneTable),
                    BuildRichShellPanel("heavy skills", skillTable),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("lane map", laneTable),
                    BuildRichShellPanel("heavy skills", skillTable)));
        AnsiConsole.WriteLine();

        if (relatedBundles.Length > 0)
        {
            SpectreConsole.Write(BuildRichShellPanel("related bundles", relatedBundleTable));
            AnsiConsole.WriteLine();
        }

        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]This collection is already narrowed. The next selection happens at the lane level, never against the full catalog.[/]")));
    }

    private void RenderCatalogAnalysisPanel(IReadOnlyList<CollectionCatalogView> collectionViews, SkillInstallLayout layout, IReadOnlyList<PackageSignalView> packageSignals)
    {
        var splitPanes = GetConsoleWidth() >= 155;

        var summary = new Spectre.Console.Grid();
        summary.AddColumn(new Spectre.Console.GridColumn { NoWrap = true });
        summary.AddColumn();
        summary.AddRow(new Spectre.Console.Markup("[dim]target[/]"), new Spectre.Console.Markup($"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"));
        summary.AddRow(new Spectre.Console.Markup("[dim]collections[/]"), new Spectre.Console.Markup(collectionViews.Count.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]skills[/]"), new Spectre.Console.Markup(skillCatalog.Skills.Count.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]package signals[/]"), new Spectre.Console.Markup(packageSignals.Count.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]tokens[/]"), new Spectre.Console.Markup(FormatTokenCount(skillCatalog.Skills.Sum(skill => skill.TokenCount))));

        var heavyTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        heavyTable.Title = new Spectre.Console.TableTitle("[bold]Heaviest skills[/]");
        heavyTable.AddColumn("Skill");
        heavyTable.AddColumn("Area");
        heavyTable.AddColumn("Tokens");

        foreach (var skill in skillCatalog.Skills
                     .OrderByDescending(skill => skill.TokenCount)
                     .ThenBy(skill => skill.Name, StringComparer.Ordinal)
                     .Take(10))
        {
            heavyTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape($"{skill.Stack} / {skill.Lane}"),
                FormatTokenCount(skill.TokenCount));
        }

        var packageTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        packageTable.Title = new Spectre.Console.TableTitle("[bold]Package entry points[/]");
        packageTable.AddColumn("Signal");
        packageTable.AddColumn("Kind");
        packageTable.AddColumn("Skill");
        packageTable.AddColumn("Area");

        foreach (var entry in packageSignals.Take(12))
        {
            packageTable.AddRow(
                Escape(entry.Signal),
                Escape(entry.Kind),
                Escape(ToAlias(entry.Skill.Name)),
                Escape($"{entry.Skill.Stack} / {entry.Skill.Lane}"));
        }

        var collectionTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        collectionTable.Title = new Spectre.Console.TableTitle("[bold]Collection composition[/]");
        collectionTable.AddColumn("Collection");
        collectionTable.AddColumn("Lanes");
        collectionTable.AddColumn("Skills");
        collectionTable.AddColumn("Tokens");

        foreach (var collection in collectionViews)
        {
            collectionTable.AddRow(
                Escape(collection.Collection),
                collection.Lanes.Count.ToString(),
                collection.SkillCount.ToString(),
                FormatTokenCount(collection.TokenCount));
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("catalog analysis", summary),
                    BuildRichShellPanel("package signals", packageTable),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("catalog analysis", summary),
                    BuildRichShellPanel("package signals", packageTable)));
        AnsiConsole.WriteLine();

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("collection matrix", collectionTable),
                    BuildRichShellPanel("token hotspots", heavyTable),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("collection matrix", collectionTable),
                    BuildRichShellPanel("token hotspots", heavyTable)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Use the tree view for hierarchy-first browsing, then inspect heavy skills or package entry points before installing.[/]")));
    }

    private void RenderCatalogTreePanel(IReadOnlyList<CollectionCatalogView> collectionViews)
    {
        var tree = new Spectre.Console.Tree("[bold]catalog[/]");

        foreach (var collection in collectionViews)
        {
            var collectionNode = tree.AddNode($"{Escape(collection.Collection)} [dim]({collection.SkillCount} skills, {FormatTokenCount(collection.TokenCount)} tokens)[/]");
            foreach (var lane in collection.Lanes)
            {
                var laneNode = collectionNode.AddNode($"{Escape(lane.Lane)} [dim]({lane.Skills.Count} skills, {FormatTokenCount(lane.TokenCount)} tokens)[/]");
                foreach (var skill in lane.Skills.OrderByDescending(skill => skill.TokenCount).ThenBy(skill => skill.Name, StringComparer.Ordinal))
                {
                    laneNode.AddNode($"{Escape(ToAlias(skill.Name))} [dim]({FormatTokenCount(skill.TokenCount)})[/]");
                }
            }
        }

        SpectreConsole.Write(BuildRichShellPanel("collection tree", tree));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]This is the full Collection -> Lane -> Skill hierarchy for the current catalog payload.[/]")));
    }

    private void RenderPackageSignalPanel(IReadOnlyList<PackageSignalView> packageSignals)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var summary = BuildRichPropertyGrid(
            ("signals", packageSignals.Count.ToString()),
            ("exact", packageSignals.Count(entry => string.Equals(entry.Kind, "Exact", StringComparison.Ordinal)).ToString()),
            ("prefix", packageSignals.Count(entry => string.Equals(entry.Kind, "Prefix", StringComparison.Ordinal)).ToString()),
            ("skills", skillCatalog.Skills.Count.ToString()));

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle("[bold]NuGet entry points[/]");
        table.AddColumn("Signal");
        table.AddColumn("Kind");
        table.AddColumn("Skill");
        table.AddColumn("Area");
        table.AddColumn("Tokens");

        foreach (var entry in packageSignals)
        {
            table.AddRow(
                Escape(entry.Signal),
                Escape(entry.Kind),
                Escape(ToAlias(entry.Skill.Name)),
                Escape($"{entry.Skill.Stack} / {entry.Skill.Lane}"),
                FormatTokenCount(entry.Skill.TokenCount));
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("package signals", summary),
                    BuildRichShellPanel("signal map", table),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("package signals", summary),
                    BuildRichShellPanel("signal map", table)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Package signals connect exact NuGet ids and prefixes to the skill that should be installed when that package appears in a project.[/]")));
    }

    private void RenderInstallPreviewPanel(string title, IReadOnlyList<SkillEntry> skills, SkillInstallLayout layout, bool force, IReadOnlyList<SkillPackageEntry>? bundles = null)
    {
        var selectedSkills = skills
            .OrderBy(skill => CatalogOrganization.GetStackRank(skill.Stack))
            .ThenBy(skill => CatalogOrganization.GetLaneRank(skill.Lane))
            .ThenBy(skill => skill.Name, StringComparer.Ordinal)
            .ToArray();
        var totalTokens = selectedSkills.Sum(skill => skill.TokenCount);
        var stackCount = selectedSkills.Select(skill => skill.Stack).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var splitPanes = GetConsoleWidth() >= 155;

        var summary = new Spectre.Console.Grid();
        summary.AddColumn(new Spectre.Console.GridColumn { NoWrap = true });
        summary.AddColumn();
        summary.AddRow(new Spectre.Console.Markup("[dim]target[/]"), new Spectre.Console.Markup($"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"));
        summary.AddRow(new Spectre.Console.Markup("[dim]mode[/]"), new Spectre.Console.Markup(force ? "Repair / overwrite" : "Install"));
        summary.AddRow(new Spectre.Console.Markup("[dim]skills[/]"), new Spectre.Console.Markup(selectedSkills.Length.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]collections[/]"), new Spectre.Console.Markup(stackCount.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]tokens[/]"), new Spectre.Console.Markup(FormatTokenCount(totalTokens)));
        if (bundles is not null)
        {
            summary.AddRow(new Spectre.Console.Markup("[dim]bundles[/]"), new Spectre.Console.Markup(bundles.Count.ToString()));
        }

        var stackTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        stackTable.Title = new Spectre.Console.TableTitle("[bold]Write set by collection[/]");
        stackTable.AddColumn("Collection");
        stackTable.AddColumn("Lanes");
        stackTable.AddColumn("Skills");
        stackTable.AddColumn("Tokens");

        foreach (var group in selectedSkills
                     .GroupBy(skill => skill.Stack, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => CatalogOrganization.GetStackRank(group.Key))
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            var lanes = group
                .Select(skill => skill.Lane)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(CatalogOrganization.GetLaneRank)
                .ThenBy(lane => lane, StringComparer.Ordinal)
                .ToArray();
            stackTable.AddRow(
                Escape(group.Key),
                Escape(string.Join(", ", lanes.Take(3))) + (lanes.Length > 3 ? $" [grey](+{lanes.Length - 3})[/]" : string.Empty),
                group.Count().ToString(),
                FormatTokenCount(group.Sum(skill => skill.TokenCount)));
        }

        var skillTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        skillTable.Title = new Spectre.Console.TableTitle("[bold]Selected skills[/]");
        skillTable.AddColumn("Skill");
        skillTable.AddColumn("Area");
        skillTable.AddColumn("Tokens");

        foreach (var skill in selectedSkills.Take(12))
        {
            skillTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape($"{skill.Stack} / {skill.Lane}"),
                FormatTokenCount(skill.TokenCount));
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel(title, summary),
                    BuildRichShellPanel("selected skills", skillTable),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel(title, summary),
                    BuildRichShellPanel("selected skills", skillTable)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("install overview", stackTable));
        AnsiConsole.WriteLine();

        if (bundles is not null && bundles.Count > 0)
        {
            var bundleTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
            bundleTable.Title = new Spectre.Console.TableTitle("[bold]Selected bundles[/]");
            bundleTable.AddColumn("Bundle");
            bundleTable.AddColumn("Area");
            bundleTable.AddColumn("Skills");

            foreach (var bundle in bundles.OrderBy(bundle => bundle.Name, StringComparer.Ordinal))
            {
                bundleTable.AddRow(
                    Escape(bundle.Name),
                    Escape(CatalogOrganization.ResolveBundleAreaLabel(bundle)),
                    bundle.Skills.Count.ToString());
            }

            SpectreConsole.Write(BuildRichShellPanel("selected bundles", bundleTable));
            AnsiConsole.WriteLine();
        }

        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]This preview is the exact write set that will be installed into the selected target if you confirm.[/]")));
    }

    private void RenderInstalledSkillsPanel(
        SkillInstallLayout layout,
        IReadOnlyList<InstalledSkillRecord> installedSkills,
        IReadOnlyList<ScopeInventoryRow> scopeInventory)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var outdatedCount = installedSkills.Count(record => !record.IsCurrent);
        var installedByCollection = installedSkills
            .GroupBy(record => record.Skill.Stack, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        var summary = BuildRichPropertyGrid(
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("installed", installedSkills.Count.ToString()),
            ("outdated", outdatedCount == 0 ? "[green]0[/]" : $"[yellow]{outdatedCount}[/]"),
            ("collections", installedSkills.Select(record => record.Skill.Stack).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()),
            ("tokens", FormatTokenCount(installedSkills.Sum(record => record.Skill.TokenCount))));

        var coverage = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        coverage.Title = new Spectre.Console.TableTitle("[bold]Installed coverage[/]");
        coverage.AddColumn("Collection");
        coverage.AddColumn("Skills");
        coverage.AddColumn("Tokens");

        foreach (var group in installedByCollection)
        {
            coverage.AddRow(
                Escape(group.Key),
                group.Count().ToString(),
                FormatTokenCount(group.Sum(record => record.Skill.TokenCount)));
        }

        var installedTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        installedTable.Title = new Spectre.Console.TableTitle("[bold]Installed skills[/]");
        installedTable.AddColumn("Skill");
        installedTable.AddColumn("Area");
        installedTable.AddColumn("Version");
        installedTable.AddColumn("Status");
        installedTable.AddColumn("Tokens");

        foreach (var record in installedSkills.OrderBy(item => item.Skill.Stack, StringComparer.Ordinal).ThenBy(item => item.Skill.Lane, StringComparer.Ordinal).ThenBy(item => item.Skill.Name, StringComparer.Ordinal))
        {
            installedTable.AddRow(
                Escape(ToAlias(record.Skill.Name)),
                Escape($"{record.Skill.Stack} / {record.Skill.Lane}"),
                Escape(record.InstalledVersion),
                record.IsCurrent ? "[green]current[/]" : $"[yellow]{Escape(record.InstalledVersion)} -> {Escape(record.Skill.Version)}[/]",
                FormatTokenCount(record.Skill.TokenCount));
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("installed summary", summary),
                    BuildRichShellPanel("installed coverage", coverage),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("installed summary", summary),
                    BuildRichShellPanel("installed coverage", coverage)));
        AnsiConsole.WriteLine();

        if (scopeInventory.Count > 1)
        {
            var scopeTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
            scopeTable.Title = new Spectre.Console.TableTitle("[bold]Scope comparison[/]");
            scopeTable.AddColumn("Scope");
            scopeTable.AddColumn("Target");
            scopeTable.AddColumn("Installed");

            foreach (var row in scopeInventory)
            {
                scopeTable.AddRow(
                    Escape(row.Scope.ToString()),
                    $"[dim]{Escape(CompactPath(row.TargetRoot.FullName))}[/]",
                    row.InstalledSkills.Count.ToString());
            }

            SpectreConsole.Write(BuildRichShellPanel("scope inventory", scopeTable));
            AnsiConsole.WriteLine();
        }

        if (installedSkills.Count == 0)
        {
            SpectreConsole.Write(BuildRichShellPanel(
                "status rail",
                new Spectre.Console.Markup("[dim]No catalog skills are installed in this target yet. Use Collections or Bundles to create the first write set.[/]")));
            return;
        }

        SpectreConsole.Write(BuildRichShellPanel("installed inventory", installedTable));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Installed state is explicit here: update all outdated skills directly, review the checked set, remove selected skills, or clear this exact target.[/]")));
    }

    private void RenderBundleBrowserPanel(IReadOnlyList<SkillPackageEntry> visibleBundles)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var skillIndex = skillCatalog.Skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
        var bundleRows = visibleBundles
            .Select(bundle => new
            {
                Bundle = bundle,
                TokenCount = bundle.Skills.Sum(skillName => skillIndex.TryGetValue(skillName, out var skill) ? skill.TokenCount : 0),
            })
            .OrderBy(bundle => CatalogOrganization.FormatBundleSortKey(bundle.Bundle))
            .ToArray();

        var summary = BuildRichPropertyGrid(
            ("bundles", visibleBundles.Count.ToString()),
            ("collections", visibleBundles.Select(bundle => bundle.Stack).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()),
            ("skills", visibleBundles.SelectMany(bundle => bundle.Skills).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()),
            ("tokens", FormatTokenCount(bundleRows.Sum(row => row.TokenCount))));

        var highlightTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.None);
        highlightTable.AddColumn("Bundle");
        highlightTable.AddColumn("Value");

        foreach (var row in bundleRows.OrderByDescending(item => item.TokenCount).ThenBy(item => item.Bundle.Name, StringComparer.Ordinal).Take(5))
        {
            highlightTable.AddRow(
                Escape(row.Bundle.Name),
                $"{FormatTokenCount(row.TokenCount)} [dim]({row.Bundle.Skills.Count} skills)[/]");
        }

        var bundleTable = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        bundleTable.Title = new Spectre.Console.TableTitle("[bold]Focused bundles[/]");
        bundleTable.AddColumn("Bundle");
        bundleTable.AddColumn("Area");
        bundleTable.AddColumn("Skills");
        bundleTable.AddColumn("Tokens");
        bundleTable.AddColumn("Sample");

        foreach (var row in bundleRows)
        {
            bundleTable.AddRow(
                Escape(row.Bundle.Name),
                Escape(CatalogOrganization.ResolveBundleAreaLabel(row.Bundle)),
                row.Bundle.Skills.Count.ToString(),
                FormatTokenCount(row.TokenCount),
                Escape(SummarizeAliases(row.Bundle.Skills)));
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("bundle summary", summary),
                    BuildRichShellPanel("bundle hotspots", highlightTable),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("bundle summary", summary),
                    BuildRichShellPanel("bundle hotspots", highlightTable)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("bundle inventory", bundleTable));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Bundles remain focused and installable. Broad category bundles are intentionally absent from this surface.[/]")));
    }

    private void RenderProjectSyncPanel(
        ProjectSkillAutoSyncPlan plan,
        SkillInstallLayout layout,
        IReadOnlyDictionary<string, InstalledSkillRecord> installedBefore,
        IReadOnlyList<SkillEntry> newSkills)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var summary = BuildRichPropertyGrid(
            ("project", $"[dim]{Escape(CompactPath(plan.ScanResult.ProjectRoot.FullName))}[/]"),
            ("projects", plan.ScanResult.ProjectFiles.Count.ToString()),
            ("matched", plan.DesiredSkills.Count.ToString()),
            ("new", newSkills.Count == 0 ? "[grey]0[/]" : $"[green]{newSkills.Count}[/]"),
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("frameworks", Escape(string.Join(", ", plan.ScanResult.TargetFrameworks))));

        var flow = BuildRichStack(
            new Spectre.Console.Markup("[deepskyblue1]Signals[/] [dim]come from .csproj packages, SDKs, and project properties[/]"),
            new Spectre.Console.Markup("[deepskyblue1]New skills[/] [dim]will be previewed before any write happens[/]"),
            new Spectre.Console.Markup("[deepskyblue1]State[/] [dim]is saved only after a confirmed project-driven install[/]"));

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle("[bold]Project matches[/]");
        table.AddColumn("Skill");
        table.AddColumn("Confidence");
        table.AddColumn("Signals");
        table.AddColumn("Status");

        foreach (var recommendation in plan.ScanResult.Recommendations
                     .Where(recommendation => recommendation.IsAutoInstallCandidate)
                     .OrderByDescending(recommendation => recommendation.Confidence)
                     .ThenBy(recommendation => recommendation.Skill.Name, StringComparer.Ordinal))
        {
            var isInstalled = installedBefore.ContainsKey(recommendation.Skill.Name);
            table.AddRow(
                Escape(ToAlias(recommendation.Skill.Name)),
                FormatConfidenceMarkup(recommendation.Confidence),
                Escape(string.Join(", ", recommendation.Signals)),
                isInstalled ? "[grey]already installed[/]" : "[green]new[/]");
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("project sync", summary),
                    BuildRichShellPanel("signal flow", flow),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("project sync", summary),
                    BuildRichShellPanel("signal flow", flow)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("matched skills", table));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup(newSkills.Count == 0
                ? "[dim]All matched skills are already installed in this target.[/]"
                : "[dim]Only the new slice will be proposed for install. Existing skills stay untouched unless you choose update or repair separately.[/]")));
    }

    private void RenderAgentBrowserPanel(AgentInstallLayout? layout, string? layoutError, IReadOnlyList<InstalledAgentRecord> installedAgents)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var installedSet = installedAgents.Select(record => record.Agent.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var summary = BuildRichPropertyGrid(
            ("platform", Escape(Session.Agent.ToString())),
            ("scope", Escape(Session.Scope.ToString())),
            ("target", layout is null ? $"[grey]{Escape(layoutError ?? "Unavailable")}[/]" : $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("agents", agentCatalog.Agents.Count.ToString()),
            ("installed", installedAgents.Count.ToString()));

        var flow = BuildRichStack(
            new Spectre.Console.Markup("[deepskyblue1]Inspect[/] [dim]agent contracts before writing any native files[/]"),
            new Spectre.Console.Markup("[deepskyblue1]Install[/] [dim]only when a native agent target exists[/]"),
            new Spectre.Console.Markup("[deepskyblue1]Repair / move / remove[/] [dim]remain target-specific lifecycle actions[/]"));

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle("[bold]Bundled agents[/]");
        table.AddColumn("Agent");
        table.AddColumn("Model");
        table.AddColumn("Skills");
        table.AddColumn("Status");

        foreach (var agent in agentCatalog.Agents.OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            table.AddRow(
                Escape(ToAlias(agent.Name)),
                Escape(agent.Model),
                Escape(SummarizeAliases(agent.Skills)),
                installedSet.Contains(agent.Name) ? "[green]installed[/]" : layout is null ? "[grey]target unavailable[/]" : "[grey]not installed[/]");
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("agent summary", summary),
                    BuildRichShellPanel("agent flow", flow),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("agent summary", summary),
                    BuildRichShellPanel("agent flow", flow)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("agent inventory", table));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup(layout is null
                ? "[dim]No native agent directory is available for the current session. Choose a concrete platform or create the native target first.[/]"
                : "[dim]Agent install surfaces are native-target only. No shared fallback directory is used for agents.[/]")));
    }

    private void RenderHelpPanel()
    {
        var sections = new (string Area, string Command, string Use)[]
        {
            ("Launch", ToolIdentity.DisplayCommand, "Open the interactive control center"),
            ("Help", $"{ToolIdentity.DisplayCommand} help", "Show direct command reference"),
            ("Version", $"{ToolIdentity.DisplayCommand} version", "Show version and update status"),
            ("Collections", $"{ToolIdentity.SkillsDisplayCommand} list --available-only", "Expand Collection -> Lane -> Skill inventory"),
            ("Bundles", $"{ToolIdentity.SkillsDisplayCommand} bundle list", "List focused install bundles"),
            ("Tokens", $"{ToolIdentity.SkillsDisplayCommand} catalog tokens --catalog-root .", "Export per-skill token counts"),
            ("Project", $"{ToolIdentity.SkillsDisplayCommand} install --auto", "Install from .csproj signals"),
            ("Installed", $"{ToolIdentity.SkillsDisplayCommand} list --installed-only", "Inspect the current target before review, remove, or clear"),
            ("Install", $"{ToolIdentity.SkillsDisplayCommand} install aspire", "Install by alias"),
            ("Install", $"{ToolIdentity.SkillsDisplayCommand} install bundle dotnet-quality", "Install a focused bundle"),
            ("Remove", $"{ToolIdentity.SkillsDisplayCommand} remove aspire", "Remove one installed skill"),
            ("Remove", $"{ToolIdentity.SkillsDisplayCommand} remove bundle dotnet-quality", "Remove one focused bundle"),
            ("Remove", $"{ToolIdentity.SkillsDisplayCommand} remove collection distributed", "Remove one collection surface"),
            ("Remove", $"{ToolIdentity.SkillsDisplayCommand} remove --all", "Clear the selected target"),
            ("Update", $"{ToolIdentity.SkillsDisplayCommand} update", "Refresh installed skills"),
            ("Where", $"{ToolIdentity.SkillsDisplayCommand} where", "Print resolved install paths"),
            ("Agents", $"{ToolIdentity.AgentDisplayCommand} list", "List bundled orchestration agents"),
            ("Agents", $"{ToolIdentity.AgentDisplayCommand} install router", "Install agents by name"),
        };

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle("[bold]Command reference[/]");
        table.AddColumn("Area");
        table.AddColumn("Command");
        table.AddColumn("Use");

        foreach (var section in sections)
        {
            table.AddRow(
                Escape(section.Area),
                $"[grey]{Escape(section.Command)}[/]",
                Escape(section.Use));
        }

        var notes = BuildRichStack(
            new Spectre.Console.Markup("[dim]Interactive shell language is[/] [green]Collection -> Lane -> Skill[/] [dim]plus focused bundles.[/]"),
            new Spectre.Console.Markup("[dim]Use[/] [green]--bundled[/] [dim]to skip network catalog fetches explicitly.[/]"),
            new Spectre.Console.Markup("[dim]Auto-detect probes native skill roots first. Shared fallback is skills-only and appears only when no native root exists.[/]"),
            new Spectre.Console.Markup($"[dim]Set[/] [green]{Escape(ToolIdentity.SkipUpdateEnvironmentVariable)}=1[/] [dim]to suppress update notices.[/]"));

        SpectreConsole.Write(BuildRichStack(
            BuildRichShellPanel("command reference", table),
            BuildRichShellPanel("notes", notes)));
    }

    private void RenderSkillOperationSummary(
        string title,
        string resultLabel,
        string resultValue,
        SkillInstallLayout layout,
        IReadOnlyList<SkillActionRow> rows,
        string? statusMessage = null,
        IReadOnlyList<string>? skipped = null,
        int generatedAdapters = 0)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var summary = BuildRichPropertyGrid(
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [dim]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            (resultLabel, resultValue),
            ("skipped", skipped?.Count.ToString() ?? "0"),
            ("adapters", generatedAdapters == 0 ? "[grey]0[/]" : $"[yellow]{generatedAdapters}[/]"));

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle($"[bold]{Escape(title)}[/]");
        table.AddColumn("Skill");
        table.AddColumn("Area");
        table.AddColumn("From");
        table.AddColumn("To");
        table.AddColumn("Action");

        foreach (var row in rows.OrderBy(item => item.Skill.Name, StringComparer.Ordinal))
        {
            table.AddRow(
                Escape(ToAlias(row.Skill.Name)),
                Escape($"{row.Skill.Stack} / {row.Skill.Lane}"),
                Escape(row.FromVersion),
                Escape(row.ToVersion),
                FormatSkillActionMarkup(row.Action));
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("operation summary", summary),
                    BuildRichShellPanel("reload hint", new Spectre.Console.Markup(Escape(layout.ReloadHint))),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("operation summary", summary),
                    BuildRichShellPanel("reload hint", new Spectre.Console.Markup(Escape(layout.ReloadHint)))));
        AnsiConsole.WriteLine();

        if (rows.Count > 0)
        {
            SpectreConsole.Write(BuildRichShellPanel("result matrix", table));
            AnsiConsole.WriteLine();
        }

        if (skipped is not null && skipped.Count > 0)
        {
            SpectreConsole.Write(BuildRichShellPanel("skipped", new Spectre.Console.Markup(Escape(string.Join(", ", skipped)))));
            AnsiConsole.WriteLine();
        }

        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup(statusMessage is null
                ? "[dim]Operation completed for the selected target.[/]"
                : Escape(statusMessage))));
    }

    private void RenderAgentOperationSummary(
        string title,
        string resultLabel,
        string resultValue,
        AgentInstallLayout layout,
        IReadOnlyList<AgentEntry> agents,
        IReadOnlyList<string>? skipped = null,
        IReadOnlyList<string>? missing = null)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var summary = BuildRichPropertyGrid(
            ("platform", Escape(layout.Agent.ToString())),
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("mode", Escape(layout.Mode.ToString())),
            (resultLabel, resultValue),
            ("skipped", skipped?.Count.ToString() ?? "0"),
            ("missing", missing?.Count.ToString() ?? "0"));

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle($"[bold]{Escape(title)}[/]");
        table.AddColumn("Agent");
        table.AddColumn("Skills");
        table.AddColumn("Status");

        foreach (var agent in agents.OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            var skippedAgent = skipped?.Contains(agent.Name, StringComparer.OrdinalIgnoreCase) == true;
            var missingAgent = missing?.Contains(agent.Name, StringComparer.OrdinalIgnoreCase) == true;
            var status = missingAgent
                ? "[grey]missing[/]"
                : skippedAgent
                    ? "[grey]skipped[/]"
                    : string.Equals(resultLabel, "removed", StringComparison.OrdinalIgnoreCase)
                        ? "[red]removed[/]"
                        : "[green]installed[/]";

            table.AddRow(
                Escape(ToAlias(agent.Name)),
                Escape(SummarizeAliases(agent.Skills)),
                status);
        }

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("agent summary", summary),
                    BuildRichShellPanel("reload hint", new Spectre.Console.Markup(Escape(layout.ReloadHint))),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("agent summary", summary),
                    BuildRichShellPanel("reload hint", new Spectre.Console.Markup(Escape(layout.ReloadHint)))));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("agent results", table));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Agent writes stay bound to the selected native target and file format.[/]")));
    }

    private void RenderSkillDetailPanel(SkillEntry skill, InstalledSkillRecord? installed, SkillInstallLayout layout)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var statusMarkup = installed is null
            ? "[grey]not installed[/]"
            : installed.IsCurrent
                ? $"{Escape(installed.InstalledVersion)} [green](current)[/]"
                : $"{Escape(installed.InstalledVersion)} [yellow](update available)[/]";

        var summary = BuildRichPropertyGrid(
            ("alias", Escape(ToAlias(skill.Name))),
            ("skill", $"[dim]{Escape(skill.Name)}[/]"),
            ("area", $"{Escape(skill.Stack)} [dim]/[/] {Escape(skill.Lane)}"),
            ("category", Escape(skill.Category)),
            ("version", Escape(skill.Version)),
            ("tokens", $"{FormatTokenCount(skill.TokenCount)} [dim]({Escape(SkillTokenCounter.ModelName)})[/]"),
            ("status", statusMarkup),
            ("compat", Escape(skill.Compatibility)),
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("install", $"[green]{Escape($"dotnet skills install {ToAlias(skill.Name)}")}[/]"));

        var surface = new Spectre.Console.Table().Border(Spectre.Console.TableBorder.None).Expand();
        surface.AddColumn("Surface");
        surface.AddColumn("Value");
        surface.AddRow("NuGet packages", skill.Packages.Count == 0 ? "[dim]-[/]" : Escape(string.Join(", ", skill.Packages.Take(4))));
        surface.AddRow("Package prefix", string.IsNullOrWhiteSpace(skill.PackagePrefix) ? "[dim]-[/]" : Escape($"{skill.PackagePrefix}.*"));
        surface.AddRow("Docs", string.IsNullOrWhiteSpace(skill.Links.Docs) ? "[dim]not declared[/]" : "[green]available[/]");
        surface.AddRow("Repository", string.IsNullOrWhiteSpace(skill.Links.Repository) ? "[dim]not declared[/]" : "[green]available[/]");
        surface.AddRow("NuGet link", string.IsNullOrWhiteSpace(skill.Links.NuGet) ? "[dim]not declared[/]" : "[green]available[/]");

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel(ToAlias(skill.Name), summary),
                    BuildRichShellPanel("skill surface", surface),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel(ToAlias(skill.Name), summary),
                    BuildRichShellPanel("skill surface", surface)));
        AnsiConsole.WriteLine();

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel("summary", new Spectre.Console.Markup(Escape(skill.Description))),
                    BuildRichShellPanel("preview", new Spectre.Console.Markup(Escape(LoadSkillPreview(skill)))),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel("summary", new Spectre.Console.Markup(Escape(skill.Description))),
                    BuildRichShellPanel("preview", new Spectre.Console.Markup(Escape(LoadSkillPreview(skill))))));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Preview comes from the current[/] [green]SKILL.md[/] [dim]payload, so token size and visible content stay aligned.[/]")));
    }

    private void RenderPackageDetailPanel(SkillPackageEntry package)
    {
        var skillIndex = skillCatalog.Skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
        var tokenCount = package.Skills.Sum(skillName => skillIndex.TryGetValue(skillName, out var skill) ? skill.TokenCount : 0);
        var splitPanes = GetConsoleWidth() >= 155;
        var grid = BuildRichPropertyGrid(
            ("bundle", Escape(package.Name)),
            ("area", Escape(CatalogOrganization.ResolveBundleAreaLabel(package))),
            ("type", Escape(package.Kind)),
            ("skills", package.Skills.Count.ToString()),
            ("tokens", FormatTokenCount(tokenCount)),
            ("install", $"[green]{Escape($"dotnet skills install bundle {package.Name}")}[/]"));

        var coverage = new Spectre.Console.Table().Border(Spectre.Console.TableBorder.None).Expand();
        coverage.AddColumn("Coverage");
        coverage.AddColumn("Value");
        coverage.AddRow("Collection", Escape(package.Stack));
        coverage.AddRow("Lane", Escape(package.Lane));
        coverage.AddRow("Included skills", package.Skills.Count.ToString());
        coverage.AddRow("Tokenizer", Escape(SkillTokenCounter.ModelName));

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel(package.Name, grid),
                    BuildRichShellPanel("bundle coverage", coverage),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel(package.Name, grid),
                    BuildRichShellPanel("bundle coverage", coverage)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("summary", new Spectre.Console.Markup(Escape(package.Description))));
        AnsiConsole.WriteLine();

        var table = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.Rounded);
        table.Title = new Spectre.Console.TableTitle("[bold]Included skills[/]");
        table.AddColumn("Alias");
        table.AddColumn("Area");
        table.AddColumn("Tokens");

        foreach (var skillName in package.Skills.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!skillIndex.TryGetValue(skillName, out var skill))
            {
                table.AddRow(Escape(ToAlias(skillName)), "[dim]missing[/]", "0");
                continue;
            }

            table.AddRow(
                Escape(ToAlias(skillName)),
                Escape($"{skill.Stack} / {skill.Lane}"),
                FormatTokenCount(skill.TokenCount));
        }

        SpectreConsole.Write(BuildRichShellPanel("included skills", table));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Focused bundles stay narrow by collection or workflow. Broad category-wide installs are intentionally not exposed here.[/]")));
    }

    private void RenderAgentDetailPanel(AgentEntry agent, AgentInstallLayout? layout, string? layoutError, bool installed)
    {
        var splitPanes = GetConsoleWidth() >= 155;
        var statusMarkup = layout is null
            ? "[grey]target unavailable[/]"
            : installed
                ? "[green]installed[/]"
                : "[grey]not installed[/]";

        var summary = BuildRichPropertyGrid(
            ("alias", Escape(ToAlias(agent.Name))),
            ("agent", $"[dim]{Escape(agent.Name)}[/]"),
            ("model", Escape(agent.Model)),
            ("skills", Escape(SummarizeAliases(agent.Skills))),
            ("tools", Escape(agent.Tools)),
            ("status", statusMarkup),
            ("target", layout is null ? $"[dim]{Escape(layoutError ?? "Unavailable")}[/]" : $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("install", $"[green]{Escape($"dotnet skills agent install {ToAlias(agent.Name)} --agent {Session.Agent.ToString().ToLowerInvariant()}")}[/]"));

        var surface = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.None);
        surface.AddColumn("Surface");
        surface.AddColumn("Value");
        surface.AddRow("Native layout", layout is null ? "[grey]not ready[/]" : Escape(layout.Mode.ToString()));
        surface.AddRow("Agent file", layout is null ? "[grey]-[/]" : Escape($"{agent.Name}{layout.FileExtension}"));
        surface.AddRow("Tool contract", string.IsNullOrWhiteSpace(agent.Tools) ? "[grey]-[/]" : Escape(agent.Tools));
        surface.AddRow("Routed skills", agent.Skills.Count.ToString());

        SpectreConsole.Write(
            splitPanes
                ? BuildRichTwoColumn(
                    BuildRichShellPanel(ToAlias(agent.Name), summary),
                    BuildRichShellPanel("agent surface", surface),
                    gap: 3)
                : BuildRichStack(
                    BuildRichShellPanel(ToAlias(agent.Name), summary),
                    BuildRichShellPanel("agent surface", surface)));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("summary", new Spectre.Console.Markup(Escape(agent.Description))));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Agents stay on native vendor targets only. No shared fallback directory is used for the agent surface.[/]")));
    }

    private void RenderSessionTargetPanel()
    {
        var skillLayout = ResolveSkillLayout();
        var agentLayout = TryResolveAgentLayout(out var agentLayoutError);

        var summary = BuildRichPropertyGrid(
            ("platform", Escape(Session.Agent.ToString())),
            ("scope", Escape(Session.Scope.ToString())),
            ("project", $"[dim]{Escape(CompactPath(Program.ResolveProjectRoot(Session.ProjectDirectory)))}[/]"),
            ("skill target", $"[dim]{Escape(CompactPath(skillLayout.PrimaryRoot.FullName))}[/]"),
            ("agent target", $"[dim]{Escape(agentLayout is null ? agentLayoutError ?? "Unavailable" : CompactPath(agentLayout.PrimaryRoot.FullName))}[/]"));

        var surfaces = new Spectre.Console.Table().Expand().Border(Spectre.Console.TableBorder.None);
        surfaces.AddColumn("Surface");
        surfaces.AddColumn("Platform");
        surfaces.AddColumn("Scope");
        surfaces.AddColumn("Path");
        surfaces.AddRow(
            "Skills",
            Escape(skillLayout.Agent.ToString()),
            Escape(skillLayout.Scope.ToString()),
            $"[dim]{Escape(CompactPath(skillLayout.PrimaryRoot.FullName))}[/]");
        surfaces.AddRow(
            "Agents",
            Escape(Session.Agent.ToString()),
            Escape(Session.Scope.ToString()),
            agentLayout is null ? $"[grey]{Escape(agentLayoutError ?? "Unavailable")}[/]" : $"[dim]{Escape(CompactPath(agentLayout.PrimaryRoot.FullName))}[/]");

        SpectreConsole.Write(
            BuildRichStack(
                BuildRichShellPanel("install destination", summary),
                BuildRichShellPanel("resolved surfaces", surfaces),
                BuildRichShellPanel(
                    "status rail",
                    new Spectre.Console.Markup("[dim]Choose the platform and scope that control where skills and agents are installed. Keep Auto only when native target detection is intentional.[/]"))));
    }

    private void RenderInfo(string message)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[dim]{Escape(message)}[/]");
        AnsiConsole.WriteLine();
        prompts.Pause("Press any key to continue...");
    }

    private void RenderError(string message)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[red]error:[/] {Escape(message)}");
        AnsiConsole.WriteLine();
        prompts.Pause("Press any key to continue...");
    }

    private bool ConfirmSkillInstallPreview(string title, IReadOnlyList<SkillEntry> skills, SkillInstallLayout layout, bool force, IReadOnlyList<SkillPackageEntry>? bundles = null)
    {
        if (skills.Count == 0)
        {
            RenderInfo("No skills were selected for this install plan.");
            return false;
        }

        AnsiConsole.Clear();
        RenderInstallPreviewPanel(title, skills, layout, force, bundles);
        return prompts.Confirm(
            force
                ? $"Write {skills.Count} skill(s) into {layout.PrimaryRoot.FullName}?"
                : $"Install {skills.Count} skill(s) into {layout.PrimaryRoot.FullName}?",
            defaultValue: true);
    }

    private SkillInstallLayout ResolveSkillLayout()
    {
        return SkillInstallTarget.Resolve(
            explicitTargetPath: null,
            Session.Agent,
            Session.Scope,
            Session.ProjectDirectory);
    }

    private AgentLayoutStatus ResolveAgentStatus()
    {
        try
        {
            var layout = AgentInstallTarget.Resolve(
                explicitTargetPath: null,
                Session.Agent,
                Session.Scope,
                Session.ProjectDirectory);
            return new AgentLayoutStatus(layout, "native target ready");
        }
        catch (Exception exception)
        {
            return new AgentLayoutStatus(null, exception.Message);
        }
    }

    private AgentInstallLayout? TryResolveAgentLayout(out string? error)
    {
        try
        {
            error = null;
            return AgentInstallTarget.Resolve(
                explicitTargetPath: null,
                Session.Agent,
                Session.Scope,
                Session.ProjectDirectory);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return null;
        }
    }

    private static string BuildSkillChoiceLabel(SkillEntry skill, IReadOnlyList<InstalledSkillRecord> installedSkills)
    {
        var installed = installedSkills.FirstOrDefault(record => string.Equals(record.Skill.Name, skill.Name, StringComparison.OrdinalIgnoreCase));
        if (installed is null)
        {
            return $"{ToAlias(skill.Name)} [{skill.Stack} / {skill.Lane}] ({FormatTokenCount(skill.TokenCount)} tokens)";
        }

        return installed.IsCurrent
            ? $"{ToAlias(skill.Name)} [{skill.Stack} / {skill.Lane}] ({FormatTokenCount(skill.TokenCount)} tokens, installed {installed.InstalledVersion})"
            : $"{ToAlias(skill.Name)} [{skill.Stack} / {skill.Lane}] ({FormatTokenCount(skill.TokenCount)} tokens, update {installed.InstalledVersion} -> {skill.Version})";
    }

    private static string BuildInstalledSkillChoiceLabel(InstalledSkillRecord record)
    {
        return record.IsCurrent
            ? $"{ToAlias(record.Skill.Name)} [{record.Skill.Stack} / {record.Skill.Lane}] ({record.InstalledVersion}, {FormatTokenCount(record.Skill.TokenCount)} tokens)"
            : $"{ToAlias(record.Skill.Name)} [{record.Skill.Stack} / {record.Skill.Lane}] ({record.InstalledVersion} -> {record.Skill.Version}, {FormatTokenCount(record.Skill.TokenCount)} tokens)";
    }

    private static string BuildCollectionChoiceLabel(CollectionCatalogView collection)
    {
        return $"{collection.Collection} ({collection.Lanes.Count} lanes, {collection.InstalledCount}/{collection.SkillCount} skills, {FormatTokenCount(collection.TokenCount)} tokens)";
    }

    private static string BuildLaneChoiceLabel(CollectionLaneView lane)
    {
        return $"{lane.Lane} ({lane.InstalledCount}/{lane.Skills.Count} skills, {FormatTokenCount(lane.TokenCount)} tokens)";
    }

    private IReadOnlyList<SkillPackageEntry> GetPrimaryBundles()
    {
        return skillCatalog.Packages
            .Where(CatalogOrganization.IsPrimaryBundle)
            .OrderBy(CatalogOrganization.FormatBundleSortKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ToAlias(string value) => value.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase)
        ? value["dotnet-".Length..]
        : value;

    private string LoadSkillPreview(SkillEntry skill)
    {
        try
        {
            var sourceDirectory = skillCatalog.ResolveSkillSource(skill.Name);
            var skillPath = new FileInfo(Path.Combine(sourceDirectory.FullName, "SKILL.md"));
            if (!skillPath.Exists)
            {
                return "SKILL.md is not available in the current catalog payload.";
            }

            var text = File.ReadAllText(skillPath.FullName);
            if (text.StartsWith("---\n", StringComparison.Ordinal))
            {
                var end = text.IndexOf("\n---\n", StringComparison.Ordinal);
                if (end >= 0)
                {
                    text = text[(end + "\n---\n".Length)..];
                }
            }

            var previewLines = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .Take(10)
                .ToArray();

            return previewLines.Length == 0
                ? "No previewable markdown lines were found in SKILL.md."
                : string.Join(Environment.NewLine, previewLines);
        }
        catch (Exception exception)
        {
            return $"Could not load preview: {exception.Message}";
        }
    }

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

    private static bool PathsEqual(DirectoryInfo left, DirectoryInfo right)
    {
        return string.Equals(
            Path.GetFullPath(left.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Escape(string value) => Markup.Escape(value);
}

internal interface IInteractivePrompts
{
    T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull;

    IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter, IReadOnlyList<T>? initiallySelected = null) where T : notnull;

    bool Confirm(string title, bool defaultValue);

    void Pause(string title);
}

internal sealed class CommandCenterInteractivePrompts : IInteractivePrompts
{
    public T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (choices.Count == 0)
        {
            throw new InvalidOperationException($"No choices are available for {title}.");
        }

        EnsureRichConsoleAvailable();

        var labels = choices.Select(formatter).ToArray();
        var prompt = new Spectre.Console.SelectionPrompt<string>
        {
            Title = $"[deepskyblue1]{EscapeMarkup(title)}[/]",
            PageSize = Math.Min(Math.Max(labels.Length, 5), 18),
            HighlightStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Aqua),
        };
        prompt.AddChoices(labels);
        var selectedLabel = SpectreConsole.Prompt(prompt);
        var index = Array.FindIndex(labels, label => string.Equals(label, selectedLabel, StringComparison.Ordinal));
        if (index >= 0)
        {
            return choices[index];
        }

        throw new InvalidOperationException($"Could not resolve the selected item for {title}.");
    }

    public IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter, IReadOnlyList<T>? initiallySelected = null) where T : notnull
    {
        if (choices.Count == 0)
        {
            return [];
        }

        EnsureRichConsoleAvailable();

        var labels = choices.Select(formatter).ToArray();
        var prompt = new Spectre.Console.MultiSelectionPrompt<string>
        {
            Title = $"[deepskyblue1]{EscapeMarkup(title)}[/]",
            InstructionsText = "[dim](Press <space> to toggle, <enter> to accept)[/]",
            PageSize = Math.Min(Math.Max(labels.Length, 5), 18),
            HighlightStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Aqua),
        };
        prompt.NotRequired();
        prompt.AddChoices(labels);
        if (initiallySelected is not null)
        {
            foreach (var selectedLabel in initiallySelected.Select(formatter).Distinct(StringComparer.Ordinal))
            {
                prompt.Select(selectedLabel);
            }
        }
        var selectedLabels = SpectreConsole.Prompt(prompt);
        var selectedSet = selectedLabels.ToHashSet(StringComparer.Ordinal);
        return labels
            .Select((label, index) => (label, index))
            .Where(item => selectedSet.Contains(item.label))
            .Select(item => choices[item.index])
            .ToArray();
    }

    public bool Confirm(string title, bool defaultValue)
    {
        EnsureRichConsoleAvailable();
        return SpectreConsole.Confirm($"[deepskyblue1]{EscapeMarkup(title)}[/]", defaultValue);
    }

    public void Pause(string title)
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(title)}[/]");

        if (Console.IsInputRedirected)
        {
            return;
        }

        Console.ReadKey(intercept: true);
    }

    private static void EnsureRichConsoleAvailable()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            throw new InvalidOperationException("The interactive shell requires a real terminal with the rich console surface enabled.");
        }
    }

    private static string EscapeMarkup(string value)
    {
        return value
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }
}

internal sealed class InteractiveSessionState
{
    public AgentPlatform Agent { get; set; }

    public InstallScope Scope { get; set; }

    public string? ProjectDirectory { get; set; }

    public bool BundledOnly { get; set; }
}

internal sealed record MenuOption<T>(string Label, T Value);

internal sealed record AgentLayoutStatus(AgentInstallLayout? Layout, string Summary);

internal sealed record PackageSignalView(string Signal, string Kind, SkillEntry Skill);

internal sealed record HomeActionView(HomeAction Action, string Label, string Summary, string Command, string Accent);

internal enum HomeAction
{
    SyncProject,
    ManageBundles,
    InstallSkills,
    Analysis,
    ManageInstalled,
    UpdateAll,
    Agents,
    Settings,
    Exit,
}

internal enum SettingsAction
{
    InstallDestination,
    RefreshCatalog,
    Help,
    Back,
}

internal enum SkillCatalogAction
{
    Inspect,
    Install,
    UpdateAllOutdated,
    UpdateOutdated,
    Back,
}

internal enum CatalogAnalysisAction
{
    Tree,
    HeavySkill,
    PackageSignals,
    Back,
}

internal enum CatalogTreeAction
{
    Back,
}

internal enum InstalledSkillsAction
{
    Inspect,
    ReviewState,
    Repair,
    CopyOrMove,
    Remove,
    RemoveAll,
    UpdateAll,
    Update,
    Back,
}

internal enum SkillDetailAction
{
    Install,
    Update,
    Repair,
    CopyOrMove,
    Remove,
    Back,
}

internal enum PackageAction
{
    Inspect,
    Install,
    Back,
}

internal enum PackageDetailAction
{
    Install,
    Back,
}

internal enum PackageSignalAction
{
    InspectSkill,
    Back,
}

internal enum AgentAction
{
    Inspect,
    Install,
    Repair,
    CopyOrMove,
    Remove,
    Back,
}

internal enum AgentDetailAction
{
    Install,
    Repair,
    CopyOrMove,
    Remove,
    Back,
}

internal enum SessionTargetAction
{
    Platform,
    Scope,
    Back,
}
