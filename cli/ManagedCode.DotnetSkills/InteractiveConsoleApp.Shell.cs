// -----------------------------------------------------------------------------
// SharpConsoleUI command center — the retained-mode, windowed interactive shell.
//
// This is the default surface for the bare `dotnet skills` invocation. (The
// `agents` / `dotnet-agents` wrappers still dispatch their bare invocation to
// the agents-list path in Program.cs; rerouting them through this command
// center is intentionally a follow-up.) It replaces the prompt-first Spectre
// loop in InteractiveConsoleApp.cs with a NavigationView-driven shell:
//   * each former Show* screen is a NavigationView page rendered with native
//     SharpConsoleUI controls (PanelControl + HorizontalGrid + MarkupControl)
//   * SelectionPrompt/Confirm flows become ListControl activation + modal
//     windows with ButtonControls
//   * mutating actions call the Runtime installers directly and re-render the
//     affected page in place
//
// The classic prompt loop survives as RunClassicShellAsync and is used as a
// fallback when stdin/stdout is redirected (CI, pipes, dumb terminals).
// -----------------------------------------------------------------------------

using ManagedCode.DotnetSkills.Runtime;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;

namespace ManagedCode.DotnetSkills;

internal sealed partial class InteractiveConsoleApp
{
    // One selection treatment for every list mode (keyboard-highlight, mouse-hover, click).
    // The list control otherwise renders three subtly different states: HighlightBackgroundColor
    // for the focused selection, the theme's ListHoverBackgroundColor for mouse hover, and the
    // theme's ListUnfocusedHighlightBackgroundColor when the list does not hold focus. We pin all
    // of them so the bar looks the same regardless of how the row was reached.
    private static readonly Color SelectionBg = new(150, 205, 255);
    private static readonly Color SelectionFg = Color.Black;
    private static readonly Color UnfocusedSelectionBg = new(44, 62, 92);
    private static readonly Color UnfocusedSelectionFg = new(205, 218, 236);
    private static readonly Color ShortcutAccent = new(130, 205, 255);

    // Accent palette — RGB equivalents of the xterm-256 color names this UI was originally
    // designed around. Kept here so the palette has one canonical home.
    private static readonly Color AccentDeepSkyBlue = new(0, 175, 255);    // Spectre "deepskyblue1"
    private static readonly Color AccentTurquoise = new(0, 215, 215);      // Spectre "turquoise2"
    private static readonly Color AccentMediumPurple = new(135, 95, 215);  // Spectre "mediumpurple2"
    private static readonly Color AccentSpringGreen = new(0, 175, 95);     // Spectre "springgreen3"
    private static readonly Color AccentGreen = new(0, 175, 0);            // Spectre "green"
    private static readonly Color AccentYellow = new(215, 175, 0);         // Spectre "yellow"
    private static readonly Color AccentGrey = new(135, 135, 135);         // Spectre "grey"
    private static readonly Color PanelBorderColor = new(70, 88, 116);     // matches the root window border

    // Live shell state for the dynamic status bar.
    private ConsoleWindowSystem? _ws;
    private ScrollablePanelControl? _activePanel;
    private HomeAction? _currentPage;
    private StatusBarControl? _statusBar;
    private StatusBarItem? _clockItem;
    private StatusBarItem? _statusMessage;
    // Top status bar shows session identity (project, scope, agent, version) and updates
    // live when InteractiveSessionState fires its change events.
    private StatusBarControl? _topStatusBar;
    private StatusBarItem? _topProjectItem;
    private StatusBarItem? _topScopeItem;
    private StatusBarItem? _topVersionItem;
    // Unsubscribe handle for session-event subscriptions tied to the current page. Each page
    // build resets this so subscriptions don't leak across page switches.
    private Action? _detachSessionEvents;

    // List-page filter active across rebuilds; cleared on page switch. Bound to the `/` overlay.
    private string _searchFilter = string.Empty;

    // Currently selected collection in the master-detail Collections page (Commit 4).
    private CollectionCatalogView? _selectedCollection;

    // First-click arms the inline two-stage install button on Collections detail (Commit 4);
    // second click commits. Cleared every time the selected collection changes.
    private bool _collectionInstallArmed;

    private static readonly Color[] SectionPalette =
    {
        new(120, 180, 255),
        new(120, 220, 160),
        new(220, 170, 110),
        new(195, 150, 230),
        new(235, 150, 150),
        new(150, 210, 220),
    };

