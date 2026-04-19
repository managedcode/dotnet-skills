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

                var homeActions = GetHomeActions(GetInstalledSkillCount(), GetOutdatedSkillCount());
                var action = prompts.SelectHomeAction(homeActions);

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
                    case HomeAction.RemoveAll:
                        RemoveAllInstalledSkillsForCurrentTarget();
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

    private int GetInstalledSkillCount()
    {
        var installer = new SkillInstaller(skillCatalog);
        return installer.GetInstalledSkills(ResolveSkillLayout()).Count;
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
        var homeActions = GetHomeActions(installedSkills.Count, outdatedSkills)
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

        var collectionSurfaceCards = collectionViews
            .Take(5)
            .Select(collection => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                CompactText(collection.Collection, 22),
                collection.InstalledCount == 0 ? "grey" : "green3",
                $"[bold]{collection.InstalledCount}/{collection.SkillCount}[/] [dim]installed[/]",
                $"[bold]{FormatTokenCount(collection.TokenCount)}[/] [dim]Tokens[/]"))
            .ToArray();

        var bundleSurfaceCards = featuredBundles
            .Select(bundle =>
            {
                var bundleTokens = bundle.Skills
                    .Select(skillName => skillCatalog.Skills.FirstOrDefault(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase)))
                    .Where(skill => skill is not null)
                    .Sum(skill => skill!.TokenCount);

                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    bundle.Name,
                    "springgreen3",
                    $"[dim]{Escape(CompactText(CatalogOrganization.ResolveBundleAreaLabel(bundle), 26))}[/]",
                    $"[bold]{FormatTokenCount(bundleTokens)}[/] [dim]Tokens[/]");
            })
            .ToArray();

        var surfaces = splitPanes
            ? BuildRichTwoColumn(
                BuildRichCardGrid(collectionSurfaceCards, maxColumns: 1),
                BuildRichCardGrid(bundleSurfaceCards, maxColumns: 1),
                gap: 3)
            : BuildRichStack(
                BuildRichCardGrid(collectionSurfaceCards, maxColumns: 1),
                BuildRichCardGrid(bundleSurfaceCards, maxColumns: 1));

        var heavySkillCards = largestSkills
            .Select(skill => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                ToAlias(skill.Name),
                "yellow",
                $"[dim]{Escape(CompactText($"{skill.Stack} / {skill.Lane}", 28))}[/]",
                $"[bold]{FormatTokenCount(skill.TokenCount)}[/] [dim]Tokens[/]"))
            .ToArray();

        var signalCards = packageSignals
            .Take(5)
            .Select(signal => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                CompactText(signal.Signal, 24),
                "turquoise2",
                $"[bold]{Escape(ToAlias(signal.Skill.Name))}[/]",
                $"[dim]{Escape(signal.Kind)}[/]"))
            .ToArray();

        var analysis = splitPanes
            ? BuildRichTwoColumn(
                BuildRichCardGrid(heavySkillCards, maxColumns: 1),
                BuildRichCardGrid(signalCards, maxColumns: 1),
                gap: 3)
            : BuildRichStack(
                BuildRichCardGrid(heavySkillCards, maxColumns: 1),
                BuildRichCardGrid(signalCards, maxColumns: 1));

        var controlLines = new List<Spectre.Console.Rendering.IRenderable>
        {
            new Spectre.Console.Markup("[bold grey]1-9[/] [dim]choose surface[/]   [bold grey]0[/] [dim]exit[/]   [bold grey]Space[/] [dim]multi-select[/]"),
            new Spectre.Console.Markup("[dim]Collections narrow before install[/]   [dim]Bundles stay focused[/]"),
            new Spectre.Console.Markup("[dim]Install preview stays mandatory before writes[/]"),
        };

        controlLines.Add(
            outdatedSkills > 0
                ? new Spectre.Console.Markup($"[yellow]Update all skills[/] [dim]is available for {outdatedSkills} outdated install(s)[/]")
                : new Spectre.Console.Markup("[yellow]Update all skills[/] [dim]stays available and returns a clear no-op when everything is current[/]"));
        controlLines.Add(
            installedSkills.Count > 0
                ? new Spectre.Console.Markup($"[red]Remove all skills[/] [dim]is available for {installedSkills.Count} installed skill(s)[/]")
                : new Spectre.Console.Markup("[red]Remove all skills[/] [dim]stays available and returns a simple no-op when nothing is installed[/]"));

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

    private static IReadOnlyList<HomeActionView> GetHomeActions(int installedSkillCount, int outdatedSkillCount)
    {
        var actions = new List<HomeActionView>
        {
            new HomeActionView('1', HomeAction.SyncProject, "Project", "sync from .csproj signals", "dotnet skills install --auto", "deepskyblue1"),
            new HomeActionView('2', HomeAction.InstallSkills, "Collections", "browse Collection -> Lane -> Skill", "dotnet skills list --available-only", "springgreen3"),
            new HomeActionView('3', HomeAction.Analysis, "Analysis", "tree, tokens, package signals", "dotnet skills catalog tokens", "gold1"),
            new HomeActionView('4', HomeAction.ManageBundles, "Bundles", "focused multi-skill installs", "dotnet skills bundle list", "turquoise2"),
            new HomeActionView('5', HomeAction.ManageInstalled, "Installed", "keep, remove, repair, move", "dotnet skills list --installed-only", "orange3"),
            new HomeActionView(
                '6',
                HomeAction.RemoveAll,
                "Remove all skills",
                installedSkillCount == 0 ? "0 installed skills" : $"{installedSkillCount} installed skills",
                "dotnet skills remove --all",
                "red"),
        };

        actions.Add(
            new HomeActionView(
                '7',
                HomeAction.UpdateAll,
                "Update all skills",
                outdatedSkillCount == 0 ? "0 outdated installed skills" : $"{outdatedSkillCount} outdated installed skills",
                "dotnet skills update",
                "yellow"));

        actions.AddRange(
        [
            new HomeActionView('8', HomeAction.Agents, "Agents", "native agent lifecycle", "dotnet agents list", "green3"),
            new HomeActionView('9', HomeAction.Settings, "Workspace", "platform, scope, catalog source", "dotnet skills where", "deepskyblue1"),
            new HomeActionView('0', HomeAction.Exit, "Exit", "leave the control center", "exit", "grey"),
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

    private static Spectre.Console.Panel BuildRichShellPanel(string title, Spectre.Console.Rendering.IRenderable content, string accent = "deepskyblue1")
    {
        var panel = new Spectre.Console.Panel(content)
            .Header($"[bold {accent}]{Escape(title)}[/]")
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

    private static Spectre.Console.Panel BuildRichDetailCard(string title, string accent, params string[] lines)
    {
        var items = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => (Spectre.Console.Rendering.IRenderable)new Spectre.Console.Markup(line))
            .ToArray();

        return BuildRichShellPanel(title, BuildRichStack(items), accent);
    }

    private static Spectre.Console.Grid BuildRichCardGrid(IReadOnlyList<Spectre.Console.Rendering.IRenderable> cards, int maxColumns = 3)
    {
        var grid = new Spectre.Console.Grid { Expand = true };
        grid.AddColumn();

        if (cards.Count == 0)
        {
            grid.AddRow(new Spectre.Console.Markup("[dim]No items available.[/]"));
            return grid;
        }

        grid = new Spectre.Console.Grid { Expand = true };
        var consoleWidth = GetConsoleWidth();
        var columnCount = consoleWidth >= 190
            ? Math.Min(maxColumns, 3)
            : consoleWidth >= 130
                ? Math.Min(maxColumns, 2)
                : 1;
        columnCount = Math.Max(1, Math.Min(columnCount, cards.Count));

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            grid.AddColumn(new Spectre.Console.GridColumn
            {
                Padding = new Spectre.Console.Padding(0, 0, columnIndex == columnCount - 1 ? 0 : 2, 0),
            });
        }

        for (var rowIndex = 0; rowIndex < cards.Count; rowIndex += columnCount)
        {
            var row = new Spectre.Console.Rendering.IRenderable[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cardIndex = rowIndex + columnIndex;
                row[columnIndex] = cardIndex < cards.Count
                    ? cards[cardIndex]
                    : new Spectre.Console.Markup(" ");
            }

            grid.AddRow(row);
        }

        return grid;
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
        table.AddColumn("Key");
        table.AddColumn("Area");
        table.AddColumn("Use");
        table.AddColumn("Command");

        foreach (var action in homeActions)
        {
            table.AddRow(
                $"[bold grey]{Escape(action.HotKey.ToString())}[/]",
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
            grid.AddRow(new Spectre.Console.Markup($"[bold grey]{Escape(action.HotKey.ToString())}[/] [{action.Accent}]{Escape(action.Label)}[/] [dim]{Escape(action.Summary)}[/]"));
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
                    if (selectedSkills is null || selectedSkills.Count == 0)
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
            }

            actions.Add(new MenuOption<InstalledSkillsAction>("Remove all skills", InstalledSkillsAction.RemoveAll));

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
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion})",
                        backLabel: "Back");
                    if (selected is null || selected.Count == 0)
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
                        orderedInstalled,
                        backLabel: "Back");
                    if (kept is null)
                    {
                        break;
                    }
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
                        ? $"Remove all {removed.Length} installed skill(s) from {layout.PrimaryRoot.FullName}?"
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
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion})",
                        backLabel: "Back");
                    if (selected is null || selected.Count == 0)
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
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion})",
                        backLabel: "Back");
                    if (selected is null || selected.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Remove {selected.Count} skill(s) from {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        RemoveSkills(selected.Select(record => record.Skill).ToArray());
                    }

                    break;
                }
                case InstalledSkillsAction.RemoveAll:
                {
                    RemoveAllInstalledSkills(
                        installedSkills,
                        layout,
                        "No catalog skills are installed in this target yet.");

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
                    if (selectedPackages is null || selectedPackages.Count == 0)
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
                    if (selectedAgents is null || selectedAgents.Count == 0)
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
                    if (selectedAgents is null || selectedAgents.Count == 0)
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
                    if (selectedAgents is null || selectedAgents.Count == 0)
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
                    if (selectedAgents is null || selectedAgents.Count == 0)
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

    private void RemoveAllInstalledSkillsForCurrentTarget()
    {
        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var installedSkills = installer.GetInstalledSkills(layout)
            .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
            .ToArray();

        RemoveAllInstalledSkills(
            installedSkills,
            layout,
            "No catalog skills are installed in this target yet.");
    }

    private void RemoveAllInstalledSkills(
        IReadOnlyList<InstalledSkillRecord> installedSkills,
        SkillInstallLayout layout,
        string emptyMessage)
    {
        if (installedSkills.Count == 0)
        {
            RenderInfo(emptyMessage);
            return;
        }

        if (prompts.Confirm($"Remove all {installedSkills.Count} installed skill(s) from {layout.PrimaryRoot.FullName}?", defaultValue: true))
        {
            RemoveSkills(installedSkills.Select(record => record.Skill).ToArray(), layout, pause: true);
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
            outdatedSkills,
            backLabel: "Back");
        if (selected is null || selected.Count == 0)
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

        var collectionCards = collectionViews
            .OrderBy(collection => CatalogOrganization.GetStackRank(collection.Collection))
            .ThenBy(collection => collection.Collection, StringComparer.Ordinal)
            .Select(collection =>
            {
                var sampleLanes = collection.Lanes.Take(3).Select(lane => lane.Lane).ToArray();
                var primaryLane = collection.Lanes
                    .OrderByDescending(lane => lane.TokenCount)
                    .ThenBy(lane => lane.Lane, StringComparer.Ordinal)
                    .FirstOrDefault();
                var sampleSkills = collection.Lanes
                    .SelectMany(lane => lane.Skills)
                    .Select(skill => skill.Name)
                    .ToArray();

                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    collection.Collection,
                    collection.InstalledCount == 0 ? "grey" : collection.InstalledCount == collection.SkillCount ? "green3" : "yellow",
                    $"[bold]{collection.InstalledCount}/{collection.SkillCount}[/] [dim]installed[/]  [bold]{collection.Lanes.Count}[/] [dim]lanes[/]",
                    $"[bold]{FormatTokenCount(collection.TokenCount)}[/] [dim]Tokens[/]",
                    $"[dim]sample lanes[/] {Escape(string.Join(", ", sampleLanes))}{(collection.Lanes.Count > sampleLanes.Length ? $" [grey](+{collection.Lanes.Count - sampleLanes.Length})[/]" : string.Empty)}",
                    $"[dim]top lane[/] {Escape(primaryLane?.Lane ?? "-")} [grey]({FormatTokenCount(primaryLane?.TokenCount ?? 0)})[/]",
                    $"[dim]skills[/] {Escape(SummarizeAliases(sampleSkills, take: 5))}");
            })
            .ToArray();

        var spotlightCards = collectionViews
            .OrderByDescending(collection => collection.TokenCount)
            .ThenBy(collection => collection.Collection, StringComparer.Ordinal)
            .Take(4)
            .Select(collection => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                collection.Collection,
                "gold1",
                $"[bold]{FormatTokenCount(collection.TokenCount)}[/] [dim]Tokens[/]",
                $"[dim]coverage[/] {collection.InstalledCount}/{collection.SkillCount} [dim]installed[/]",
                $"[dim]widest lane[/] {Escape(collection.Lanes.OrderByDescending(lane => lane.Skills.Count).ThenBy(lane => lane.Lane, StringComparer.Ordinal).FirstOrDefault()?.Lane ?? "-")}"))
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("navigation", flow),
            BuildRichShellPanel("collection browser", overview),
            gap: 3));
        AnsiConsole.WriteLine();

        SpectreConsole.Write(BuildRichShellPanel(
            "collection directory",
            BuildRichCardGrid(collectionCards, maxColumns: 2),
            "deepskyblue1"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "heaviest collections",
            BuildRichCardGrid(spotlightCards, maxColumns: 2),
            "gold1"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Choose a collection first. Install surfaces only appear after the lane boundary is explicit.[/]")));
    }

    private void RenderCollectionDetailPanel(CollectionCatalogView collectionView, SkillInstallLayout layout)
    {
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

        var laneCards = collectionView.Lanes
            .OrderBy(lane => CatalogOrganization.GetLaneRank(lane.Lane))
            .ThenBy(lane => lane.Lane, StringComparer.Ordinal)
            .Select(lane =>
            {
                var examples = lane.Skills.Take(4).Select(skill => ToAlias(skill.Name)).ToArray();
                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    lane.Lane,
                    lane.InstalledCount == 0 ? "grey" : lane.InstalledCount == lane.Skills.Count ? "green3" : "yellow",
                    $"[bold]{lane.InstalledCount}/{lane.Skills.Count}[/] [dim]installed[/]",
                    $"[bold]{FormatTokenCount(lane.TokenCount)}[/] [dim]Tokens[/]",
                    $"[dim]examples[/] {Escape(string.Join(", ", examples))}{(lane.Skills.Count > examples.Length ? $" [grey](+{lane.Skills.Count - examples.Length})[/]" : string.Empty)}");
            })
            .ToArray();

        var heavySkillCards = collectionView.Lanes
            .SelectMany(lane => lane.Skills)
            .OrderByDescending(skill => skill.TokenCount)
            .ThenBy(skill => skill.Name, StringComparer.Ordinal)
            .Take(8)
            .Select(skill => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                ToAlias(skill.Name),
                "yellow",
                $"[dim]{Escape(skill.Lane)}[/] [bold]{FormatTokenCount(skill.TokenCount)}[/] [dim]Tokens[/]",
                Escape(CompactDescription(skill.Description))))
            .ToArray();

        var relatedBundles = GetPrimaryBundles()
            .Where(package => string.Equals(package.Stack, collectionView.Collection, StringComparison.OrdinalIgnoreCase)
                              || package.Skills.Any(skillName =>
                                  collectionView.Lanes.SelectMany(lane => lane.Skills)
                                      .Any(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase))))
            .DistinctBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(CatalogOrganization.FormatBundleSortKey, StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        var relatedBundleCards = relatedBundles
            .Select(bundle =>
            {
                var tokenCount = bundle.Skills
                    .Select(skillName => skillCatalog.Skills.FirstOrDefault(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase)))
                    .Where(skill => skill is not null)
                    .Sum(skill => skill!.TokenCount);

                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    bundle.Name,
                    "green3",
                    $"[dim]{Escape(CatalogOrganization.ResolveBundleAreaLabel(bundle))}[/]",
                    $"[bold]{bundle.Skills.Count}[/] [dim]skills[/]  [bold]{FormatTokenCount(tokenCount)}[/] [dim]Tokens[/]",
                    $"[green]{Escape($"dotnet skills install bundle {bundle.Name}")}[/]");
            })
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("collection modes", flow),
            BuildRichShellPanel(collectionView.Collection, summary),
            gap: 3));
        AnsiConsole.WriteLine();

        SpectreConsole.Write(BuildRichShellPanel(
            "lane directory",
            BuildRichCardGrid(laneCards, maxColumns: 2),
            "deepskyblue1"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "heavy skills",
            BuildRichCardGrid(heavySkillCards, maxColumns: 2),
            "yellow"));
        AnsiConsole.WriteLine();

        if (relatedBundles.Length > 0)
        {
            SpectreConsole.Write(BuildRichShellPanel(
                "related bundles",
                BuildRichCardGrid(relatedBundleCards, maxColumns: 2),
                "green3"));
            AnsiConsole.WriteLine();
        }

        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]This collection is already narrowed. The next selection happens at the lane level, never against the full catalog.[/]")));
    }

    private void RenderCatalogAnalysisPanel(IReadOnlyList<CollectionCatalogView> collectionViews, SkillInstallLayout layout, IReadOnlyList<PackageSignalView> packageSignals)
    {
        var summary = new Spectre.Console.Grid();
        summary.AddColumn(new Spectre.Console.GridColumn { NoWrap = true });
        summary.AddColumn();
        summary.AddRow(new Spectre.Console.Markup("[dim]target[/]"), new Spectre.Console.Markup($"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"));
        summary.AddRow(new Spectre.Console.Markup("[dim]collections[/]"), new Spectre.Console.Markup(collectionViews.Count.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]skills[/]"), new Spectre.Console.Markup(skillCatalog.Skills.Count.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]package signals[/]"), new Spectre.Console.Markup(packageSignals.Count.ToString()));
        summary.AddRow(new Spectre.Console.Markup("[dim]tokens[/]"), new Spectre.Console.Markup(FormatTokenCount(skillCatalog.Skills.Sum(skill => skill.TokenCount))));

        var flow = BuildRichStack(
            new Spectre.Console.Markup("[deepskyblue1]Tree view[/] [dim]remains the full Collection -> Lane -> Skill hierarchy[/]"),
            new Spectre.Console.Markup("[yellow]Token hotspots[/] [dim]show where prompt weight concentrates[/]"),
            new Spectre.Console.Markup("[green3]Package signals[/] [dim]show which NuGet ids lead to install recommendations[/]"));

        var heavySkillCards = skillCatalog.Skills
            .OrderByDescending(skill => skill.TokenCount)
            .ThenBy(skill => skill.Name, StringComparer.Ordinal)
            .Take(10)
            .Select(skill => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                ToAlias(skill.Name),
                "yellow",
                $"[dim]{Escape(skill.Stack)} / {Escape(skill.Lane)}[/]",
                $"[bold]{FormatTokenCount(skill.TokenCount)}[/] [dim]Tokens[/]"))
            .ToArray();

        var packageCards = packageSignals
            .Take(12)
            .Select(entry => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                entry.Signal,
                entry.Kind.Equals("Exact", StringComparison.Ordinal) ? "green3" : "deepskyblue1",
                $"[dim]{Escape(entry.Kind)}[/] -> [bold]{Escape(ToAlias(entry.Skill.Name))}[/]",
                $"[dim]{Escape(entry.Skill.Stack)} / {Escape(entry.Skill.Lane)}[/]",
                $"[bold]{FormatTokenCount(entry.Skill.TokenCount)}[/] [dim]Tokens[/]"))
            .ToArray();

        var collectionCards = collectionViews
            .OrderByDescending(collection => collection.TokenCount)
            .ThenBy(collection => collection.Collection, StringComparer.Ordinal)
            .Select(collection => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                collection.Collection,
                "deepskyblue1",
                $"[bold]{collection.Lanes.Count}[/] [dim]lanes[/]  [bold]{collection.SkillCount}[/] [dim]skills[/]",
                $"[bold]{FormatTokenCount(collection.TokenCount)}[/] [dim]Tokens[/]",
                $"[dim]heaviest lane[/] {Escape(collection.Lanes.OrderByDescending(lane => lane.TokenCount).ThenBy(lane => lane.Lane, StringComparer.Ordinal).FirstOrDefault()?.Lane ?? "-")}"))
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("catalog analysis", summary),
            BuildRichShellPanel("analysis flow", flow),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "collection composition",
            BuildRichCardGrid(collectionCards, maxColumns: 2),
            "deepskyblue1"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "token hotspots",
            BuildRichCardGrid(heavySkillCards, maxColumns: 2),
            "yellow"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "package entry points",
            BuildRichCardGrid(packageCards, maxColumns: 2),
            "green3"));
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
        var summary = BuildRichPropertyGrid(
            ("signals", packageSignals.Count.ToString()),
            ("exact", packageSignals.Count(entry => string.Equals(entry.Kind, "Exact", StringComparison.Ordinal)).ToString()),
            ("prefix", packageSignals.Count(entry => string.Equals(entry.Kind, "Prefix", StringComparison.Ordinal)).ToString()),
            ("skills", skillCatalog.Skills.Count.ToString()));

        var flow = BuildRichStack(
            new Spectre.Console.Markup("[green3]Exact[/] [dim]matches fire on a concrete NuGet id[/]"),
            new Spectre.Console.Markup("[deepskyblue1]Prefix[/] [dim]matches catch package families such as provider or SDK prefixes[/]"),
            new Spectre.Console.Markup("[yellow]Tokens[/] [dim]show the prompt cost of the routed skill before install[/]"));

        var exactCards = packageSignals
            .Where(entry => string.Equals(entry.Kind, "Exact", StringComparison.Ordinal))
            .Select(entry => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                entry.Signal,
                "green3",
                $"[bold]{Escape(ToAlias(entry.Skill.Name))}[/]",
                $"[dim]{Escape(entry.Skill.Stack)} / {Escape(entry.Skill.Lane)}[/]",
                $"[bold]{FormatTokenCount(entry.Skill.TokenCount)}[/] [dim]Tokens[/]"))
            .ToArray();

        var prefixCards = packageSignals
            .Where(entry => string.Equals(entry.Kind, "Prefix", StringComparison.Ordinal))
            .Select(entry => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                entry.Signal,
                "deepskyblue1",
                $"[bold]{Escape(ToAlias(entry.Skill.Name))}[/]",
                $"[dim]{Escape(entry.Skill.Stack)} / {Escape(entry.Skill.Lane)}[/]",
                $"[bold]{FormatTokenCount(entry.Skill.TokenCount)}[/] [dim]Tokens[/]"))
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("package signals", summary),
            BuildRichShellPanel("signal flow", flow),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "exact entry points",
            BuildRichCardGrid(exactCards, maxColumns: 2),
            "green3"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "prefix entry points",
            BuildRichCardGrid(prefixCards, maxColumns: 2),
            "deepskyblue1"));
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

        var writeSetCards = selectedSkills
            .GroupBy(skill => skill.Stack, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => CatalogOrganization.GetStackRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var lanes = group
                    .Select(skill => skill.Lane)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(CatalogOrganization.GetLaneRank)
                    .ThenBy(lane => lane, StringComparer.Ordinal)
                    .ToArray();

                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    group.Key,
                    "deepskyblue1",
                    $"[bold]{group.Count()}[/] [dim]skills[/]  [bold]{FormatTokenCount(group.Sum(skill => skill.TokenCount))}[/] [dim]Tokens[/]",
                    $"[dim]lanes[/] {Escape(string.Join(", ", lanes.Take(3)))}{(lanes.Length > 3 ? $" [grey](+{lanes.Length - 3})[/]" : string.Empty)}");
            })
            .ToArray();

        var selectedSkillCards = selectedSkills
            .Select(skill => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                ToAlias(skill.Name),
                "green3",
                $"[dim]{Escape(skill.Stack)} / {Escape(skill.Lane)}[/]",
                $"[bold]{FormatTokenCount(skill.TokenCount)}[/] [dim]Tokens[/]",
                force ? "[yellow]repair / overwrite[/]" : "[green]new write set member[/]"))
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel(title, summary),
            BuildRichShellPanel(
                "install mode",
                BuildRichStack(
                    new Spectre.Console.Markup(force
                        ? "[yellow]Repair / overwrite[/] [dim]will rewrite already-installed payloads in the selected target[/]"
                        : "[green]Install[/] [dim]will add only the selected write set to the current target[/]"),
                    new Spectre.Console.Markup("[deepskyblue1]Preview[/] [dim]is the exact filesystem write set before confirmation[/]")),
                "green3"),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "write set by collection",
            BuildRichCardGrid(writeSetCards, maxColumns: 2),
            "deepskyblue1"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "selected skills",
            BuildRichCardGrid(selectedSkillCards, maxColumns: 2),
            "green3"));
        AnsiConsole.WriteLine();

        if (bundles is not null && bundles.Count > 0)
        {
            var bundleCards = bundles
                .OrderBy(bundle => bundle.Name, StringComparer.Ordinal)
                .Select(bundle => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    bundle.Name,
                    "yellow",
                    $"[dim]{Escape(CatalogOrganization.ResolveBundleAreaLabel(bundle))}[/]",
                    $"[bold]{bundle.Skills.Count}[/] [dim]skills[/]"))
                .ToArray();

            SpectreConsole.Write(BuildRichShellPanel(
                "selected bundles",
                BuildRichCardGrid(bundleCards, maxColumns: 2),
                "yellow"));
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
        var outdatedCount = installedSkills.Count(record => !record.IsCurrent);
        var installedByCollection = installedSkills
            .GroupBy(record => record.Skill.Stack, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => CatalogOrganization.GetStackRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        var summary = BuildRichPropertyGrid(
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("installed", installedSkills.Count.ToString()),
            ("outdated", outdatedCount == 0 ? "[green]0[/]" : $"[yellow]{outdatedCount}[/]"),
            ("collections", installedSkills.Select(record => record.Skill.Stack).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()),
            ("tokens", FormatTokenCount(installedSkills.Sum(record => record.Skill.TokenCount))));

        var lifecycle = BuildRichStack(
            new Spectre.Console.Markup("[yellow]Update all skills[/] [dim]stays available even when everything is current[/]"),
            new Spectre.Console.Markup("[deepskyblue1]Review installed set[/] [dim]starts checked so deselection prepares removal[/]"),
            new Spectre.Console.Markup("[red]Remove all skills[/] [dim]clears only this resolved target[/]"));

        var collectionCards = installedByCollection
            .Select(group =>
            {
                var sampleSkills = group
                    .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
                    .Take(4)
                    .Select(record => ToAlias(record.Skill.Name))
                    .ToArray();
                var collectionOutdated = group.Count(record => !record.IsCurrent);

                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    group.Key,
                    collectionOutdated == 0 ? "green3" : "yellow",
                    $"[bold]{group.Count()}[/] [dim]skills[/]  [bold]{FormatTokenCount(group.Sum(record => record.Skill.TokenCount))}[/] [dim]Tokens[/]",
                    collectionOutdated == 0
                        ? "[green]all current[/]"
                        : $"[yellow]{collectionOutdated} update available[/]",
                    $"[dim]sample[/] {Escape(string.Join(", ", sampleSkills))}{(group.Count() > sampleSkills.Length ? $" [grey](+{group.Count() - sampleSkills.Length})[/]" : string.Empty)}");
            })
            .ToArray();

        var focusCards = installedSkills
            .OrderBy(record => record.IsCurrent)
            .ThenByDescending(record => record.Skill.TokenCount)
            .ThenBy(record => record.Skill.Name, StringComparer.Ordinal)
            .Take(10)
            .Select(record => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                ToAlias(record.Skill.Name),
                record.IsCurrent ? "green3" : "yellow",
                $"[dim]{Escape(record.Skill.Stack)} / {Escape(record.Skill.Lane)}[/]",
                record.IsCurrent
                    ? $"[green]{Escape(record.InstalledVersion)} current[/]"
                    : $"[yellow]{Escape(record.InstalledVersion)} -> {Escape(record.Skill.Version)}[/]",
                $"[bold]{FormatTokenCount(record.Skill.TokenCount)}[/] [dim]Tokens[/]"))
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("installed summary", summary),
            BuildRichShellPanel("lifecycle", lifecycle),
            gap: 3));
        AnsiConsole.WriteLine();

        if (scopeInventory.Count > 1)
        {
            var scopeCards = scopeInventory
                .Select(row => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    row.Scope.ToString(),
                    PathsEqual(row.TargetRoot, layout.PrimaryRoot) ? "deepskyblue1" : "grey",
                    $"[dim]{Escape(CompactPath(row.TargetRoot.FullName))}[/]",
                    $"[bold]{row.InstalledSkills.Count}[/] [dim]installed skills[/]"))
                .ToArray();

            SpectreConsole.Write(BuildRichShellPanel(
                "scope inventory",
                BuildRichCardGrid(scopeCards, maxColumns: 2),
                "deepskyblue1"));
            AnsiConsole.WriteLine();
        }

        if (installedSkills.Count == 0)
        {
            SpectreConsole.Write(BuildRichShellPanel(
                "status rail",
                new Spectre.Console.Markup("[dim]No catalog skills are installed in this target yet. Use Collections or Bundles to create the first write set.[/]")));
            return;
        }

        SpectreConsole.Write(BuildRichShellPanel(
            "installed collections",
            BuildRichCardGrid(collectionCards, maxColumns: 2),
            "green3"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "skill focus",
            BuildRichCardGrid(focusCards, maxColumns: 2),
            outdatedCount == 0 ? "green3" : "yellow"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Installed state is explicit here: update all outdated skills directly, review the checked set, remove selected skills, or clear this exact target.[/]")));
    }

    private void RenderBundleBrowserPanel(IReadOnlyList<SkillPackageEntry> visibleBundles)
    {
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

        var flow = BuildRichStack(
            new Spectre.Console.Markup("[green3]Bundles[/] [dim]are one-command install presets, not taxonomy pages[/]"),
            new Spectre.Console.Markup("[deepskyblue1]Collections[/] [dim]stay the browse-first surface when you want lane-level control[/]"),
            new Spectre.Console.Markup("[yellow]Tokens[/] [dim]keep the install cost visible before you commit the write set[/]"));

        var highlightCards = bundleRows
            .OrderByDescending(item => item.TokenCount)
            .ThenBy(item => item.Bundle.Name, StringComparer.Ordinal)
            .Take(4)
            .Select(row => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                row.Bundle.Name,
                "yellow",
                $"[bold]{FormatTokenCount(row.TokenCount)}[/] [dim]Tokens[/]",
                $"[dim]{row.Bundle.Skills.Count} skills[/]  [dim]{Escape(CatalogOrganization.ResolveBundleAreaLabel(row.Bundle))}[/]"))
            .ToArray();

        var bundleCards = bundleRows
            .Select(row => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                row.Bundle.Name,
                row.Bundle.Kind.Equals("curated", StringComparison.OrdinalIgnoreCase) ? "green3" : "deepskyblue1",
                $"[dim]{Escape(CatalogOrganization.ResolveBundleAreaLabel(row.Bundle))}[/]",
                $"[bold]{row.Bundle.Skills.Count}[/] [dim]skills[/]  [bold]{FormatTokenCount(row.TokenCount)}[/] [dim]Tokens[/]",
                $"[dim]sample[/] {Escape(SummarizeAliases(row.Bundle.Skills))}",
                $"[green]{Escape($"dotnet skills install bundle {row.Bundle.Name}")}[/]"))
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("bundle summary", summary),
            BuildRichShellPanel("bundle flow", flow),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "bundle hotspots",
            BuildRichCardGrid(highlightCards, maxColumns: 2),
            "yellow"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "bundle directory",
            BuildRichCardGrid(bundleCards, maxColumns: 2),
            "green3"));
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

        var matchCards = plan.ScanResult.Recommendations
            .Where(recommendation => recommendation.IsAutoInstallCandidate)
            .OrderByDescending(recommendation => recommendation.Confidence)
            .ThenBy(recommendation => recommendation.Skill.Name, StringComparer.Ordinal)
            .Select(recommendation =>
            {
                var isInstalled = installedBefore.ContainsKey(recommendation.Skill.Name);
                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    ToAlias(recommendation.Skill.Name),
                    isInstalled ? "grey" : "green3",
                    $"{FormatConfidenceMarkup(recommendation.Confidence)} [dim]confidence[/]",
                    $"[dim]{Escape(recommendation.Skill.Stack)} / {Escape(recommendation.Skill.Lane)}[/]",
                    $"[dim]signals[/] {Escape(CompactText(string.Join(", ", recommendation.Signals), 64))}",
                    isInstalled ? "[grey]already installed[/]" : "[green]new install candidate[/]");
            })
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("project sync", summary),
            BuildRichShellPanel("signal flow", flow),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "matched skills",
            BuildRichCardGrid(matchCards, maxColumns: 2),
            "green3"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup(newSkills.Count == 0
                ? "[dim]All matched skills are already installed in this target.[/]"
                : "[dim]Only the new slice will be proposed for install. Existing skills stay untouched unless you choose update or repair separately.[/]")));
    }

    private void RenderAgentBrowserPanel(AgentInstallLayout? layout, string? layoutError, IReadOnlyList<InstalledAgentRecord> installedAgents)
    {
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

        var agentCards = agentCatalog.Agents
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(agent =>
            {
                var installed = installedSet.Contains(agent.Name);
                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    ToAlias(agent.Name),
                    installed ? "green3" : layout is null ? "grey" : "deepskyblue1",
                    $"[dim]{Escape(agent.Model)}[/]",
                    $"[dim]skills[/] {Escape(SummarizeAliases(agent.Skills))}",
                    installed ? "[green]installed[/]" : layout is null ? "[grey]target unavailable[/]" : "[grey]not installed[/]");
            })
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("agent summary", summary),
            BuildRichShellPanel("agent flow", flow),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "agent directory",
            BuildRichCardGrid(agentCards, maxColumns: 2),
            "deepskyblue1"));
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
            ("Remove", $"{ToolIdentity.SkillsDisplayCommand} remove --all", "Remove all installed skills from the selected target"),
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
        var summary = BuildRichPropertyGrid(
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [dim]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            (resultLabel, resultValue),
            ("skipped", skipped?.Count.ToString() ?? "0"),
            ("adapters", generatedAdapters == 0 ? "[grey]0[/]" : $"[yellow]{generatedAdapters}[/]"));

        var resultCards = rows
            .OrderBy(item => item.Skill.Name, StringComparer.Ordinal)
            .Select(row => (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                ToAlias(row.Skill.Name),
                row.Action switch
                {
                    SkillAction.Removed => "red",
                    SkillAction.Updated => "yellow",
                    SkillAction.Installed => "green3",
                    _ => "grey",
                },
                $"[dim]{Escape(row.Skill.Stack)} / {Escape(row.Skill.Lane)}[/]",
                $"[dim]{Escape(row.FromVersion)}[/] -> [bold]{Escape(row.ToVersion)}[/]",
                FormatSkillActionMarkup(row.Action)))
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("operation summary", summary),
            BuildRichShellPanel("reload hint", new Spectre.Console.Markup(Escape(layout.ReloadHint))),
            gap: 3));
        AnsiConsole.WriteLine();

        if (rows.Count > 0)
        {
            SpectreConsole.Write(BuildRichShellPanel(
                "results",
                BuildRichCardGrid(resultCards, maxColumns: 2),
                "green3"));
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
        var summary = BuildRichPropertyGrid(
            ("platform", Escape(layout.Agent.ToString())),
            ("target", $"[dim]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("mode", Escape(layout.Mode.ToString())),
            (resultLabel, resultValue),
            ("skipped", skipped?.Count.ToString() ?? "0"),
            ("missing", missing?.Count.ToString() ?? "0"));

        var resultCards = agents
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(agent =>
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

                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    ToAlias(agent.Name),
                    missingAgent ? "grey" : skippedAgent ? "grey" : string.Equals(resultLabel, "removed", StringComparison.OrdinalIgnoreCase) ? "red" : "green3",
                    $"[dim]{Escape(agent.Model)}[/]",
                    $"[dim]skills[/] {Escape(SummarizeAliases(agent.Skills))}",
                    status);
            })
            .ToArray();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("agent summary", summary),
            BuildRichShellPanel("reload hint", new Spectre.Console.Markup(Escape(layout.ReloadHint))),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "agent results",
            BuildRichCardGrid(resultCards, maxColumns: 2),
            "green3"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Agent writes stay bound to the selected native target and file format.[/]")));
    }

    private void RenderSkillDetailPanel(SkillEntry skill, InstalledSkillRecord? installed, SkillInstallLayout layout)
    {
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

        var surfaceCards = new Spectre.Console.Rendering.IRenderable[]
        {
            BuildRichDetailCard(
                "NuGet surface",
                "green3",
                skill.Packages.Count == 0 ? "[dim]no concrete packages declared[/]" : $"[dim]packages[/] {Escape(string.Join(", ", skill.Packages.Take(4)))}",
                string.IsNullOrWhiteSpace(skill.PackagePrefix) ? "[dim]no package prefix[/]" : $"[dim]prefix[/] {Escape($"{skill.PackagePrefix}.*")}"),
            BuildRichDetailCard(
                "Upstream links",
                "deepskyblue1",
                string.IsNullOrWhiteSpace(skill.Links.Docs) ? "[grey]docs not declared[/]" : "[green]docs available[/]",
                string.IsNullOrWhiteSpace(skill.Links.Repository) ? "[grey]repository not declared[/]" : "[green]repository available[/]",
                string.IsNullOrWhiteSpace(skill.Links.NuGet) ? "[grey]NuGet link not declared[/]" : "[green]NuGet link available[/]")
        };

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel(ToAlias(skill.Name), summary),
            BuildRichShellPanel("skill surface", BuildRichCardGrid(surfaceCards, maxColumns: 1)),
            gap: 3));
        AnsiConsole.WriteLine();

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel("summary", new Spectre.Console.Markup(Escape(skill.Description))),
            BuildRichShellPanel("preview", new Spectre.Console.Markup(Escape(LoadSkillPreview(skill)))),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Preview comes from the current[/] [green]SKILL.md[/] [dim]payload, so token size and visible content stay aligned.[/]")));
    }

    private void RenderPackageDetailPanel(SkillPackageEntry package)
    {
        var skillIndex = skillCatalog.Skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
        var tokenCount = package.Skills.Sum(skillName => skillIndex.TryGetValue(skillName, out var skill) ? skill.TokenCount : 0);
        var grid = BuildRichPropertyGrid(
            ("bundle", Escape(package.Name)),
            ("area", Escape(CatalogOrganization.ResolveBundleAreaLabel(package))),
            ("type", Escape(package.Kind)),
            ("skills", package.Skills.Count.ToString()),
            ("tokens", FormatTokenCount(tokenCount)),
            ("install", $"[green]{Escape($"dotnet skills install bundle {package.Name}")}[/]"));

        var coverageCards = new Spectre.Console.Rendering.IRenderable[]
        {
            BuildRichDetailCard(
                "Coverage",
                "deepskyblue1",
                $"[dim]collection[/] {Escape(package.Stack)}",
                $"[dim]lane[/] {Escape(package.Lane)}"),
            BuildRichDetailCard(
                "Payload",
                "green3",
                $"[bold]{package.Skills.Count}[/] [dim]included skills[/]",
                $"[dim]tokenizer[/] {Escape(SkillTokenCounter.ModelName)}")
        };

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel(package.Name, grid),
            BuildRichShellPanel("bundle coverage", BuildRichCardGrid(coverageCards, maxColumns: 1)),
            gap: 3));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel("summary", new Spectre.Console.Markup(Escape(package.Description))));
        AnsiConsole.WriteLine();

        var includedSkillCards = package.Skills
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(skillName =>
            {
                if (!skillIndex.TryGetValue(skillName, out var skill))
                {
                    return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                        ToAlias(skillName),
                        "grey",
                        "[grey]missing from current catalog payload[/]");
                }

                return (Spectre.Console.Rendering.IRenderable)BuildRichDetailCard(
                    ToAlias(skillName),
                    "green3",
                    $"[dim]{Escape(skill.Stack)} / {Escape(skill.Lane)}[/]",
                    $"[bold]{FormatTokenCount(skill.TokenCount)}[/] [dim]Tokens[/]");
            })
            .ToArray();

        SpectreConsole.Write(BuildRichShellPanel(
            "included skills",
            BuildRichCardGrid(includedSkillCards, maxColumns: 2),
            "green3"));
        AnsiConsole.WriteLine();
        SpectreConsole.Write(BuildRichShellPanel(
            "status rail",
            new Spectre.Console.Markup("[dim]Focused bundles stay narrow by collection or workflow. Broad category-wide installs are intentionally not exposed here.[/]")));
    }

    private void RenderAgentDetailPanel(AgentEntry agent, AgentInstallLayout? layout, string? layoutError, bool installed)
    {
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

        var surfaceCards = new Spectre.Console.Rendering.IRenderable[]
        {
            BuildRichDetailCard(
                "Native layout",
                layout is null ? "grey" : "deepskyblue1",
                layout is null ? "[grey]target not ready[/]" : $"[dim]mode[/] {Escape(layout.Mode.ToString())}",
                layout is null ? "[grey]no resolved file yet[/]" : $"[dim]agent file[/] {Escape($"{agent.Name}{layout.FileExtension}")}"),
            BuildRichDetailCard(
                "Routing",
                "green3",
                string.IsNullOrWhiteSpace(agent.Tools) ? "[grey]no explicit tool contract[/]" : $"[dim]tools[/] {Escape(agent.Tools)}",
                $"[dim]routed skills[/] {agent.Skills.Count}")
        };

        SpectreConsole.Write(BuildRichTwoColumn(
            BuildRichShellPanel(ToAlias(agent.Name), summary),
            BuildRichShellPanel("agent surface", BuildRichCardGrid(surfaceCards, maxColumns: 1)),
            gap: 3));
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

        var surfaces = new Spectre.Console.Rendering.IRenderable[]
        {
            BuildRichDetailCard(
                "Skills",
                "green3",
                $"[dim]{Escape(skillLayout.Agent.ToString())} / {Escape(skillLayout.Scope.ToString())}[/]",
                $"[dim]{Escape(CompactPath(skillLayout.PrimaryRoot.FullName))}[/]"),
            BuildRichDetailCard(
                "Agents",
                agentLayout is null ? "grey" : "deepskyblue1",
                $"[dim]{Escape(Session.Agent.ToString())} / {Escape(Session.Scope.ToString())}[/]",
                agentLayout is null ? $"[grey]{Escape(agentLayoutError ?? "Unavailable")}[/]" : $"[dim]{Escape(CompactPath(agentLayout.PrimaryRoot.FullName))}[/]")
        };

        SpectreConsole.Write(
            BuildRichStack(
                BuildRichShellPanel("install destination", summary),
                BuildRichShellPanel("resolved surfaces", BuildRichCardGrid(surfaces, maxColumns: 2)),
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
    HomeActionView SelectHomeAction(IReadOnlyList<HomeActionView> choices);

    T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull;

    IReadOnlyList<T>? MultiSelect<T>(
        string title,
        IReadOnlyList<T> choices,
        Func<T, string> formatter,
        IReadOnlyList<T>? initiallySelected = null,
        string? backLabel = null) where T : notnull;

    bool Confirm(string title, bool defaultValue);

    void Pause(string title);
}

internal sealed class CommandCenterInteractivePrompts : IInteractivePrompts
{
    public HomeActionView SelectHomeAction(IReadOnlyList<HomeActionView> choices)
    {
        if (choices.Count == 0)
        {
            throw new InvalidOperationException("No home actions are available.");
        }

        EnsureRichConsoleAvailable();

        AnsiConsole.Write(new Spectre.Console.Markup("[grey]Action key[/]: "));
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            var keyChar = char.ToUpperInvariant(key.KeyChar);
            var selected = choices.FirstOrDefault(choice => char.ToUpperInvariant(choice.HotKey) == keyChar);
            if (selected is not null)
            {
                AnsiConsole.MarkupLine($"[dim]{EscapeMarkup(selected.Label)}[/]");
                return selected;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                var exitAction = choices.FirstOrDefault(choice => choice.Action == HomeAction.Exit);
                if (exitAction is not null)
                {
                    AnsiConsole.MarkupLine("[dim]Exit[/]");
                    return exitAction;
                }
            }
        }
    }

    public T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (choices.Count == 0)
        {
            throw new InvalidOperationException($"No choices are available for {title}.");
        }

        EnsureRichConsoleAvailable();

        var labels = choices.Select(formatter).ToArray();
        var displayLabels = labels.Select(BuildPromptDisplayLabel).ToArray();
        var prompt = new Spectre.Console.SelectionPrompt<string>
        {
            Title = $"[deepskyblue1]{EscapeMarkup(title)}[/]",
            PageSize = Math.Min(Math.Max(displayLabels.Length, 5), 18),
            HighlightStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Aqua),
        };
        prompt.AddChoices(displayLabels);
        var selectedLabel = SpectreConsole.Prompt(prompt);
        var index = Array.FindIndex(displayLabels, label => string.Equals(label, selectedLabel, StringComparison.Ordinal));
        if (index >= 0)
        {
            return choices[index];
        }

        throw new InvalidOperationException($"Could not resolve the selected item for {title}.");
    }

    public IReadOnlyList<T>? MultiSelect<T>(
        string title,
        IReadOnlyList<T> choices,
        Func<T, string> formatter,
        IReadOnlyList<T>? initiallySelected = null,
        string? backLabel = null) where T : notnull
    {
        if (choices.Count == 0)
        {
            return [];
        }

        EnsureRichConsoleAvailable();

        var labels = choices.Select(formatter).ToArray();
        var displayLabels = labels.Select(BuildPromptDisplayLabel).ToArray();
        var displayBackLabel = backLabel is null ? null : BuildPromptDisplayLabel(backLabel);
        var prompt = new Spectre.Console.MultiSelectionPrompt<string>
        {
            Title = $"[deepskyblue1]{EscapeMarkup(title)}[/]",
            InstructionsText = backLabel is null
                ? "[dim](Press <space> to toggle, <enter> to accept)[/]"
                : $"[dim](Press <space> to toggle, <enter> to accept, select {EscapeMarkup(backLabel)} to cancel)[/]",
            PageSize = Math.Min(Math.Max(displayLabels.Length, 5), 18),
            HighlightStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Aqua),
        };
        prompt.NotRequired();
        if (displayBackLabel is not null)
        {
            prompt.AddChoice(displayBackLabel);
        }
        prompt.AddChoices(displayLabels);
        if (initiallySelected is not null)
        {
            foreach (var selectedLabel in initiallySelected.Select(formatter).Distinct(StringComparer.Ordinal))
            {
                prompt.Select(BuildPromptDisplayLabel(selectedLabel));
            }
        }
        var selectedLabels = SpectreConsole.Prompt(prompt);
        if (displayBackLabel is not null && selectedLabels.Contains(displayBackLabel, StringComparer.Ordinal))
        {
            return null;
        }
        var selectedSet = selectedLabels.ToHashSet(StringComparer.Ordinal);
        return displayLabels
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

    internal static string BuildPromptDisplayLabel(string value)
    {
        return EscapeMarkup(value);
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

internal sealed record HomeActionView(char HotKey, HomeAction Action, string Label, string Summary, string Command, string Accent);

internal enum HomeAction
{
    SyncProject,
    ManageBundles,
    InstallSkills,
    Analysis,
    ManageInstalled,
    RemoveAll,
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