    /// <summary>
    /// Entry point for the bare interactive invocation. Launches the SharpConsoleUI
    /// command center; falls back to the classic prompt loop when there is no real terminal.
    /// </summary>
    public async Task<int> RunAsync()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return await RunClassicShellAsync();
        }

        try
        {
            toolUpdateStatus = await getToolUpdateStatusAsync(cachePath);
            await LoadCatalogsAsync(refreshCatalog: false);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to load the skill catalog: {exception.Message}");
            return 1;
        }

        try
        {
            var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer), BuildTheme());
            // Top/bottom system panels are both replaced by interactive StatusBarControl instances —
            // the top one carries live session identity (project, scope, version), the bottom one
            // carries shortcuts + toast slot.
            windowSystem.PanelStateService.ShowTopPanel = false;
            windowSystem.PanelStateService.ShowBottomPanel = false;

            CreateCommandCenter(windowSystem);
            windowSystem.Run();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Clear();
            ExceptionFormatter.WriteException(exception);
            return 1;
        }
    }

    private void CreateCommandCenter(ConsoleWindowSystem ws)
    {
        _ws = ws;

        var installedCount = SafeCount(GetInstalledSkillCount);
        var outdatedCount = SafeCount(GetOutdatedSkillCount);
        var actions = GetHomeActions(installedCount, outdatedCount)
            .Where(action => action.Action != HomeAction.Exit)
            .ToArray();

        var nav = Controls.NavigationView()
            .WithNavWidth(30)
            .WithPaneHeader("[bold rgb(120,180,255)]  ◆  dotnet skills[/]")
            .WithPaneDisplayMode(NavigationViewDisplayMode.Auto)
            .WithExpandedThreshold(96)
            .WithCompactThreshold(54)
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(new Color(70, 100, 150))
            .WithContentPadding(1, 0, 1, 0)
            .WithContentHeader(true)
            .WithSelectedColors(Color.White, new Color(40, 80, 160))
            .AddItem(new NavigationItem("Home", icon: "◈", subtitle: "Session & telemetry"), panel => BuildHomePage(ws, panel));

        var sectionIndex = 0;
        foreach (var section in actions.GroupBy(action => action.Section))
        {
            var color = SectionPalette[sectionIndex++ % SectionPalette.Length];
            nav = nav.AddHeader(section.Key, color, header =>
            {
                foreach (var action in section)
                {
                    var captured = action;
                    header.AddItem(
                        new NavigationItem(captured.Label, icon: "›", subtitle: captured.Summary) { Tag = captured.Action },
                        panel => BuildActionPage(ws, panel, captured.Action));
                }
            });
        }

        var navView = nav
            .OnSelectedItemChanged((_, e) => RebuildStatusBar(e.NewItem?.Tag as HomeAction?))
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        _topStatusBar = new StatusBarControl(stickyBottom: false)
        {
            StickyPosition = StickyPosition.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BackgroundColor = Color.Transparent,
            SeparatorChar = "·",
            ShortcutLabelSeparator = " ",
        };

        _statusBar = new StatusBarControl(stickyBottom: true)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BackgroundColor = Color.Transparent,
            ShortcutForegroundColor = ShortcutAccent,
            SeparatorChar = "·",
            ShortcutLabelSeparator = " ",
        };

        // Rule separators above and below the content area (cxpost/cxfiles framing pattern).
        var topRule = Controls.Rule();
        var bottomRule = Controls.Rule();
        topRule.StickyPosition = StickyPosition.Top;
        bottomRule.StickyPosition = StickyPosition.Bottom;

        // Background gradient (cxpost / cxfiles house style — cool dark blue top to near-black bottom).
        var backgroundGradient = ColorGradient.FromColors(new Color(25, 32, 52), new Color(7, 7, 13));

        new WindowBuilder(ws)
            .WithTitle("dotnet skills — command center")
            .HideTitle()
            .Maximized()
            .Movable(false)
            .Resizable(false)
            .HideTitleButtons()
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(new Color(70, 88, 116))
            .WithBackgroundGradient(backgroundGradient, GradientDirection.Vertical)
            .WithAsyncWindowThread(ClockLoopAsync)
            .OnKeyPressed((_, e) => HandleGlobalKey(e))
            .OnClosed((_, _) => ws.Shutdown(0))
            .AddControl(_topStatusBar)
            .AddControl(topRule)
            .AddControl(navView)
            .AddControl(bottomRule)
            .AddControl(_statusBar)
            .BuildAndShow();

        RebuildStatusBar(null);
        RebuildTopStatusBar();
    }

    /// <summary>
    /// Repopulates the top status bar with current session identity. Called on initial build
    /// and from session-change event subscriptions in BuildActionPage/BuildHomePage.
    /// </summary>
    private void RebuildTopStatusBar()
    {
        var bar = _topStatusBar;
        if (bar is null) return;

        bar.BatchUpdate(() =>
        {
            bar.ClearAll();
            _topProjectItem = bar.AddLeftText($"[bold rgb(120,180,255)]◆[/] [bold]dotnet skills[/] [grey50]v{Escape(ToolVersionInfo.CurrentVersion)}[/]");
            bar.AddLeftSeparator();
            bar.AddLeftText($"[grey50]project[/] {Escape(CompactPath(Session.ProjectDirectory ?? Environment.CurrentDirectory))}");
            bar.AddLeftSeparator();
            _topScopeItem = bar.AddLeftText($"[grey50]scope[/] {Escape(Session.Scope.ToString())} [grey50]·[/] [grey50]platform[/] {Escape(Session.Agent.ToString())}");

            _topVersionItem = bar.AddRightText($"[grey50]catalog[/] {Escape(skillCatalog.CatalogVersion)} [grey50]·[/] {skillCatalog.Skills.Count} skills");
        });
    }

    /// <summary>
    /// Navigates to a HomeAction page without going through the NavigationView rail — used by
    /// the clickable Home metric cards and the command palette. The rail's visual selection
    /// state won't follow, but the content panel rebuilds and status bars update.
    /// </summary>
    private void NavigateTo(HomeAction action)
    {
        if (_ws is null || _activePanel is null) return;
        BuildActionPage(_ws, _activePanel, action);
        RebuildStatusBar(action);
    }

    /// <summary>
    /// Replaces any prior session-event subscriptions with a fresh one bound to the active page,
    /// so flipping Session.Scope/Agent/Project from anywhere refreshes the open page in place.
    /// Must be called at the top of every page builder.
    /// </summary>
    private void AttachSessionEvents()
    {
        _detachSessionEvents?.Invoke();
        Action handler = () =>
        {
            RebuildTopStatusBar();
            RebuildActivePage();
        };
        Session.AgentChanged += handler;
        Session.ScopeChanged += handler;
        Session.ProjectChanged += handler;
        Session.SnapshotChanged += handler;
        _detachSessionEvents = () =>
        {
            Session.AgentChanged -= handler;
            Session.ScopeChanged -= handler;
            Session.ProjectChanged -= handler;
            Session.SnapshotChanged -= handler;
        };
    }

    private void HandleGlobalKey(KeyPressedEventArgs e)
    {
        var key = e.KeyInfo;

        // Esc clears an active search filter first, then ends the session.
        if (key.Key == ConsoleKey.Escape)
        {
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                _searchFilter = string.Empty;
                RebuildActivePage();
                e.Handled = true;
                return;
            }
            _ws?.Shutdown(0);
            e.Handled = true;
            return;
        }

        // Plain `/` opens the search overlay on any list-bearing page (no modifier required).
        if ((key.Modifiers & ConsoleModifiers.Control) == 0)
        {
            if (key.KeyChar == '/' && IsListBearingPage(_currentPage))
            {
                ShowSearchOverlay();
                e.Handled = true;
            }
            return;
        }

        switch (key.Key)
        {
            case ConsoleKey.R:
                RefreshCatalogFromUi();
                e.Handled = true;
                break;
            case ConsoleKey.U when _currentPage == HomeAction.ManageInstalled:
                UpdateAllOutdatedFromUi();
                e.Handled = true;
                break;
            case ConsoleKey.I when _currentPage == HomeAction.SyncProject:
                InstallAllRecommendedFromUi();
                e.Handled = true;
                break;
            case ConsoleKey.Delete when _currentPage == HomeAction.ManageInstalled:
                RemoveAllFromUi();
                e.Handled = true;
                break;
            case ConsoleKey.P:
                if (_ws is not null) ShowCommandPalette(_ws);
                e.Handled = true;
                break;
        }
    }

    private static bool IsListBearingPage(HomeAction? page) => page is
        HomeAction.BrowseSkills or
        HomeAction.ManageInstalled or
        HomeAction.BrowseCollections or
        HomeAction.BrowseBundles or
        HomeAction.BrowsePackages or
        HomeAction.BrowseAgents;

    // -------------------------------------------------------------------------
    // Page dispatch
    // -------------------------------------------------------------------------

    private void BuildActionPage(ConsoleWindowSystem ws, ScrollablePanelControl panel, HomeAction action)
    {
        // Page-switch clears transient filters (search + Collections detail selection) so each
        // page lands in a clean state. Use NavigateTo if you need to preserve filter context.
        if (_currentPage != action)
        {
            _searchFilter = string.Empty;
            _selectedCollection = null;
            _collectionInstallArmed = false;
        }
        _activePanel = panel;
        _currentPage = action;
        AttachSessionEvents();
        ClearStickyStatus();
        switch (action)
        {
            case HomeAction.BrowseSkills: BuildSkillBrowserPage(ws, panel); break;
            case HomeAction.ManageInstalled: BuildInstalledPage(ws, panel); break;
            case HomeAction.BrowseCollections: BuildCollectionsPage(ws, panel); break;
            case HomeAction.BrowseBundles: BuildBundlesPage(ws, panel, primaryOnly: true); break;
            case HomeAction.BrowsePackages: BuildPackagesPage(ws, panel); break;
            case HomeAction.BrowseAgents: BuildAgentsPage(ws, panel); break;
            case HomeAction.SyncProject: BuildProjectPage(ws, panel); break;
            case HomeAction.Analysis: BuildAnalysisPage(ws, panel); break;
            case HomeAction.RemoveAll: BuildRemoveAllPage(ws, panel); break;
            case HomeAction.UpdateAll: BuildUpdateAllPage(ws, panel); break;
            case HomeAction.Workspace: BuildSettingsPage(ws, panel); break;
            case HomeAction.About: BuildAboutPage(panel); break;
            default:
                panel.ClearContents();
                panel.AddControl(BuildNotePanel(action.ToString(), "[grey50]Not available in this surface.[/]", AccentGrey));
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Home
    // -------------------------------------------------------------------------

    private void BuildHomePage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        if (_currentPage != null)
        {
            _searchFilter = string.Empty;
            _selectedCollection = null;
            _collectionInstallArmed = false;
        }
        _activePanel = panel;
        _currentPage = null;
        AttachSessionEvents();
        ClearStickyStatus();
        panel.ClearContents();

        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var installed = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>());
        var outdated = installed.Count(record => !record.IsCurrent);

        panel.AddControl(BuildPropertyPanel("session", AccentDeepSkyBlue,
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("platform", Escape(Session.Agent.ToString())),
            ("scope", Escape(Session.Scope.ToString())),
            ("project", Escape(CompactPath(Session.ProjectDirectory ?? Environment.CurrentDirectory))),
            ("target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]")));

        // catalog telemetry — five native metric cards laid out by HorizontalGrid (responsive flex).
        var installedAccent = installed.Count > 0 ? AccentGreen : AccentGrey;
        var outdatedAccent = outdated == 0 ? AccentGreen : AccentYellow;
        // Cards are clickable navigation targets — click "outdated" to jump to Installed, etc.
        var telemetryGrid = Controls.HorizontalGrid()
            .Column(col => col.Flex(1).Add(BuildMetricCard("skills", skillCatalog.Skills.Count.ToString(), "in catalog", AccentDeepSkyBlue, () => NavigateTo(HomeAction.BrowseSkills))))
            .Column(col => col.Flex(1).Add(BuildMetricCard("bundles", GetPrimaryBundles().Count.ToString(), "focused", AccentTurquoise, () => NavigateTo(HomeAction.BrowseBundles))))
            .Column(col => col.Flex(1).Add(BuildMetricCard("installed", $"{installed.Count}/{skillCatalog.Skills.Count}", "in current target", installedAccent, () => NavigateTo(HomeAction.ManageInstalled))))
            .Column(col => col.Flex(1).Add(BuildMetricCard("outdated", outdated.ToString(), outdated == 0 ? "all current" : "need update", outdatedAccent, () => NavigateTo(HomeAction.ManageInstalled))))
            .Column(col => col.Flex(1).Add(BuildMetricCard("agents", agentCatalog.Agents.Count.ToString(), "orchestration", AccentMediumPurple, () => NavigateTo(HomeAction.BrowseAgents))))
            .Build();
        panel.AddControl(telemetryGrid);

        if (toolUpdateStatus?.HasUpdate == true)
        {
            var freshness = toolUpdateStatus.CheckedAt is null
                ? "[grey50]latest release detected[/]"
                : toolUpdateStatus.UsedCachedValue
                    ? $"[grey50]cached[/] [grey]{Escape(toolUpdateStatus.CheckedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))}[/]"
                    : $"[grey50]checked[/] [grey]{Escape(toolUpdateStatus.CheckedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))}[/]";
            panel.AddControl(BuildBulletPanel("tool update", AccentYellow,
                "[bold yellow]New dotnet-skills version available[/]",
                $"[grey50]current[/] [grey]{Escape(toolUpdateStatus.CurrentVersion)}[/] [grey50]-> latest[/] [green]{Escape(toolUpdateStatus.LatestVersion ?? "?")}[/]",
                $"[green]{Escape(GlobalToolUpdateCommand)}[/]",
                $"[grey50]local tool manifest[/] [green]{Escape(LocalToolUpdateCommand)}[/]",
                freshness));
        }

        panel.AddControl(BuildBulletPanel("quick start", AccentDeepSkyBlue,
            "[grey50]Use the rail on the left to browse and install.[/]",
            "[grey]Skills[/] [grey50]browse and install individual catalog skills[/]",
            "[grey]Installed[/] [grey50]update or remove what is already installed[/]",
            "[grey]Project[/] [grey50]scan the current solution and install recommended skills[/]",
            "[grey]Agents[/] [grey50]install orchestration agents into native agent directories[/]"));
    }

    // -------------------------------------------------------------------------
    // Native control helpers — every page and modal renders through these.
    // -------------------------------------------------------------------------

    /// <summary>
    /// A native PanelControl with rounded border, themed header, and accent border color —
    /// the visual equivalent of BuildRichShellPanel but drawn directly into the cell buffer
    /// so its border aligns with the surrounding window chrome.
    /// </summary>
    private static PanelControl BuildSectionPanel(string title, string body, Color accent) => Controls.Panel()
        .WithHeader($"[bold]{Escape(title)}[/]")
        .WithBorderStyle(BorderStyle.Rounded)
        .WithBorderColor(accent)
        .WithPadding(1, 0, 1, 0)
        .WithContent(body)
        .WithAlignment(HorizontalAlignment.Stretch)
        .Build();

    /// <summary>
    /// A native metric card: three stacked lines (title accent, value bold, detail grey) inside
    /// a rounded PanelControl with an accent border. Used in HorizontalGrid columns.
    /// </summary>
    /// <param name="onClick">Optional click handler — when non-null, the card becomes a
    /// navigation target via its MouseClick event.</param>
    private static PanelControl BuildMetricCard(string title, string value, string detail, Color accent, Action? onClick = null)
    {
        // Multi-line markup body — PanelControl splits on \n and wraps each line.
        var body = string.Join("\n",
            $"[bold]{Escape(value)}[/]",
            $"[grey50]{Escape(detail)}[/]");
        var card = Controls.Panel()
            .WithHeader($"[bold]{Escape(title)}[/]")
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(accent)
            .WithPadding(1, 0, 1, 0)
            .WithContent(body)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        if (onClick is not null)
        {
            card.MouseClick += (_, _) => onClick();
        }
        return card;
    }

    /// <summary>
    /// Formats one row of a property grid as " label  value" — fixed-width left column so values
    /// line up when stacked. Equivalent to BuildRichPropertyGrid's two-column grid but rendered
    /// inline as markup text (cheaper, and PanelControl wraps the value if it overflows).
    /// </summary>
    private static string FormatRow(string label, string value)
    {
        const int labelWidth = 12;
        var padded = label.Length >= labelWidth ? label : label + new string(' ', labelWidth - label.Length);
        return $"[grey50]{Escape(padded)}[/] {value}";
    }

    /// <summary>
    /// A native section panel whose body is a property grid built from label/value rows.
    /// The native equivalent of BuildRichShellPanel(BuildRichPropertyGrid(...)).
    /// </summary>
    private static PanelControl BuildPropertyPanel(string title, Color accent, params (string Label, string Value)[] rows)
    {
        var body = string.Join("\n", rows.Select(r => FormatRow(r.Label, r.Value)));
        return BuildSectionPanel(title, body, accent);
    }

    /// <summary>
    /// A native section panel containing a single markup line — used for empty-state notes and
    /// short status messages.
    /// </summary>
    private static PanelControl BuildNotePanel(string title, string markup, Color accent)
        => BuildSectionPanel(title, markup, accent);

    /// <summary>
    /// A native section panel whose body is a vertical stack of markup lines — used for
    /// "quick start", "surface map", and similar bullet-list cards. Lines are joined with \n
    /// so PanelControl wraps each independently.
    /// </summary>
    private static PanelControl BuildBulletPanel(string title, Color accent, params string[] lines)
    {
        var body = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        return BuildSectionPanel(title, body, accent);
    }

    /// <summary>
    /// Lays out a sequence of cards in a responsive HorizontalGrid with 1, 2, or 3 columns based
    /// on the current console width — the native equivalent of BuildRichCardGrid(maxColumns).
    /// Empty columns at the end of the last row are padded with blank MarkupControls so the cards
    /// keep equal width.
    /// </summary>
    private static IWindowControl BuildCardGrid(IReadOnlyList<PanelControl> cards, int maxColumns = 3)
    {
        if (cards.Count == 0)
        {
            return new MarkupControl(new List<string> { "[grey50]No items available.[/]" });
        }

        var consoleWidth = SafeConsole(() => Console.WindowWidth, 120);
        var columnCount = consoleWidth >= 190 ? Math.Min(maxColumns, 3)
                        : consoleWidth >= 130 ? Math.Min(maxColumns, 2)
                                              : 1;
        columnCount = Math.Max(1, Math.Min(columnCount, cards.Count));

        var grid = Controls.HorizontalGrid();
        for (var i = 0; i < columnCount; i++)
        {
            var columnIndex = i;
            grid = grid.Column(col =>
            {
                col.Flex(1);
                for (var cardIndex = columnIndex; cardIndex < cards.Count; cardIndex += columnCount)
                {
                    col.Add(cards[cardIndex]);
                }
            });
        }

        return grid.Build();
    }

    // -------------------------------------------------------------------------
    // Skill browser
    // -------------------------------------------------------------------------

    private void BuildSkillBrowserPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var installed = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>());
        var available = skillCatalog.Skills
            .Where(skill => installed.All(record => !string.Equals(record.Skill.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(skill => CatalogOrganization.GetStackRank(skill.Stack))
            .ThenBy(skill => skill.Stack, StringComparer.Ordinal)
            .ThenBy(skill => skill.Name, StringComparer.Ordinal)
            .ToArray();

        var filtered = available.Where(s => MatchesFilter(s.Name, s.Stack, s.Lane)).ToArray();

        panel.AddControl(BuildPropertyPanel("skill browser", AccentTurquoise,
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("available", $"{filtered.Length}/{available.Length}"),
            ("installed", $"{installed.Count}/{skillCatalog.Skills.Count}")));
        AddSearchChip(panel);

        if (available.Length == 0)
        {
            panel.AddControl(BuildNotePanel("available", "[grey50]Every catalog skill is already installed in this target.[/]", AccentDeepSkyBlue));
            return;
        }
        if (filtered.Length == 0)
        {
            panel.AddControl(BuildNotePanel("available", $"[grey50]No skills match “{Escape(_searchFilter)}”.[/]", AccentYellow));
            return;
        }

        var list = StyledList("Available skills (Enter for details)")
            .MaxVisibleItems(16)
            .WithScrollbarVisibility(ScrollbarVisibility.Auto);
        foreach (var skill in filtered)
        {
            // ListControl parses item text as markup; BuildSkillChoiceLabel produces plain
            // text containing bracketed stack/lane like "[.NET Foundations / ...]". Escape so
            // brackets are not interpreted as Spectre markup tags.
            list.AddItem(Escape(BuildSkillChoiceLabel(skill, installed)), skill);
        }
        list.OnItemActivated((_, item) =>
        {
            if (item.Tag is SkillEntry skill)
            {
                ShowSkillDetailModal(ws, panel, skill);
            }
        });
        panel.AddControl(list.Build());
    }

    private void ShowSkillDetailModal(ConsoleWindowSystem ws, ScrollablePanelControl owner, SkillEntry skill)
    {
        var detail = new IWindowControl[]
        {
            BuildPropertyPanel(ToAlias(skill.Name), AccentTurquoise,
                ("skill", Escape(skill.Name)),
                ("collection", Escape(skill.Stack)),
                ("lane", Escape(skill.Lane)),
                ("version", Escape(skill.Version)),
                ("tokens", FormatTokenCount(skill.TokenCount))),
            BuildNotePanel("summary", Escape(skill.Description), AccentDeepSkyBlue),
            BuildNotePanel("preview", Escape(LoadSkillPreview(skill)), AccentGrey),
        };

        ShowModalNative(ws, $"Skill · {ToAlias(skill.Name)}", detail,
            ("Install into current target", () =>
            {
                var summary = SafeGet(() => new SkillInstaller(skillCatalog).Install(new[] { skill }, ResolveSkillLayout(), force: false), default(SkillInstallSummary));
                if (summary is null)
                    Toast($"Install failed for {ToAlias(skill.Name)}", NotificationSeverity.Danger);
                else
                    Toast($"{ToAlias(skill.Name)}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped", NotificationSeverity.Success);
                BuildSkillBrowserPage(ws, owner);
            }),
            ("Force reinstall", () =>
            {
                var summary = SafeGet(() => new SkillInstaller(skillCatalog).Install(new[] { skill }, ResolveSkillLayout(), force: true), default(SkillInstallSummary));
                if (summary is null)
                    Toast($"Install failed for {ToAlias(skill.Name)}", NotificationSeverity.Danger);
                else
                    Toast($"{ToAlias(skill.Name)}: reinstalled ({summary.InstalledCount} written)", NotificationSeverity.Success);
                BuildSkillBrowserPage(ws, owner);
            }));
    }

    // -------------------------------------------------------------------------
    // Installed skills
    // -------------------------------------------------------------------------

    private void BuildInstalledPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var installed = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>())
            .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
            .ToArray();
        var outdated = installed.Where(record => !record.IsCurrent).ToArray();

        var filtered = installed.Where(r => MatchesFilter(r.Skill.Name, r.Skill.Stack, r.Skill.Lane)).ToArray();

        panel.AddControl(BuildPropertyPanel("installed skills", AccentGreen,
            ("target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("installed", string.IsNullOrEmpty(_searchFilter) ? installed.Length.ToString() : $"{filtered.Length}/{installed.Length}"),
            ("outdated", outdated.Length == 0 ? "[green]0[/]" : $"[yellow]{outdated.Length}[/]"),
            ("tokens", FormatTokenCount(installed.Sum(record => record.Skill.TokenCount)))));
        AddSearchChip(panel);

        if (installed.Length == 0)
        {
            panel.AddControl(BuildNotePanel("installed", "[grey50]No catalog skills are installed in this target yet. Visit the Skills page to add some.[/]", AccentDeepSkyBlue));
            return;
        }
        if (filtered.Length == 0)
        {
            panel.AddControl(BuildNotePanel("installed", $"[grey50]No installed skills match “{Escape(_searchFilter)}”.[/]", AccentYellow));
            return;
        }

        // Real sortable TableControl — columns can be sorted by clicking the header. Per-row
        // foreground color flags outdated rows yellow without needing markup escaping per cell.
        var table = Controls.Table()
            .WithTitle("Installed skills (Enter for details)")
            .AddColumn("Status", TextJustification.Center, width: 8)
            .AddColumn("Skill")
            .AddColumn("Collection")
            .AddColumn("Lane")
            .AddColumn("Installed", TextJustification.Right)
            .AddColumn("Latest", TextJustification.Right)
            .AddColumn("Tokens", TextJustification.Right)
            .WithSorting()
            .Rounded()
            .WithBorderColor(AccentGreen);
        foreach (var record in filtered)
        {
            var row = new TableRow(
                record.IsCurrent ? "✓ current" : "↻ update",
                ToAlias(record.Skill.Name),
                record.Skill.Stack,
                record.Skill.Lane,
                record.InstalledVersion,
                record.Skill.Version,
                FormatTokenCount(record.Skill.TokenCount))
            {
                Tag = record,
                ForegroundColor = record.IsCurrent ? null : AccentYellow,
            };
            table.AddRow(row);
        }
        // RowActivated fires on Enter or double-click; index is into the filtered array because
        // we appended rows in the same order.
        table.OnRowActivated((_, idx) =>
        {
            if (idx >= 0 && idx < filtered.Length)
            {
                ShowInstalledSkillModal(ws, panel, filtered[idx]);
            }
        });
        panel.AddControl(table.Build());

        if (outdated.Length > 0)
        {
            panel.AddControl(Controls.Button($"Update all {outdated.Length} outdated skill(s)")
                .OnClick((_, _) =>
                {
                    var summaryText = UpdateSkillRecords(outdated);
                    Toast(summaryText, summaryText.Contains("failed", StringComparison.OrdinalIgnoreCase) ? NotificationSeverity.Danger : NotificationSeverity.Success);
                    BuildInstalledPage(ws, panel);
                }).Build());
        }

        panel.AddControl(Controls.Button($"Remove all {installed.Length} installed skill(s)")
            .OnClick((_, _) => ConfirmModal(ws, "Remove all installed skills?",
                $"This removes every catalog skill from {layout.PrimaryRoot.FullName}.",
                () =>
                {
                    var summary = SafeGet(() => new SkillInstaller(skillCatalog).Remove(installed.Select(r => r.Skill).ToArray(), layout), default(SkillRemoveSummary));
                    ToastResult(summary, "Remove failed", summary is null ? string.Empty : $"Removed {summary.RemovedCount} skill(s)");
                    BuildInstalledPage(ws, panel);
                })).Build());
    }

    private void ShowInstalledSkillModal(ConsoleWindowSystem ws, ScrollablePanelControl owner, InstalledSkillRecord record)
    {
        var detail = new IWindowControl[]
        {
            BuildPropertyPanel(ToAlias(record.Skill.Name), AccentGreen,
                ("skill", Escape(record.Skill.Name)),
                ("collection", Escape($"{record.Skill.Stack} / {record.Skill.Lane}")),
                ("installed", Escape(record.InstalledVersion)),
                ("latest", Escape(record.Skill.Version)),
                ("status", record.IsCurrent ? "[green]✓ current[/]" : "[yellow]↻ update available[/]"),
                ("tokens", FormatTokenCount(record.Skill.TokenCount))),
            BuildNotePanel("summary", Escape(record.Skill.Description), AccentDeepSkyBlue),
        };

        var buttons = new List<(string, Action)>();
        if (!record.IsCurrent)
        {
            buttons.Add(($"Update to {record.Skill.Version}", () =>
            {
                var msg = UpdateSkillRecords(new[] { record });
                Toast(msg, msg.Contains("failed", StringComparison.OrdinalIgnoreCase) ? NotificationSeverity.Danger : NotificationSeverity.Success);
                BuildInstalledPage(ws, owner);
            }));
        }
        buttons.Add(("Reinstall (force)", () =>
        {
            var summary = SafeGet(() => new SkillInstaller(skillCatalog).Install(new[] { record.Skill }, ResolveSkillLayout(), force: true), default(SkillInstallSummary));
            ToastResult(summary, "Reinstall failed", $"{ToAlias(record.Skill.Name)}: reinstalled");
            BuildInstalledPage(ws, owner);
        }));
        buttons.Add(("Remove", () => ConfirmModal(ws, $"Remove {ToAlias(record.Skill.Name)}?", $"Deletes the skill directory from {ResolveSkillLayout().PrimaryRoot.FullName}.", () =>
        {
            var summary = SafeGet(() => new SkillInstaller(skillCatalog).Remove(new[] { record.Skill }, ResolveSkillLayout()), default(SkillRemoveSummary));
            ToastResult(summary, "Remove failed", $"Removed {ToAlias(record.Skill.Name)}");
            BuildInstalledPage(ws, owner);
        })));

        ShowModalNative(ws, $"Installed · {ToAlias(record.Skill.Name)}", detail, buttons.ToArray());
    }

    private string UpdateSkillRecords(IReadOnlyList<InstalledSkillRecord> records)
    {
        var layout = ResolveSkillLayout();
        var skills = records.Select(record => record.Skill).ToArray();
        var summary = SafeGet(() => new SkillInstaller(skillCatalog).Install(skills, layout, force: true), default(SkillInstallSummary));
        return summary is null ? "Update failed" : $"Updated {summary.InstalledCount} skill(s)";
    }

    // -------------------------------------------------------------------------
    // Collections
    // -------------------------------------------------------------------------

    private void BuildCollectionsPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var installed = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>());
        var views = BuildCollectionViews(installed)
            .OrderBy(view => CatalogOrganization.GetStackRank(view.Collection))
            .ThenBy(view => view.Collection, StringComparer.Ordinal)
            .ToArray();
        var filtered = views.Where(v => MatchesFilter(v.Collection)).ToArray();

        panel.AddControl(BuildPropertyPanel("collection browser", AccentDeepSkyBlue,
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("collections", string.IsNullOrEmpty(_searchFilter) ? views.Length.ToString() : $"{filtered.Length}/{views.Length}"),
            ("skills", skillCatalog.Skills.Count.ToString()),
            ("installed", $"{installed.Count}/{skillCatalog.Skills.Count}")));
        AddSearchChip(panel);

        if (views.Length == 0)
        {
            panel.AddControl(BuildNotePanel("collections", "[grey50]No collections in this catalog version.[/]", AccentDeepSkyBlue));
            return;
        }
        if (filtered.Length == 0)
        {
            panel.AddControl(BuildNotePanel("collections", $"[grey50]No collections match “{Escape(_searchFilter)}”.[/]", AccentYellow));
            return;
        }

        // Master-detail layout. Left column lists collections; right column shows the detail of
        // _selectedCollection. Clicking a left-list row updates only the right pane in place —
        // no modal, no full-page rebuild. The right pane is a ScrollablePanel so the detail can
        // grow with the collection's lane list.
        if (_selectedCollection is null
            || !filtered.Any(v => string.Equals(v.Collection, _selectedCollection.Collection, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedCollection = filtered[0];
            _collectionInstallArmed = false;
        }

        // Build the detail pane as a standalone ScrollablePanelControl so we can update it
        // independently of the left list when the user changes selection.
        var rightPane = new ScrollablePanelControl
        {
            ShowScrollbar = true,
            VerticalScrollMode = ScrollMode.Scroll,
            EnableMouseWheel = true,
        };

        var grid = Controls.HorizontalGrid()
            .Column(col =>
            {
                col.Flex(1);
                var list = StyledList("Collections")
                    .MaxVisibleItems(20)
                    .WithScrollbarVisibility(ScrollbarVisibility.Auto);
                foreach (var view in filtered)
                {
                    list.AddItem(Escape(BuildCollectionChoiceLabel(view)), view);
                }
                list.OnItemActivated((_, item) =>
                {
                    if (item.Tag is CollectionCatalogView v)
                    {
                        _selectedCollection = v;
                        _collectionInstallArmed = false;
                        BuildCollectionDetail(rightPane, v);
                    }
                });
                col.Add(list.Build());
            })
            .Column(col =>
            {
                col.Flex(2).Add(rightPane);
            })
            .Build();

        panel.AddControl(grid);
        BuildCollectionDetail(rightPane, _selectedCollection!);
    }

    /// <summary>
    /// Renders the right pane of the Collections master-detail view: stats, lanes, and an inline
    /// two-stage install button (first click arms, second commits — satisfies AGENTS.md's
    /// "install overview before confirmation" rule without a modal).
    /// </summary>
    private void BuildCollectionDetail(ScrollablePanelControl pane, CollectionCatalogView view)
    {
        pane.ClearContents();
        pane.AddControl(BuildPropertyPanel(view.Collection, AccentDeepSkyBlue,
            ("collection", Escape(view.Collection)),
            ("lanes", view.Lanes.Count.ToString()),
            ("skills", $"{view.InstalledCount}/{view.SkillCount}"),
            ("tokens", FormatTokenCount(view.TokenCount))));

        if (view.Lanes.Count > 0)
        {
            pane.AddControl(BuildBulletPanel("lanes", AccentTurquoise,
                view.Lanes.Select(lane => $"[grey50]·[/] [grey]{Escape(lane.Lane)}[/] [grey50]({lane.InstalledCount}/{lane.Skills.Count} skills, {FormatTokenCount(lane.TokenCount)} tokens)[/]").ToArray()));
        }

        var armed = _collectionInstallArmed;
        var label = armed
            ? $"Click again to install all {view.SkillCount} skill(s)"
            : $"Install collection ({view.SkillCount} skill(s))";
        pane.AddControl(Controls.Button(label)
            .OnClick((_, _) =>
            {
                if (!_collectionInstallArmed)
                {
                    _collectionInstallArmed = true;
                    Toast($"Click again to confirm installing {view.SkillCount} skill(s)", NotificationSeverity.Warning);
                    BuildCollectionDetail(pane, view);
                    return;
                }
                var skills = SafeGet(() => new SkillInstaller(skillCatalog).SelectSkillsFromCollections(new[] { view.Collection }), Array.Empty<SkillEntry>());
                var summary = skills.Count == 0 ? null : SafeGet(() => new SkillInstaller(skillCatalog).Install(skills, ResolveSkillLayout(), force: false), default(SkillInstallSummary));
                ToastResult(summary, $"Could not install collection {view.Collection}", summary is null ? string.Empty : $"{view.Collection}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped");
                _collectionInstallArmed = false;
                if (_ws is not null && _activePanel is not null) BuildCollectionsPage(_ws, _activePanel);
            }).Build());
    }

    // -------------------------------------------------------------------------
    // Bundles / packages
    // -------------------------------------------------------------------------

    private void BuildBundlesPage(ConsoleWindowSystem ws, ScrollablePanelControl panel, bool primaryOnly)
    {
        panel.ClearContents();

        var packages = (primaryOnly
                ? GetPrimaryBundles()
                : skillCatalog.Packages.OrderBy(p => p.Name, StringComparer.Ordinal).ToArray())
            .ToArray();
        var title = primaryOnly ? "focused bundles" : "catalog packages";
        var skillTokens = skillCatalog.Skills.ToDictionary(skill => skill.Name, skill => skill.TokenCount, StringComparer.OrdinalIgnoreCase);

        var filtered = packages.Where(p => MatchesFilter(p.Name, p.Title)).ToArray();

        panel.AddControl(BuildPropertyPanel(title, AccentDeepSkyBlue,
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            (primaryOnly ? "bundles" : "packages", string.IsNullOrEmpty(_searchFilter) ? packages.Length.ToString() : $"{filtered.Length}/{packages.Length}"),
            ("skills covered", skillCatalog.Skills.Count.ToString())));
        AddSearchChip(panel);

        if (packages.Length == 0)
        {
            panel.AddControl(BuildNotePanel(title, "[grey50]Nothing available in this catalog version.[/]", AccentDeepSkyBlue));
            return;
        }
        if (filtered.Length == 0)
        {
            panel.AddControl(BuildNotePanel(title, $"[grey50]No bundles match “{Escape(_searchFilter)}”.[/]", AccentYellow));
            return;
        }

        var list = StyledList($"{(primaryOnly ? "Bundles" : "Packages")} (Enter for details)")
            .MaxVisibleItems(16)
            .WithScrollbarVisibility(ScrollbarVisibility.Auto);
        foreach (var package in filtered)
        {
            var tokenCount = package.Skills.Sum(name => skillTokens.TryGetValue(name, out var value) ? value : 0);
            list.AddItem($"{Escape(package.Name)}  [dim]({package.Skills.Count} skills, {FormatTokenCount(tokenCount)} tokens)[/]", package);
        }
        list.OnItemActivated((_, item) =>
        {
            if (item.Tag is SkillPackageEntry package)
            {
                ShowBundleModal(ws, panel, package, primaryOnly);
            }
        });
        panel.AddControl(list.Build());
    }

    private void ShowBundleModal(ConsoleWindowSystem ws, ScrollablePanelControl owner, SkillPackageEntry package, bool primaryOnly)
    {
        var detail = new IWindowControl[]
        {
            BuildPropertyPanel(package.Name, AccentTurquoise,
                ("package", Escape(package.Name)),
                ("title", Escape(package.Title)),
                ("skills", package.Skills.Count.ToString()),
                ("includes", Escape(string.Join(", ", package.Skills.Take(10).Select(ToAlias))))),
            BuildNotePanel("summary", Escape(package.Description), AccentDeepSkyBlue),
        };

        ShowModalNative(ws, $"Bundle · {package.Name}", detail,
            ("Install bundle into current target", () =>
            {
                var skills = SafeGet(() => new SkillInstaller(skillCatalog).SelectSkillsFromPackages(new[] { package.Name }), Array.Empty<SkillEntry>());
                var summary = skills.Count == 0 ? null : SafeGet(() => new SkillInstaller(skillCatalog).Install(skills, ResolveSkillLayout(), force: false), default(SkillInstallSummary));
                ToastResult(summary, $"Could not install bundle {package.Name}", summary is null ? string.Empty : $"{package.Name}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped");
                BuildBundlesPage(ws, owner, primaryOnly);
            }));
    }

    // -------------------------------------------------------------------------
    // Packages — NuGet ids / prefixes → catalog skills
    // -------------------------------------------------------------------------

    private void BuildPackagesPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var signals = SafeGet(BuildPackageSignals, Array.Empty<PackageSignalView>());
        var filtered = signals.Where(s => MatchesFilter(s.Signal, s.Skill.Name, s.Skill.Stack, s.Skill.Lane)).ToArray();

        panel.AddControl(BuildPropertyPanel("package signals", AccentTurquoise,
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("signals", string.IsNullOrEmpty(_searchFilter) ? signals.Count.ToString() : $"{filtered.Length}/{signals.Count}"),
            ("skills covered", signals.Select(s => s.Skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString())));
        AddSearchChip(panel);

        if (signals.Count == 0)
        {
            panel.AddControl(BuildNotePanel("packages", "[grey50]No NuGet package or prefix signals are present in this catalog version.[/]", AccentDeepSkyBlue));
            return;
        }
        if (filtered.Length == 0)
        {
            panel.AddControl(BuildNotePanel("packages", $"[grey50]No signals match “{Escape(_searchFilter)}”.[/]", AccentYellow));
            return;
        }

        var list = StyledList("Package signals (Enter to inspect linked skill)")
            .MaxVisibleItems(16)
            .WithScrollbarVisibility(ScrollbarVisibility.Auto);
        foreach (var signal in filtered)
        {
            // ListControl renders item text as markup — escape the whole plain-text label.
            list.AddItem(Escape($"{signal.Signal} [{signal.Kind}] -> {ToAlias(signal.Skill.Name)} [{signal.Skill.Stack} / {signal.Skill.Lane}] ({FormatTokenCount(signal.Skill.TokenCount)} tokens)"), signal);
        }
        list.OnItemActivated((_, item) =>
        {
            if (item.Tag is PackageSignalView signal)
            {
                ShowSkillDetailModal(ws, panel, signal.Skill);
            }
        });
        panel.AddControl(list.Build());
    }

    // -------------------------------------------------------------------------
    // Agents
    // -------------------------------------------------------------------------

    private void BuildAgentsPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var layout = TryResolveAgentLayout(out var layoutError);
        var installer = new AgentInstaller(agentCatalog);
        var installed = layout is null
            ? Array.Empty<InstalledAgentRecord>()
            : SafeGet(() => installer.GetInstalledAgents(layout), Array.Empty<InstalledAgentRecord>());

        var allAgents = agentCatalog.Agents.OrderBy(a => a.Name, StringComparer.Ordinal).ToArray();
        var filteredAgents = allAgents.Where(a => MatchesFilter(a.Name, a.Description)).ToArray();

        panel.AddControl(BuildPropertyPanel("orchestration agents", AccentMediumPurple,
            ("agents", string.IsNullOrEmpty(_searchFilter) ? agentCatalog.Agents.Count.ToString() : $"{filteredAgents.Length}/{agentCatalog.Agents.Count}"),
            ("platform", Escape(Session.Agent.ToString())),
            ("target", layout is null ? $"[red]{Escape(layoutError ?? "unresolved")}[/]" : $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("installed", layout is null ? "[grey]-[/]" : $"{installed.Count}/{agentCatalog.Agents.Count}")));
        AddSearchChip(panel);

        if (agentCatalog.Agents.Count == 0)
        {
            panel.AddControl(BuildNotePanel("agents", "[grey50]No agents available in the catalog.[/]", AccentDeepSkyBlue));
            return;
        }
        if (filteredAgents.Length == 0)
        {
            panel.AddControl(BuildNotePanel("agents", $"[grey50]No agents match “{Escape(_searchFilter)}”.[/]", AccentYellow));
            return;
        }

        var list = StyledList("Agents (Enter for details)")
            .MaxVisibleItems(14)
            .WithScrollbarVisibility(ScrollbarVisibility.Auto);
        foreach (var agent in filteredAgents)
        {
            var isInstalled = installed.Any(i => string.Equals(i.Agent.Name, agent.Name, StringComparison.OrdinalIgnoreCase));
            list.AddItem($"{(isInstalled ? "✓ " : "○ ")}{Escape(ToAlias(agent.Name))}  [dim]{Escape(CompactDescription(agent.Description))}[/]", agent);
        }
        list.OnItemActivated((_, item) =>
        {
            if (item.Tag is AgentEntry agent)
            {
                ShowAgentModal(ws, panel, agent);
            }
        });
        panel.AddControl(list.Build());

        if (layout is null)
        {
            panel.AddControl(BuildNotePanel("note", "[yellow]No native agent directory resolved. Set the platform on the Settings page, or create one of .codex/.claude/.github/.gemini/.junie.[/]", AccentYellow));
            return;
        }

        panel.AddControl(Controls.Button("Install all agents into detected native directories")
            .OnClick((_, _) =>
            {
                var detected = SafeGet(() => AgentInstallTarget.ResolveAllDetected(Session.ProjectDirectory, Session.Scope), Array.Empty<AgentInstallLayout>());
                if (detected.Count == 0)
                {
                    Toast("No native agent directories detected", NotificationSeverity.Warning);
                    return;
                }
                var summary2 = SafeGet(() => new AgentInstaller(agentCatalog).InstallToMultiple(agentCatalog.Agents, detected, force: false), default(AgentInstallSummary));
                ToastResult(summary2, "Install failed", summary2 is null ? string.Empty : $"Installed {summary2.InstalledCount} agent file(s) across {detected.Count} platform(s)");
                BuildAgentsPage(ws, panel);
            }).Build());
    }

    private void ShowAgentModal(ConsoleWindowSystem ws, ScrollablePanelControl owner, AgentEntry agent)
    {
        var detail = new IWindowControl[]
        {
            BuildPropertyPanel(ToAlias(agent.Name), AccentMediumPurple,
                ("agent", Escape(agent.Name)),
                ("skills", agent.Skills.Count == 0 ? "[grey50]-[/]" : Escape(string.Join(", ", agent.Skills.Select(ToAlias)))),
                ("platform", Escape(Session.Agent.ToString()))),
            BuildNotePanel("summary", Escape(agent.Description), AccentDeepSkyBlue),
        };

        var buttons = new List<(string, Action)>();
        var layout = TryResolveAgentLayout(out _);
        if (layout is not null)
        {
            buttons.Add(("Install into current target", () =>
            {
                var summary = SafeGet(() => new AgentInstaller(agentCatalog).Install(new[] { agent }, layout, force: false), default(AgentInstallSummary));
                ToastResult(summary, "Install failed", summary is null ? string.Empty : $"{ToAlias(agent.Name)}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped");
                BuildAgentsPage(ws, owner);
            }));
            buttons.Add(("Remove from current target", () =>
            {
                var summary = SafeGet(() => new AgentInstaller(agentCatalog).Remove(new[] { agent }, layout), default(AgentRemoveSummary));
                ToastResult(summary, "Remove failed", summary is null ? string.Empty : $"Removed {ToAlias(agent.Name)} ({summary.RemovedCount} file(s))");
                BuildAgentsPage(ws, owner);
            }));
        }

        ShowModalNative(ws, $"Agent · {ToAlias(agent.Name)}", detail, buttons.ToArray());
    }

    // -------------------------------------------------------------------------
    // Project sync / recommend
    // -------------------------------------------------------------------------

    private void BuildProjectPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var layout = ResolveSkillLayout();
        var scan = SafeGet(() => new ProjectSkillRecommender(skillCatalog).Analyze(Session.ProjectDirectory), null);
        if (scan is null)
        {
            panel.AddControl(BuildNotePanel("project scan", "[red]Could not scan the project directory.[/]", new Color(200, 60, 60)));
            return;
        }

        var installer = new SkillInstaller(skillCatalog);
        var installedByName = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>())
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);

        var high = scan.Recommendations.Count(r => r.Confidence == RecommendationConfidence.High);
        var med = scan.Recommendations.Count(r => r.Confidence == RecommendationConfidence.Medium);
        var low = scan.Recommendations.Count(r => r.Confidence == RecommendationConfidence.Low);

        panel.AddControl(BuildPropertyPanel("project scan", AccentDeepSkyBlue,
            ("project", $"[grey50]{Escape(CompactPath(scan.ProjectRoot.FullName))}[/]"),
            ("scanned", $"{scan.ProjectFiles.Count} project file(s)"),
            ("frameworks", scan.TargetFrameworks.Count == 0 ? "[grey50]unknown[/]" : Escape(string.Join(", ", scan.TargetFrameworks))),
            ("target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("recommendations", $"{scan.Recommendations.Count}  [grey50]([/][green]{high} high[/][grey50] · [/][yellow]{med} med[/][grey50] · [/][grey]{low} low[/][grey50])[/]")));

        if (scan.Recommendations.Count == 0)
        {
            panel.AddControl(BuildNotePanel("recommendations", "[grey50]No package or framework signals matched the catalog. Start with the[/] [green]dotnet[/] [grey50]and[/] [green]modern-csharp[/] [grey50]skills from the Skills page.[/]", AccentDeepSkyBlue));
            return;
        }

        var list = StyledList("Recommended skills (Enter to install)")
            .MaxVisibleItems(16)
            .WithScrollbarVisibility(ScrollbarVisibility.Auto);
        foreach (var recommendation in scan.Recommendations
                     .OrderByDescending(r => r.Confidence)
                     .ThenBy(r => r.Skill.Name, StringComparer.Ordinal))
        {
            var marker = recommendation.Confidence switch
            {
                RecommendationConfidence.High => "[green]●●●[/]",
                RecommendationConfidence.Medium => "[yellow]●●○[/]",
                _ => "[grey]●○○[/]",
            };
            installedByName.TryGetValue(recommendation.Skill.Name, out var record);
            var status = record is null ? "[deepskyblue1]new[/]" : record.IsCurrent ? "[green]installed[/]" : "[yellow]update[/]";
            list.AddItem($"{marker} {Escape(ToAlias(recommendation.Skill.Name))}  [dim]{status}[/]  [grey]{Escape(string.Join("; ", recommendation.Reasons.Take(2)))}[/]", recommendation);
        }
        list.OnItemActivated((_, item) =>
        {
            if (item.Tag is ProjectSkillRecommendation recommendation)
            {
                // Outdated recommendations need force=true: SkillInstaller.Install skips
                // existing skill directories unless forced, so an "update" entry would
                // otherwise be reported as skipped and stay outdated.
                var isOutdated = installedByName.TryGetValue(recommendation.Skill.Name, out var existing) && !existing.IsCurrent;
                var summary2 = SafeGet(() => new SkillInstaller(skillCatalog).Install(new[] { recommendation.Skill }, ResolveSkillLayout(), force: isOutdated), default(SkillInstallSummary));
                ToastResult(summary2, $"Install failed for {ToAlias(recommendation.Skill.Name)}", summary2 is null ? string.Empty : $"{ToAlias(recommendation.Skill.Name)}: {summary2.InstalledCount} written, {summary2.SkippedExisting.Count} skipped");
                BuildProjectPage(ws, panel);
            }
        });
        panel.AddControl(list.Build());

        // Split recommendations: new ones install with force=false, outdated ones need
        // force=true so the existing skill directory is overwritten with the latest version.
        var newSkills = scan.Recommendations
            .Where(r => !installedByName.ContainsKey(r.Skill.Name))
            .Select(r => r.Skill)
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .ToArray();
        var outdatedSkills = scan.Recommendations
            .Where(r => installedByName.TryGetValue(r.Skill.Name, out var rec) && !rec.IsCurrent)
            .Select(r => r.Skill)
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .ToArray();
        var installable = newSkills.Concat(outdatedSkills).ToArray();
        if (installable.Length > 0)
        {
            panel.AddControl(Controls.Button($"Install all {installable.Length} recommended skill(s)")
                .OnClick((_, _) =>
                {
                    var skillLayout = ResolveSkillLayout();
                    var installer2 = new SkillInstaller(skillCatalog);
                    var newSummary = newSkills.Length == 0 ? default : SafeGet(() => installer2.Install(newSkills, skillLayout, force: false), default(SkillInstallSummary));
                    var updateSummary = outdatedSkills.Length == 0 ? default : SafeGet(() => installer2.Install(outdatedSkills, skillLayout, force: true), default(SkillInstallSummary));
                    var installedCount = (newSummary?.InstalledCount ?? 0) + (updateSummary?.InstalledCount ?? 0);
                    var skippedCount = (newSummary?.SkippedExisting.Count ?? 0) + (updateSummary?.SkippedExisting.Count ?? 0);
                    var failed = installedCount == 0 && skippedCount == 0;
                    Toast(failed ? "Install failed" : $"Installed {installedCount}, skipped {skippedCount}", failed ? NotificationSeverity.Danger : NotificationSeverity.Success);
                    BuildProjectPage(ws, panel);
                }).Build());
        }
    }

    // -------------------------------------------------------------------------
    // Catalog analysis
    // -------------------------------------------------------------------------

    private void BuildAnalysisPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var installed = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>());
        var views = BuildCollectionViews(installed)
            .OrderByDescending(view => view.SkillCount)
            .ToArray();
        var signals = SafeGet(BuildPackageSignals, Array.Empty<PackageSignalView>());
        var heaviest = skillCatalog.Skills.OrderByDescending(skill => skill.TokenCount).Take(12).ToArray();

        panel.AddControl(BuildPropertyPanel("catalog analysis", AccentDeepSkyBlue,
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("collections", views.Length.ToString()),
            ("skills", skillCatalog.Skills.Count.ToString()),
            ("total tokens", FormatTokenCount(skillCatalog.Skills.Sum(skill => skill.TokenCount))),
            ("package signals", signals.Count.ToString())));

        var collectionCards = views.Take(12).Select(view => BuildBulletPanel(
            view.Collection, AccentDeepSkyBlue,
            $"[grey50]skills[/] {view.SkillCount}  [grey50]installed[/] {view.InstalledCount}  [grey50]tokens[/] {FormatTokenCount(view.TokenCount)}")).ToList();
        panel.AddControl(BuildCardGrid(collectionCards, maxColumns: 3));

        var heavyTable = Controls.Table()
            .WithTitle("Heaviest skills (Enter for details)")
            .AddColumn("Skill")
            .AddColumn("Collection")
            .AddColumn("Lane")
            .AddColumn("Tokens", TextJustification.Right)
            .WithSorting()
            .Rounded()
            .WithBorderColor(AccentDeepSkyBlue);
        foreach (var skill in heaviest)
        {
            heavyTable.AddRow(new TableRow(ToAlias(skill.Name), skill.Stack, skill.Lane, FormatTokenCount(skill.TokenCount)) { Tag = skill });
        }
        heavyTable.OnRowActivated((_, idx) =>
        {
            if (idx >= 0 && idx < heaviest.Length)
            {
                ShowSkillDetailModal(ws, panel, heaviest[idx]);
            }
        });
        panel.AddControl(heavyTable.Build());

        // Native bar charts: skills sorted by tokens (heaviest 12), then collections sorted by
        // skill count (top 8). Each bar uses the standard threshold gradient so the eye picks
        // up "big" entries immediately.
        if (heaviest.Length > 0)
        {
            var maxTokens = heaviest.Max(s => s.TokenCount);
            var chart1 = new ScrollablePanelControl
            {
                ShowScrollbar = false,
                EnableMouseWheel = false,
            };
            foreach (var skill in heaviest)
            {
                chart1.AddControl(BuildSkillTokenBar(skill, maxTokens));
            }
            panel.AddControl(BuildSectionPanel("tokens by skill (top 12)", string.Empty, AccentDeepSkyBlue));
            panel.AddControl(chart1);
        }

        var topCollections = views.Take(8).ToArray();
        if (topCollections.Length > 0)
        {
            var maxCount = topCollections.Max(v => v.SkillCount);
            var chart2 = new ScrollablePanelControl
            {
                ShowScrollbar = false,
                EnableMouseWheel = false,
            };
            foreach (var view in topCollections)
            {
                chart2.AddControl(BuildCollectionCountBar(view, maxCount));
            }
            panel.AddControl(BuildSectionPanel("skills per collection (top 8)", string.Empty, AccentTurquoise));
            panel.AddControl(chart2);
        }

        if (signals.Count > 0)
        {
            var signalLines = signals.Take(18).Select(signal =>
                $"[grey]{Escape(signal.Signal)}[/] [grey50]({Escape(signal.Kind)})[/] [grey50]→[/] {Escape(ToAlias(signal.Skill.Name))}").ToArray();
            panel.AddControl(BuildBulletPanel("package signals", AccentTurquoise, signalLines));
        }
    }

    /// <summary>
    /// A horizontal bar showing one skill's token weight against the chart's max. Color follows
    /// a green→yellow→red threshold gradient so heavy skills stand out visually.
    /// </summary>
    private static BarGraphControl BuildSkillTokenBar(SkillEntry skill, int maxTokens)
        => Controls.BarGraph()
            .WithLabel($"{ToAlias(skill.Name)}")
            .WithLabelWidth(28)
            .WithValue(skill.TokenCount)
            .WithMaxValue(maxTokens == 0 ? 1 : maxTokens)
            .WithValueFormat("N0")
            .ShowValue(true)
            .WithStandardGradient()
            .Build();

    /// <summary>
    /// A horizontal bar showing one collection's skill count against the chart's max. Uses the
    /// turquoise accent for the filled portion.
    /// </summary>
    private static BarGraphControl BuildCollectionCountBar(CollectionCatalogView view, int maxCount)
        => Controls.BarGraph()
            .WithLabel(view.Collection)
            .WithLabelWidth(28)
            .WithValue(view.SkillCount)
            .WithMaxValue(maxCount == 0 ? 1 : maxCount)
            .WithValueFormat("0")
            .ShowValue(true)
            .WithFilledColor(AccentTurquoise)
            .Build();

    // -------------------------------------------------------------------------
    // Remove all / Update all action pages
    // -------------------------------------------------------------------------

    private void BuildRemoveAllPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();
        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var installed = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>());

        panel.AddControl(BuildPropertyPanel("remove all installed skills", new Color(200, 60, 60),
            ("target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("installed", installed.Count.ToString())));

        if (installed.Count == 0)
        {
            panel.AddControl(BuildNotePanel("status", "[grey50]Nothing to remove in this target.[/]", AccentDeepSkyBlue));
            return;
        }

        panel.AddControl(Controls.Button($"Remove all {installed.Count} skill(s) from this target")
            .OnClick((_, _) => ConfirmModal(ws, "Remove all installed skills?", $"Deletes every catalog skill directory under {layout.PrimaryRoot.FullName}.", () =>
            {
                var summary = SafeGet(() => new SkillInstaller(skillCatalog).Remove(installed.Select(r => r.Skill).ToArray(), layout), default(SkillRemoveSummary));
                ToastResult(summary, "Remove failed", summary is null ? string.Empty : $"Removed {summary.RemovedCount} skill(s)");
                BuildRemoveAllPage(ws, panel);
            })).Build());
    }

    private void BuildUpdateAllPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();
        var layout = ResolveSkillLayout();
        var installer = new SkillInstaller(skillCatalog);
        var outdated = SafeGet(() => installer.GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>())
            .Where(record => !record.IsCurrent)
            .ToArray();

        panel.AddControl(BuildPropertyPanel("update all outdated skills", AccentYellow,
            ("target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("outdated", outdated.Length.ToString())));

        if (outdated.Length == 0)
        {
            panel.AddControl(BuildNotePanel("status", "[green]All installed skills already match the catalog version.[/]", AccentGreen));
            return;
        }

        var pendingLines = outdated.Select(record =>
            $"[yellow]↻[/] {Escape(ToAlias(record.Skill.Name))}  [grey50]{Escape(record.InstalledVersion)} → {Escape(record.Skill.Version)}[/]").ToArray();
        panel.AddControl(BuildBulletPanel("pending updates", AccentYellow, pendingLines));

        panel.AddControl(Controls.Button($"Update all {outdated.Length} skill(s)")
            .OnClick((_, _) =>
            {
                var msg = UpdateSkillRecords(outdated);
                Toast(msg, msg.Contains("failed", StringComparison.OrdinalIgnoreCase) ? NotificationSeverity.Danger : NotificationSeverity.Success);
                BuildUpdateAllPage(ws, panel);
            }).Build());
    }

    // -------------------------------------------------------------------------
    // Settings / workspace
    // -------------------------------------------------------------------------

    private void BuildSettingsPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var layout = ResolveSkillLayout();
        var agentStatus = ResolveAgentStatus();
        panel.AddControl(BuildPropertyPanel("workspace", AccentDeepSkyBlue,
            ("platform", Escape(Session.Agent.ToString())),
            ("scope", Escape(Session.Scope.ToString())),
            ("project", Escape(CompactPath(Session.ProjectDirectory ?? Environment.CurrentDirectory))),
            ("skill target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("agent target", agentStatus.Layout is null ? $"[red]{Escape(agentStatus.Summary)}[/]" : $"[grey50]{Escape(CompactPath(agentStatus.Layout.PrimaryRoot.FullName))}[/]"),
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("build", ToolVersionInfo.IsDevelopmentBuild ? "[grey50]local development[/]" : "[green]published[/]")));

        // Inline form: native dropdowns (change-on-pick, no modal) for Platform/Scope,
        // a plain Button for catalog refresh. SelectedIndexChanged fires only on user
        // interaction (DropdownBuilder attaches the handler AFTER SelectedIndex is set),
        // so no guard flag is needed against the initial-paint pulse.
        var platformValues = Enum.GetValues<AgentPlatform>();
        var platformDropdown = Controls.Dropdown("Platform")
            .AddItems(platformValues.Select(v => v.ToString()).ToArray())
            .SelectedIndex(Array.IndexOf(platformValues, Session.Agent))
            .OnSelectionChanged((_, idx) =>
            {
                if (idx < 0 || idx >= platformValues.Length) return;
                var chosen = platformValues[idx];
                if (chosen.Equals(Session.Agent)) return;
                Session.Agent = chosen;
                Toast($"Platform set to {chosen}", NotificationSeverity.Success);
            })
            .Build();

        var scopeValues = Enum.GetValues<InstallScope>();
        var scopeDropdown = Controls.Dropdown("Scope")
            .AddItems(scopeValues.Select(v => v.ToString()).ToArray())
            .SelectedIndex(Array.IndexOf(scopeValues, Session.Scope))
            .OnSelectionChanged((_, idx) =>
            {
                if (idx < 0 || idx >= scopeValues.Length) return;
                var chosen = scopeValues[idx];
                if (chosen.Equals(Session.Scope)) return;
                Session.Scope = chosen;
                Toast($"Scope set to {chosen}", NotificationSeverity.Success);
            })
            .Build();

        panel.AddControl(BuildSectionPanel("install target", "[grey50]Platform and scope control where skills and agents are written. Changes take effect immediately.[/]", AccentDeepSkyBlue));
        panel.AddControl(platformDropdown);
        panel.AddControl(scopeDropdown);

        panel.AddControl(BuildSectionPanel("catalog", "[grey50]Pull the latest catalog from upstream.[/]", AccentTurquoise));
        panel.AddControl(Controls.Button("Refresh catalog now")
            .OnClick((_, _) => RefreshCatalogFromUi())
            .Build());
    }

    // -------------------------------------------------------------------------
    // About
    // -------------------------------------------------------------------------

    private void BuildAboutPage(ScrollablePanelControl panel)
    {
        panel.ClearContents();
        panel.AddControl(BuildPropertyPanel("about", AccentDeepSkyBlue,
            ("tool", $"{Escape(ToolIdentity.DisplayCommand)}"),
            ("package", Escape(ToolIdentity.PackageId)),
            ("version", Escape(ToolVersionInfo.CurrentVersion)),
            ("build", ToolVersionInfo.IsDevelopmentBuild ? "[grey50]local development[/]" : "[green]published[/]"),
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("skills", skillCatalog.Skills.Count.ToString()),
            ("agents", agentCatalog.Agents.Count.ToString())));

        panel.AddControl(BuildBulletPanel("surface map", AccentDeepSkyBlue,
            "[grey]Home[/] [grey50]session, catalog telemetry, update notice[/]",
            "[grey]Skills / Installed[/] [grey50]browse, install, update, remove catalog skills[/]",
            "[grey]Collections / Bundles / Packages[/] [grey50]install grouped surfaces[/]",
            "[grey]Agents[/] [grey50]install orchestration agents into native agent directories[/]",
            "[grey]Project[/] [grey50]scan .csproj signals and install recommended skills[/]",
            "[grey]Analysis[/] [grey50]collection sizes, heaviest skills, package signals[/]"));

        panel.AddControl(BuildBulletPanel("notes", AccentGrey,
            "[grey50]This is the SharpConsoleUI command center. Run with redirected stdin/stdout to get the classic prompt shell instead.[/]",
            "[grey50]CLI sub-commands (list, install, recommend, …) are unchanged — see[/] [green]dotnet skills help[/][grey50].[/]"));
    }

    // -------------------------------------------------------------------------
    // Modal + status helpers
    // -------------------------------------------------------------------------

    private void ShowModalNative(ConsoleWindowSystem ws, string title, IReadOnlyList<IWindowControl> contents, params (string Label, Action OnClick)[] buttons)
    {
        Window? modal = null;
        var width = Math.Clamp(SafeConsole(() => Console.WindowWidth, 120) - 10, 56, 116);
        var height = Math.Clamp(SafeConsole(() => Console.WindowHeight, 32) - 6, 14, 34);

        var body = Controls.ScrollablePanel().Build();
        foreach (var c in contents)
        {
            body.AddControl(c);
        }

        void Close()
        {
            if (modal is not null)
            {
                ws.CloseWindow(modal);
            }
        }

        var toolbar = Controls.Toolbar().WithSpacing(2).WithAlignment(HorizontalAlignment.Center);
        foreach (var (label, onClick) in buttons)
        {
            var captured = onClick;
            toolbar.AddButton(label, (_, _) => { Close(); captured(); });
        }
        toolbar.AddButton("Close", (_, _) => Close());

        modal = new WindowBuilder(ws)
            .WithTitle(title)
            .WithSize(width, height)
            .Centered()
            .AsModal()
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(new Color(90, 110, 142))
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            })
            .AddControl(body)
            .AddControl(toolbar.StickyBottom().Build())
            .BuildAndShow();
    }

    private void ConfirmModal(ConsoleWindowSystem ws, string title, string message, Action onConfirm)
    {
        var content = new IWindowControl[]
        {
            BuildNotePanel("confirm", $"[yellow]{Escape(message)}[/]", AccentYellow),
        };
        ShowModalNative(ws, title, content, ("Yes, proceed", onConfirm));
    }

    private void ChooseEnumModal<TEnum>(ConsoleWindowSystem ws, string title, TEnum[] values, TEnum current, Action<TEnum> onPicked)
        where TEnum : struct, Enum
    {
        Window? modal = null;

        void Close()
        {
            if (modal is not null)
            {
                ws.CloseWindow(modal);
            }
        }

        var list = StyledList(title).MaxVisibleItems(Math.Min(values.Length, 10));
        foreach (var value in values)
        {
            list.AddItem((value.Equals(current) ? "● " : "  ") + value, value);
        }
        list.OnItemActivated((_, item) =>
        {
            Close();
            if (item.Tag is TEnum picked)
            {
                onPicked(picked);
            }
        });

        var toolbar = Controls.Toolbar().WithSpacing(2).WithAlignment(HorizontalAlignment.Center);
        toolbar.AddButton("Cancel", (_, _) => Close());

        modal = new WindowBuilder(ws)
            .WithTitle(title)
            .WithSize(Math.Clamp(values.Length == 0 ? 40 : values.Max(v => v.ToString().Length) + 24, 40, 70), Math.Min(values.Length + 8, 18))
            .Centered()
            .AsModal()
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(new Color(90, 110, 142))
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            })
            .AddControl(list.Build())
            .AddControl(toolbar.StickyBottom().Build())
            .BuildAndShow();
    }

    /// <summary>
    /// Opens a small modal hosting a PromptControl. Pressing Enter sets the active page's
    /// _searchFilter and rebuilds it. Esc dismisses without changing the filter. Triggered by
    /// `/` from any list-bearing page.
    /// </summary>
    private void ShowSearchOverlay()
    {
        if (_ws is null) return;
        Window? modal = null;

        void Close()
        {
            if (modal is not null) _ws.CloseWindow(modal);
        }

        var prompt = Controls.Prompt($"  /  ")
            .UnfocusOnEnter(false)
            .OnEntered((_, query) =>
            {
                _searchFilter = (query ?? string.Empty).Trim();
                Close();
                RebuildActivePage();
            })
            .Build();

        var hint = new MarkupControl(new List<string>
        {
            "[grey50]Type to filter the current list. [bold]Enter[/] applies, [bold]Esc[/] cancels.[/]",
            string.IsNullOrEmpty(_searchFilter) ? string.Empty : $"[grey50]current:[/] [yellow]{Escape(_searchFilter)}[/]",
        });

        modal = new WindowBuilder(_ws)
            .WithTitle("search")
            .WithSize(Math.Clamp(SafeConsole(() => Console.WindowWidth, 100) - 20, 50, 80), 9)
            .Centered()
            .AsModal()
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(AccentYellow)
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            })
            .AddControl(hint)
            .AddControl(prompt)
            .BuildAndShow();
    }

    /// <summary>
    /// Global command palette (Ctrl+P). Opens a centered modal hosting a PromptControl + a
    /// ListControl pre-populated with every catalog skill, bundle, and agent. Enter on the prompt
    /// filters the list; Enter on the list activates the entry (skill/bundle/agent detail modal).
    /// </summary>
    private void ShowCommandPalette(ConsoleWindowSystem ws)
    {
        Window? modal = null;
        var allEntries = BuildPaletteEntries();

        void Close()
        {
            if (modal is not null) ws.CloseWindow(modal);
        }

        var listControl = StyledList(null).MaxVisibleItems(14).WithScrollbarVisibility(ScrollbarVisibility.Auto).Build();
        listControl.ItemActivated += (_, item) =>
        {
            if (item.Tag is PaletteEntry entry)
            {
                Close();
                entry.Activate();
            }
        };
        void FillList(string filter)
        {
            listControl.ClearItems();
            var f = filter.Trim();
            var matches = string.IsNullOrEmpty(f)
                ? allEntries
                : allEntries.Where(e => e.SearchHaystack.Contains(f, StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var entry in matches.Take(80))
            {
                listControl.AddItem(new ListItem($"[{entry.AccentMarkup}]{entry.IconLabel}[/]  {Escape(entry.Label)}  [grey50]{Escape(entry.Detail)}[/]") { Tag = entry });
            }
        }

        FillList(string.Empty);

        var prompt = Controls.Prompt("  >  ")
            .UnfocusOnEnter(false)
            .OnEntered((_, query) =>
            {
                FillList(query ?? string.Empty);
            })
            .Build();

        modal = new WindowBuilder(ws)
            .WithTitle("command palette · Esc to close")
            .WithSize(Math.Clamp(SafeConsole(() => Console.WindowWidth, 120) - 10, 64, 100), 22)
            .Centered()
            .AsModal()
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(AccentDeepSkyBlue)
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            })
            .AddControl(prompt)
            .AddControl(listControl)
            .BuildAndShow();
    }

    /// <summary>
    /// Builds the union of every searchable entry — skills, bundles, agents, settings actions —
    /// used to populate the command palette. Each entry knows how to activate itself.
    /// </summary>
    private IReadOnlyList<PaletteEntry> BuildPaletteEntries()
    {
        var entries = new List<PaletteEntry>();

        foreach (var skill in skillCatalog.Skills)
        {
            entries.Add(new PaletteEntry(
                IconLabel: "◇ skill",
                AccentMarkup: "turquoise2",
                Label: ToAlias(skill.Name),
                Detail: $"{skill.Stack} / {skill.Lane}",
                SearchHaystack: $"{skill.Name} {skill.Stack} {skill.Lane}",
                Activate: () => { if (_ws is not null && _activePanel is not null) ShowSkillDetailModal(_ws, _activePanel, skill); }));
        }

        foreach (var bundle in skillCatalog.Packages)
        {
            var b = bundle;
            entries.Add(new PaletteEntry(
                IconLabel: "□ bundle",
                AccentMarkup: "springgreen3",
                Label: b.Name,
                Detail: $"{b.Skills.Count} skill(s)",
                SearchHaystack: $"{b.Name} {b.Title} bundle package",
                Activate: () => { if (_ws is not null && _activePanel is not null) ShowBundleModal(_ws, _activePanel, b, primaryOnly: false); }));
        }

        foreach (var agent in agentCatalog.Agents)
        {
            var a = agent;
            entries.Add(new PaletteEntry(
                IconLabel: "△ agent",
                AccentMarkup: "mediumpurple2",
                Label: ToAlias(a.Name),
                Detail: CompactDescription(a.Description),
                SearchHaystack: $"{a.Name} agent orchestration {a.Description}",
                Activate: () => { if (_ws is not null && _activePanel is not null) ShowAgentModal(_ws, _activePanel, a); }));
        }

        // Settings actions and page jumps.
        entries.Add(new PaletteEntry("⚙ settings", "deepskyblue1", "Settings",                "open workspace settings", "settings platform scope refresh workspace",                    () => NavigateTo(HomeAction.Workspace)));
        entries.Add(new PaletteEntry("↻ refresh",  "deepskyblue1", "Refresh catalog",          "pull the latest catalog",  "refresh catalog reload pull",                                   () => RefreshCatalogFromUi()));
        entries.Add(new PaletteEntry("◈ home",     "deepskyblue1", "Home",                    "session and telemetry",    "home session telemetry",                                        () => { if (_ws is not null && _activePanel is not null) BuildHomePage(_ws, _activePanel); }));
        entries.Add(new PaletteEntry("→ skills",   "turquoise2",   "Skills",                  "browse the catalog",       "skills browse catalog",                                         () => NavigateTo(HomeAction.BrowseSkills)));
        entries.Add(new PaletteEntry("→ installed","green",        "Installed",               "manage installed skills",  "installed manage update remove",                                () => NavigateTo(HomeAction.ManageInstalled)));
        entries.Add(new PaletteEntry("→ collections","deepskyblue1","Collections",            "browse collections",       "collections browse",                                            () => NavigateTo(HomeAction.BrowseCollections)));
        entries.Add(new PaletteEntry("→ bundles",  "springgreen3", "Bundles",                 "focused bundles",          "bundles focused",                                               () => NavigateTo(HomeAction.BrowseBundles)));
        entries.Add(new PaletteEntry("→ packages", "turquoise2",   "Packages",                "NuGet signals",            "packages nuget signals",                                        () => NavigateTo(HomeAction.BrowsePackages)));
        entries.Add(new PaletteEntry("→ agents",   "mediumpurple2","Agents",                  "orchestration agents",     "agents orchestration",                                          () => NavigateTo(HomeAction.BrowseAgents)));
        entries.Add(new PaletteEntry("→ project",  "deepskyblue1", "Project",                 "scan and install",         "project scan recommend",                                        () => NavigateTo(HomeAction.SyncProject)));
        entries.Add(new PaletteEntry("→ analysis", "deepskyblue1", "Analysis",                "catalog analysis",         "analysis stats heaviest",                                       () => NavigateTo(HomeAction.Analysis)));
        entries.Add(new PaletteEntry("→ about",    "grey",         "About",                   "version and surface map",  "about version",                                                 () => NavigateTo(HomeAction.About)));

        return entries;
    }

    private sealed record PaletteEntry(
        string IconLabel,
        string AccentMarkup,
        string Label,
        string Detail,
        string SearchHaystack,
        Action Activate);

    private static ITheme BuildTheme() => new ModernGrayTheme
    {
        ListHoverBackgroundColor = SelectionBg,
        ListHoverForegroundColor = SelectionFg,
        ListUnfocusedHighlightBackgroundColor = UnfocusedSelectionBg,
        ListUnfocusedHighlightForegroundColor = UnfocusedSelectionFg,
    };

    /// <summary>
    /// A list control styled so the selected row is a solid inverted bar — the same bar whether
    /// the row was reached by keyboard, mouse hover, or click (see <see cref="BuildTheme"/>).
    /// </summary>
    private static ListBuilder StyledList(string? title = null) => Controls.List(title)
        .WithScrollbarVisibility(ScrollbarVisibility.Auto)
        .WithAutoHighlightOnFocus(true)
        .WithHoverHighlighting(true)
        .WithHighlightColors(SelectionFg, SelectionBg);

    // -------------------------------------------------------------------------
    // Interactive status bar (dynamic per page, clickable hints, highlighted keys)
    // -------------------------------------------------------------------------

    private void RebuildStatusBar(HomeAction? page)
    {
        var bar = _statusBar;
        if (bar is null)
        {
            return;
        }

        _currentPage = page;
        bar.BatchUpdate(() =>
        {
            bar.ClearAll();

            bar.AddLeft("↑↓", "Move");
            bar.AddLeft("Enter", page is HomeAction.SyncProject ? "Install" : page is HomeAction.Workspace ? "Change" : "Open");
            if (IsListBearingPage(page))
            {
                bar.AddLeft("/", "Search", ShowSearchOverlay);
            }
            bar.AddLeft("Ctrl+P", "Palette", () => { if (_ws is not null) ShowCommandPalette(_ws); });
            foreach (var (key, label, action) in PageShortcuts(page))
            {
                bar.AddLeft(key, label, action);
            }
            bar.AddLeft("Ctrl+R", "Refresh", RefreshCatalogFromUi);
            bar.AddLeft("Esc", "Quit", () => _ws?.Shutdown(0));

            _statusMessage = bar.AddCenterText(string.Empty);

            bar.AddRightText($"[dim]v{Escape(skillCatalog.CatalogVersion)} · {skillCatalog.Skills.Count} skills[/]");
            bar.AddRightSeparator();
            _clockItem = bar.AddRightText(DateTime.Now.ToString("HH:mm:ss"));
        });
    }

    private IEnumerable<(string Key, string Label, Action OnClick)> PageShortcuts(HomeAction? page) => page switch
    {
        HomeAction.ManageInstalled => new (string, string, Action)[]
        {
            ("Ctrl+U", "Update outdated", UpdateAllOutdatedFromUi),
            ("Ctrl+Del", "Remove all", RemoveAllFromUi),
        },
        HomeAction.SyncProject => new (string, string, Action)[]
        {
            ("Ctrl+I", "Install recommended", InstallAllRecommendedFromUi),
        },
        _ => Array.Empty<(string, string, Action)>(),
    };

    private void RebuildActivePage()
    {
        if (_ws is null || _activePanel is null)
        {
            return;
        }

        if (_currentPage is HomeAction action)
        {
            BuildActionPage(_ws, _activePanel, action);
        }
        else
        {
            BuildHomePage(_ws, _activePanel);
        }
    }

    /// <summary>
    /// Shows a transient notification. Info/Success render only as a sliding card; Warning/Danger
    /// also leave a sticky line in the bottom status bar until the next page change so the user
    /// has time to read it. Default severity is Info.
    /// </summary>
    private void Toast(string message, NotificationSeverity? severity = null)
    {
        if (string.IsNullOrEmpty(message)) { ClearStickyStatus(); return; }

        var sev = severity ?? NotificationSeverity.Info;
        _ws?.NotificationStateService.ShowNotification(title: string.Empty, message, sev);

        if (_statusMessage is not null)
        {
            if (sev == NotificationSeverity.Warning)
            {
                _statusMessage.Label = $"[yellow]⚠ {Escape(message)}[/]";
            }
            else if (sev == NotificationSeverity.Danger)
            {
                _statusMessage.Label = $"[red]✘ {Escape(message)}[/]";
            }
            else
            {
                // Info / Success / None — the slide-in card carries the feedback; keep the bar quiet.
                _statusMessage.Label = string.Empty;
            }
        }
    }

    private void ClearStickyStatus()
    {
        if (_statusMessage is not null) _statusMessage.Label = string.Empty;
    }

    /// <summary>
    /// Convenience for "install/remove" callers: a null result is treated as a failure (rendered
    /// as a red toast with sticky status); a non-null result is success (transient green toast).
    /// </summary>
    private void ToastResult(object? result, string failureMessage, string successMessage)
    {
        if (result is null)
            Toast(failureMessage, NotificationSeverity.Danger);
        else
            Toast(successMessage, NotificationSeverity.Success);
    }

    /// <summary>
    /// Case-insensitive substring test against the current search filter. Empty filter matches
    /// everything. Tokens (any of the supplied parts) are matched independently — a row is kept
    /// if ANY token contains the filter so callers can pass name + collection + lane as separate
    /// tokens and get the expected "OR" behavior.
    /// </summary>
    private bool MatchesFilter(params string?[] tokens)
    {
        if (string.IsNullOrWhiteSpace(_searchFilter)) return true;
        var needle = _searchFilter;
        foreach (var token in tokens)
        {
            if (token is not null && token.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Renders a small "filter: …" chip at the top of a list-bearing page so the user knows
    /// the visible list is filtered. Caller is responsible for only emitting it when filter is set.
    /// </summary>
    private void AddSearchChip(ScrollablePanelControl panel)
    {
        if (string.IsNullOrWhiteSpace(_searchFilter)) return;
        panel.AddControl(BuildNotePanel(
            "filter",
            $"[yellow]matching “{Escape(_searchFilter)}”[/]  [grey50]· press[/] [bold]Esc[/] [grey50]to clear[/]",
            AccentYellow));
    }

    private async Task ClockLoopAsync(Window window, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_clockItem is not null)
            {
                _clockItem.Label = DateTime.Now.ToString("HH:mm:ss");
                window.Invalidate(false);
            }
        }
    }

    private void RefreshCatalogFromUi()
    {
        try
        {
            Toast("Refreshing catalog…", NotificationSeverity.Info);
            LoadCatalogsAsync(refreshCatalog: true).GetAwaiter().GetResult();
            Toast($"Catalog refreshed: {skillCatalog.CatalogVersion} ({skillCatalog.Skills.Count} skills)", NotificationSeverity.Success);
        }
        catch (Exception exception)
        {
            Toast($"Refresh failed: {exception.Message}", NotificationSeverity.Danger);
        }

        // RaiseSnapshotChanged fires the AttachSessionEvents handler which calls
        // RebuildTopStatusBar() + RebuildActivePage(); also bump the bottom bar.
        Session.RaiseSnapshotChanged();
        RebuildStatusBar(_currentPage);
    }

    private void UpdateAllOutdatedFromUi()
    {
        var layout = ResolveSkillLayout();
        var outdated = SafeGet(() => new SkillInstaller(skillCatalog).GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>())
            .Where(record => !record.IsCurrent)
            .ToArray();
        if (outdated.Length == 0)
        {
            Toast("No outdated skills in this target", NotificationSeverity.Warning);
            return;
        }

        var msg = UpdateSkillRecords(outdated);
        Toast(msg, msg.Contains("failed", StringComparison.OrdinalIgnoreCase) ? NotificationSeverity.Danger : NotificationSeverity.Success);
        RebuildActivePage();
    }

    private void RemoveAllFromUi()
    {
        if (_ws is null)
        {
            return;
        }

        var layout = ResolveSkillLayout();
        var installed = SafeGet(() => new SkillInstaller(skillCatalog).GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>());
        if (installed.Count == 0)
        {
            Toast("Nothing to remove in this target", NotificationSeverity.Warning);
            return;
        }

        ConfirmModal(_ws, "Remove all installed skills?", $"Deletes every catalog skill from {layout.PrimaryRoot.FullName}.", () =>
        {
            var summary = SafeGet(() => new SkillInstaller(skillCatalog).Remove(installed.Select(record => record.Skill).ToArray(), layout), default(SkillRemoveSummary));
            ToastResult(summary, "Remove failed", summary is null ? string.Empty : $"Removed {summary.RemovedCount} skill(s)");
            RebuildActivePage();
        });
    }

    private void InstallAllRecommendedFromUi()
    {
        var scan = SafeGet(() => new ProjectSkillRecommender(skillCatalog).Analyze(Session.ProjectDirectory), null);
        if (scan is null)
        {
            Toast("Project scan failed", NotificationSeverity.Danger);
            return;
        }

        var layout = ResolveSkillLayout();
        var installedByName = SafeGet(() => new SkillInstaller(skillCatalog).GetInstalledSkills(layout), Array.Empty<InstalledSkillRecord>())
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var newSkills = scan.Recommendations
            .Where(r => !installedByName.ContainsKey(r.Skill.Name))
            .Select(r => r.Skill)
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .ToArray();
        var outdatedSkills = scan.Recommendations
            .Where(r => installedByName.TryGetValue(r.Skill.Name, out var rec) && !rec.IsCurrent)
            .Select(r => r.Skill)
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .ToArray();
        if (newSkills.Length == 0 && outdatedSkills.Length == 0)
        {
            Toast("No new recommended skills to install", NotificationSeverity.Warning);
            return;
        }

        // force=true on outdated entries so existing skill dirs are overwritten, force=false on new ones.
        var installer = new SkillInstaller(skillCatalog);
        var newSummary = newSkills.Length == 0 ? default : SafeGet(() => installer.Install(newSkills, layout, force: false), default(SkillInstallSummary));
        var updateSummary = outdatedSkills.Length == 0 ? default : SafeGet(() => installer.Install(outdatedSkills, layout, force: true), default(SkillInstallSummary));
        var installedCount = (newSummary?.InstalledCount ?? 0) + (updateSummary?.InstalledCount ?? 0);
        var skippedCount = (newSummary?.SkippedExisting.Count ?? 0) + (updateSummary?.SkippedExisting.Count ?? 0);
        var failed = installedCount == 0 && skippedCount == 0;
        Toast(failed ? "Install failed" : $"Installed {installedCount}, skipped {skippedCount}", failed ? NotificationSeverity.Danger : NotificationSeverity.Success);
        RebuildActivePage();
    }

    private static int SafeCount(Func<int> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0;
        }
    }

    private static T SafeGet<T>(Func<T> getter, T fallback)
    {
        try
        {
            return getter();
        }
        catch
        {
            return fallback;
        }
    }

    private static int SafeConsole(Func<int> getter, int fallback)
    {
        try
        {
            var value = getter();
            return value > 0 ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
