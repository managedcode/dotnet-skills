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
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
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

    // Dimmed vertical column separator for borderless page tables. Deliberately a low-contrast
    // desaturated grey-blue — NOT an accent — so the hairlines read as quiet column structure
    // without competing with the turquoise/blue column headers above them.
    private static readonly Color GridLineColor = new(64, 76, 92);

    // Faint accent-tinted fill behind a collection band row in the grouped Collections table — a
    // darkened blend of the page accent toward the dark bg, so bands read as group headers without
    // shouting. Quiet, like GridLineColor.
    private static readonly Color CollectionBandBackground = new(22, 40, 54);

    // Elevated modal surface: a lighter version of the page's dark blue gradient. In a dark UI an
    // "above" surface reads as lighter, so modals get this raised gradient (vs the page's
    // (25,32,52)->(7,7,13)) to feel floated rather than blended into the page.
    private static readonly ColorGradient ElevatedModalGradient =
        ColorGradient.FromColors(new Color(58, 70, 104), new Color(34, 42, 66));
    // Softer warm yellow for "outdated" row foreground — the saturated AccentYellow used to
    // double as both the chart-severity yellow and the row-attention yellow, which made the
    // Project confidence trio fight the Installed table for the user's eye. Desaturated so the
    // row signal stays warm without dominating.
    private static readonly Color OutdatedRowFg = new(200, 180, 80);

    // Live shell state for the dynamic status bar.
    private ConsoleWindowSystem? _ws;
    private ScrollablePanelControl? _activePanel;
    private HomeAction? _currentPage;
    // The nav rail. Held so NavigateTo (palette / Home cards / toolbar buttons) can sync the rail's
    // visual selection to the target page instead of leaving it stuck on the previously-clicked item.
    private NavigationView? _nav;
    // The command-center window — held so the command palette can host its portal overlay.
    private Window? _mainWindow;
    private CommandPalettePortal? _palettePortal;
    private LayoutNode? _palettePortalNode;
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

    // First-click arms the inline two-stage install button on Collections detail (Commit 4);
    // second click commits. Cleared every time the selected collection changes.
    private bool _collectionInstallArmed;

    // Keys (collection name, case-insensitive) of collections currently expanded in the grouped
    // Collections table. Empty = all collapsed (the default at-a-glance view).
    private readonly HashSet<string> _expandedCollections = new(StringComparer.OrdinalIgnoreCase);

    // ===== Off-thread operation queue (installs/removes) — UI-thread-only access by construction =====
    private readonly Queue<Action> _operationQueue = new();
    private bool _operationInProgress;
    private SpinnerTextAnimator? _operationSpinner;

    // True while the quit-confirm dialog is open, so repeated Ctrl+Q/Esc don't stack modals.
    private bool _quitConfirmOpen;

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
            var windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                BuildTheme(),
                options: new ConsoleWindowSystemOptions(ExitKey: null));
            // We own Ctrl+Q (ExitKey disabled above) so it raises the graceful confirm dialog instead
            // of the framework's instant shutdown.
            windowSystem.RegisterGlobalShortcut(ConsoleModifiers.Control, ConsoleKey.Q, RequestExit);
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
        // Rail surfaces only. Exit is a shell hotkey, not a rail destination. Update-all /
        // Remove-all are consolidated into the Installed page (its toolbar + U / Delete
        // shortcuts + per-row modal), so they're hidden from the rail here — their manifest
        // surfaces, HomeAction values, page builders, and CLI verbs all stay intact. This
        // filter is on the SharpConsoleUI call site only; GetHomeActions and the classic
        // fallback menu are untouched.
        var actions = GetHomeActions(installedCount, outdatedCount)
            .Where(action => action.Action is not (HomeAction.Exit or HomeAction.UpdateAll or HomeAction.RemoveAll))
            .ToArray();

        var nav = Controls.NavigationView()
            .WithNavWidth(30)
            .WithPaneHeader("[bold rgb(120,180,255)]  ◆  dotnet skills[/]")
            .WithPaneDisplayMode(NavigationViewDisplayMode.Auto)
            // Show full pane labels at ≥120 cols. On 90–120-col terminals the rail goes
            // Compact (icon-only) — and because every surface now has a DISTINCT geometric
            // icon (see IconFor), compact mode stays readable rather than a column of
            // identical glyphs. Below 90 cols the rail collapses to Minimal (summon on hotkey).
            .WithExpandedThreshold(120)
            .WithCompactThreshold(90)
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
                        new NavigationItem(captured.Label, icon: IconFor(captured.Action), subtitle: captured.Summary) { Tag = captured.Action },
                        panel => BuildActionPage(ws, panel, captured.Action));
                }
            });
        }

        var navView = nav
            .OnSelectedItemChanged((_, e) => RebuildStatusBar(e.NewItem?.Tag as HomeAction?))
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();
        _nav = navView;

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

        _mainWindow = new WindowBuilder(ws)
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

        // Preview-key hook: lets the command palette portal swallow all keys before they reach the page.
        _mainWindow.PreviewKeyPressed += (_, e) => HandlePreviewKey(e);

        RebuildStatusBar(null);
        RebuildTopStatusBar();
    }

    /// <summary>
    /// Distinct geometric (1-cell, terminal-safe) rail icon per surface. Distinct icons keep the
    /// rail readable in Compact (icon-only) mode — otherwise every section item shows the same glyph.
    /// </summary>
    private static string IconFor(HomeAction action) => action switch
    {
        HomeAction.BrowseSkills      => "◇",
        HomeAction.BrowseCollections => "⊞",
        HomeAction.BrowseBundles     => "▣",
        HomeAction.BrowsePackages    => "⬡",
        HomeAction.BrowseAgents      => "⊕",
        HomeAction.ManageInstalled   => "▤",
        HomeAction.SyncProject       => "⌖",
        HomeAction.Analysis          => "∿",
        HomeAction.Workspace         => "⚙",  // Settings
        HomeAction.About             => "ⓘ",
        _ => "›",
    };

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
    /// Navigates to a HomeAction page from outside the rail (Home metric cards, command palette,
    /// page toolbar buttons). Routes through the rail's selection so the rail highlight follows the
    /// content: selecting the matching item rebuilds the page via the rail's own factory and fires
    /// SelectedItemChanged (which updates the status bar). Falls back to a direct build if no rail
    /// item carries the action (e.g. Home, or an action not present in the rail).
    /// </summary>
    private void NavigateTo(HomeAction action)
    {
        if (_ws is null || _activePanel is null) return;

        if (_nav is not null)
        {
            var items = _nav.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Tag is HomeAction tagged && tagged == action)
                {
                    // Setting SelectedIndex applies the rail highlight AND invokes the item's page
                    // factory (→ BuildActionPage) + fires SelectedItemChanged (→ RebuildStatusBar),
                    // so content, rail, and status bar all stay in sync through one path.
                    _nav.SelectedIndex = i;
                    return;
                }
            }
        }

        // Fallback: no matching rail item — build directly (rail selection won't follow).
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

    // Fires BEFORE any control sees the key (Window.PreviewKeyPressed). While the command palette portal
    // is open it captures ALL keys here and marks them handled, so nothing leaks to the focused page/rail
    // (OnKeyPressed runs too late — the focused control would already have processed the key).
    private void HandlePreviewKey(KeyPressedEventArgs e)
    {
        if (_palettePortal != null)
        {
            _palettePortal.ProcessKey(e.KeyInfo);
            e.Handled = true;
        }
    }

    private void HandleGlobalKey(KeyPressedEventArgs e)
    {
        var key = e.KeyInfo;

        // Esc clears an active search filter first, then raises the graceful quit dialog.
        if (key.Key == ConsoleKey.Escape)
        {
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                _searchFilter = string.Empty;
                RebuildActivePage();
                e.Handled = true;
                return;
            }
            RequestExit();
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
        // Page-switch clears transient filters (search + Collections expansion/arm state) so each
        // page lands in a clean state. Use NavigateTo if you need to preserve filter context.
        if (_currentPage != action)
        {
            _searchFilter = string.Empty;
            _expandedCollections.Clear();
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
                AddEmptyState(panel, "Not available in this surface.");
                break;
        }
    }


    // -------------------------------------------------------------------------
    // Native control helpers — every page and modal renders through these.
    // -------------------------------------------------------------------------

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
        return Controls.Panel()
            .WithHeader($"[bold]{Escape(title)}[/]")
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(accent)
            .WithPadding(1, 0, 1, 0)
            .WithContent(body)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
    }


    /// <summary>
    /// One-line identity strip used as a page header. Renders as a single MarkupControl with the
    /// title in accent + bold, followed by middle-dot separated key/value pairs. Replaces the
    /// taller BuildPropertyPanel(...) headers so each page can devote vertical space to data
    /// (tables, graphs) instead of a five-row card. Values are passed in already-marked-up form,
    /// labels are dimmed by this helper.
    /// </summary>
    private static IWindowControl BuildIdentityStrip(string title, Color accent, params (string Label, string Value)[] facts)
        => BuildIdentityStrip(title, accent, hint: null, facts);

    /// <summary>
    /// Identity strip with an optional affordance <paramref name="hint"/> (e.g. "Enter ▸ details")
    /// rendered as a quiet bracketed chip immediately after the title. Pages whose primary table no
    /// longer carries its own title line pass the affordance here instead, so the page has a single
    /// header (the column row) rather than a redundant turquoise title duplicating the strip.
    /// </summary>
    private static IWindowControl BuildIdentityStrip(string title, Color accent, string? hint, params (string Label, string Value)[] facts)
    {
        var hex = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        var head = $"[bold {hex}]{Escape(title)}[/]";
        if (!string.IsNullOrEmpty(hint))
        {
            // Dim bracketed chip — subordinate to the title, signals the row action without
            // competing with the accent-colored column header below.
            head += $"  [grey50]❨[/][grey62]{Escape(hint)}[/][grey50]❩[/]";
        }
        var parts = new List<string> { head };
        foreach (var (label, value) in facts)
        {
            if (string.IsNullOrEmpty(value)) continue;
            parts.Add($"[grey50]{Escape(label)}[/] {value}");
        }
        var line = string.Join("  [grey50]·[/]  ", parts);
        return new MarkupControl(new List<string> { line });
    }

    /// <summary>
    /// Adds an identity strip plus a horizontal rule beneath it. The rule visually separates the
    /// page header from the content below (table, graph, form) the same way the modal toolbar's
    /// AboveLine separates verbs from content. Rule color follows the page accent so the strip
    /// and rule read as a single composite header. Use this in page builders instead of calling
    /// `panel.AddControl(BuildIdentityStrip(...))` directly.
    /// </summary>
    private static void AddIdentityStrip(ScrollablePanelControl panel, string title, Color accent, params (string Label, string Value)[] facts)
        => AddIdentityStrip(panel, title, accent, hint: null, facts);

    /// <summary>
    /// Identity-strip variant carrying an affordance <paramref name="hint"/> chip (see
    /// <see cref="BuildIdentityStrip(string, Color, string?, ValueTuple{string, string}[])"/>). Pages
    /// whose primary table dropped its own title use this so the row action ("Enter ▸ details") lives
    /// in the header strip rather than as a second turquoise line above the columns.
    /// </summary>
    private static void AddIdentityStrip(ScrollablePanelControl panel, string title, Color accent, string? hint, params (string Label, string Value)[] facts)
    {
        panel.AddControl(BuildIdentityStrip(title, accent, hint, facts));
        panel.AddControl(Controls.RuleBuilder()
            .WithColor(accent)
            .WithBorderStyle(BorderStyle.Single)
            .Build());
    }

    /// <summary>
    /// In-page section header: a blank spacer line followed by a titled rule. Replaces the
    /// previous pattern of using `BuildSectionPanel(title, "", accent)` — a full rounded
    /// PanelControl with an empty body — just to display a heading. Two rows instead of three,
    /// and the heading reads as a rule with a caption rather than as an empty panel. The empty
    /// line above gives the title visual breathing room and rhythm between sections.
    /// Use between content blocks (e.g. above each chart on the Analysis page).
    /// </summary>
    private static void AddSectionHeader(ScrollablePanelControl panel, string title, Color accent)
    {
        // Empty spacer line above — a single-row blank MarkupControl gives the title rule room
        // without needing margins. Cheap, predictable rhythm.
        panel.AddControl(new MarkupControl(new List<string> { string.Empty }));
        panel.AddControl(Controls.RuleBuilder()
            .WithTitle(title)
            .TitleLeft()
            .WithColor(accent)
            .WithBorderStyle(BorderStyle.Single)
            .Build());
    }

    // ===== Quiet text-block vocabulary (replaces the rounded accent-box panels) =====

    internal enum NoteSeverity { Info, Warning, Error, Success }

    /// <summary>Quiet empty-state: a spacer + a centered dim line. No box. For passive
    /// "no results / nothing available" states (the page identity strip gives context).</summary>
    private static void AddEmptyState(ScrollablePanelControl panel, string message)
    {
        panel.AddControl(new MarkupControl(new List<string> { string.Empty }));
        panel.AddControl(Controls.Markup()
            .AddLine($"[grey50]{message}[/]")
            .Centered()
            .Build());
    }

    /// <summary>Inline severity note: a single wrapped line with a leading glyph + color, NO box.
    /// The glyph/color carries severity. For actionable notes (warnings, errors, status).</summary>
    private static void AddInlineNote(ScrollablePanelControl panel, string message, NoteSeverity severity)
    {
        // Muted accent hexes (not the raw [yellow]/[red] markup tags, which resolve to the
        // max-bright #FFFF00 / #FF0000 and "scream" against the dark shell). These match the
        // app's AccentYellow / AccentGreen palette so a passive note stays warm, not loud.
        var (glyph, open) = severity switch
        {
            NoteSeverity.Warning => ("⚠", "[#d7af00]"),   // AccentYellow — muted amber
            NoteSeverity.Error => ("✕", "[#d70000]"),     // softened red
            NoteSeverity.Success => ("✓", "[#00af00]"),   // AccentGreen
            _ => ("ℹ", "[grey62]"),
        };
        panel.AddControl(new MarkupControl(new List<string> { $"{open}{glyph}  {message}[/]" }));
    }

    /// <summary>Form section header: a titled rule (via AddSectionHeader) + a dim caption line. No box.
    /// The caller adds the fields after.</summary>
    private static void AddFormSection(ScrollablePanelControl panel, string title, string caption, Color accent)
    {
        AddSectionHeader(panel, title, accent);
        panel.AddControl(new MarkupControl(new List<string> { $"[grey50]{caption}[/]" }));
    }

    /// <summary>Info block: a bold caption + dim body lines, no border. For bullet-list info
    /// ("quick start", "tool update", "surface map", "notes").</summary>
    private static void AddInfoBlock(ScrollablePanelControl panel, string title, params string[] lines)
    {
        var rows = new List<string> { $"[bold]{Escape(title)}[/]" };
        rows.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        panel.AddControl(new MarkupControl(rows));
    }

    /// <summary>De-emphasized titled block for use INSIDE a detail modal (which already has a frame).
    /// A bold caption + body, no accent box.</summary>
    private static IWindowControl BuildModalBlock(string title, string body)
        => new MarkupControl(new List<string> { $"[bold]{Escape(title)}[/]", body });

    /// <summary>
    /// Standard sortable rounded table with a left-aligned title and the accent border color.
    /// TableControl defaults the title to centered; left-aligned reads better against the
    /// left-aligned identity strip above and is consistent across the polished shell. Use this
    /// instead of `Controls.Table().WithTitle(...).WithSorting().Rounded().WithBorderColor(...)`
    /// boilerplate.
    /// </summary>
    private static TableControlBuilder BuildStyledTable(string title, Color accent) => Controls.Table()
        .WithTitle(title, TextJustification.Left)
        .WithSorting()
        .Rounded()
        .WithBorderColor(accent)
        .StretchHorizontal();

    /// <summary>
    /// Borderless variant of <see cref="BuildStyledTable"/> for a page's PRIMARY table — the one that
    /// sits directly inside the NavigationView content area, which already supplies a rounded frame.
    /// Dropping the table's own border removes the redundant inner frame (one frame, not two). Tables
    /// shown inside a modal (which has its own border) keep the rounded <see cref="BuildStyledTable"/>.
    /// </summary>
    private static TableControlBuilder BuildStyledTableBorderless(string title, Color accent)
    {
        var builder = BuildStyledTableBorderless(accent);
        if (!string.IsNullOrEmpty(title))
            builder.WithTitle(title, TextJustification.Left);
        return builder;
    }

    /// <summary>
    /// Titleless borderless page table. The page's identity strip already names the section (and
    /// carries the row-action affordance as a chip), so the table needs no title of its own — a
    /// turquoise title line directly above the same-colored column header read as a duplicate header.
    /// Use this for Skills/Bundles/Packages/Agents; the column row is the only header.
    /// </summary>
    private static TableControlBuilder BuildStyledTableBorderless(Color accent) => Controls.Table()
        .WithSorting()
        .NoBorder()
        .WithBorderColor(accent)
        // Dim hairline between columns — borders are off, so this is the only column structure.
        // A desaturated grey keeps it quiet (see GridLineColor); padded:true gives the glyph a
        // space on each side so it doesn't sit flush against the cell text.
        .WithColumnSeparator('│', GridLineColor, padded: true)
        // Keep the right-aligned final column (e.g. Tokens) off the scrollbar with a 1-cell gutter.
        .ScrollbarGutter()
        .StretchHorizontal()
        // Fill the page's content height so the table viewport (rows + scrollbar) uses the available
        // space instead of leaving a large void below. The page table is always the primary content,
        // so it should own the vertical room.
        .WithVerticalAlignment(VerticalAlignment.Fill);

    /// <summary>
    /// Configures a built TableControl with the polish-PR's standard runtime properties:
    /// `TruncationFade = true` makes truncated cell text fade-to-background over the last 4
    /// columns instead of clipping or showing an ASCII ellipsis. There's no builder method for
    /// this property, so we set it after Build. Call once per table right after the Build site.
    /// </summary>
    private static TableControl ApplyStyledTableRuntime(TableControl table)
    {
        table.TruncationFade = true;
        return table;
    }

    /// <summary>
    /// Builds a page-level action toolbar — sits between the identity strip and the data table.
    /// Each button is a bulk action (per-row actions stay in the modal). Buttons whose
    /// <paramref name="entries"/> tuple has <c>Enabled = false</c> render as disabled, giving
    /// the user a visual signal that the precondition isn't met (e.g. "Update all outdated"
    /// disabled when nothing is outdated). Returns null when no entries are provided so the
    /// caller can drop it without an empty bar.
    /// </summary>
    private static IWindowControl? BuildPageToolbar(params (string Label, bool Enabled, Action OnClick)[] entries)
    {
        if (entries is null || entries.Length == 0) return null;
        var builder = Controls.Toolbar()
            .WithSpacing(1)
            .WithBelowLine(true);
        foreach (var (label, enabled, onClick) in entries)
        {
            var btn = Controls.Button(label).OnClick((_, _) => onClick()).Build();
            btn.IsEnabled = enabled;
            builder.AddButton(btn);
        }
        return builder.Build();
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
    // Project sync / recommend
    // -------------------------------------------------------------------------

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

        // Separator between the modal's data/property panels and the action toolbar. The
        // toolbar's own AboveLine only spans the centered button cluster (the toolbar measures to
        // its content width), so it reads as an underline of the buttons rather than a section
        // divider. Instead, a standalone edge-to-edge Rule spans the full modal width, and the
        // toolbar itself is borderless so the buttons sit centered beneath it.
        var toolbarRule = Controls.RuleBuilder()
            .WithColor(new Color(70, 88, 116))
            .WithBorderStyle(BorderStyle.Single)
            .Build();
        toolbarRule.StickyPosition = StickyPosition.Bottom;

        var toolbar = Controls.Toolbar()
            .WithSpacing(2)
            .WithAlignment(HorizontalAlignment.Center);
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
            .WithBackgroundGradient(ElevatedModalGradient, GradientDirection.Vertical)
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
            .AddControl(toolbarRule)
            .AddControl(toolbar.StickyBottom().Build())
            .BuildAndShow();
    }

    // Single exit funnel for Ctrl+Q, Escape, and the "Quit" status action. Always confirms; the message
    // escalates when a background operation is in progress or queued (quitting then drops the queue).
    private void RequestExit()
    {
        if (_quitConfirmOpen || _ws is null) return;
        _quitConfirmOpen = true;

        bool busy = _operationInProgress || _operationQueue.Count > 0;
        int queued = _operationQueue.Count;
        string body = busy
            ? $"[yellow]⚠ An operation is still running{(queued > 0 ? $" (+{queued} queued)" : string.Empty)}. Quitting now drops queued operations.[/]"
            : "Exit the catalog browser?";
        string confirmLabel = busy ? "Quit anyway" : "Quit";

        ShowExitConfirm("Quit dotnet skills?", body, confirmLabel,
            onConfirm: () => { _quitConfirmOpen = false; _ws?.Shutdown(0); },
            onCancel: () => { _quitConfirmOpen = false; });
    }

    // Compact confirm dialog: a small modal sized to its message, with the message as plain markup (no
    // boxed panel) and a centered [confirmLabel] / [Cancel] toolbar. Escape or Cancel runs onCancel;
    // the confirm button runs onConfirm. Self-sized (vs the large content-modal ShowModalNative) so a
    // one-line question doesn't open a near-fullscreen window.
    private void ShowExitConfirm(string title, string bodyMarkup, string confirmLabel, Action onConfirm, Action onCancel)
    {
        var ws = _ws;
        if (ws is null) return;

        Window? modal = null;
        bool closed = false;
        void Close(Action after)
        {
            if (closed) return;
            closed = true;
            if (modal is not null) ws.CloseWindow(modal);
            after();
        }

        var toolbar = Controls.Toolbar()
            .WithSpacing(2)
            .WithAlignment(HorizontalAlignment.Center);
        toolbar.AddButton(confirmLabel, (_, _) => Close(onConfirm));
        toolbar.AddButton("Cancel", (_, _) => Close(onCancel));

        // Width fits the longer of the message / title / buttons, within a tidy small range.
        int msgWidth = MarkupParser.StripLength(bodyMarkup);
        int btnWidth = confirmLabel.Length + "Cancel".Length + 8;
        int width = Math.Clamp(Math.Max(Math.Max(msgWidth, title.Length), btnWidth) + 8, 40, 72);

        modal = new WindowBuilder(ws)
            .WithTitle(title)
            .WithSize(width, 8)
            .Centered()
            .AsModal()
            .WithBackgroundGradient(ElevatedModalGradient, GradientDirection.Vertical)
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(new Color(90, 110, 142))
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    Close(onCancel);
                    e.Handled = true;
                }
            })
            .AddControl(new MarkupControl(new List<string> { string.Empty, "  " + bodyMarkup }))
            .AddControl(toolbar.StickyBottom().Build())
            .BuildAndShow();
    }

    /// <summary>
    /// Danger-styled confirmation dialog for destructive actions. Red (Danger-role) border, a ⚠
    /// title prefix, the message, an optional "Affected" list (capped at 8 + "+K more"), and a
    /// neutral Cancel (focused by default — reflexive Enter cancels) beside a Danger-red confirm.
    /// Esc cancels. No auto "Close" button.
    /// </summary>
    private void ConfirmDangerModal(ConsoleWindowSystem ws, string title, string message, string confirmLabel, Action onConfirm, IReadOnlyList<string>? affectedItems = null)
    {
        const int AffectedListCap = 8;
        const int ConfirmWidth = 58;   // compact, content-width — not the full info-modal clamp

        Window? modal = null;

        // Danger-role red for the border (the contrast-checked Border derivative, not the fill).
        Color dangerColor = ColorRoleResolver.Resolve(ColorRole.Danger, ws.Theme).Border;

        // Content-sized: a confirm is a question, not a document. Estimate the body line count from
        // the message (wrapped to the inner width) + the optional Affected block, then add chrome
        // (border 2 + title 1 + rule 1 + button row 1 + a little breathing room). Clamp to the screen.
        int termW = SafeConsole(() => Console.WindowWidth, 120);
        int termH = SafeConsole(() => Console.WindowHeight, 32);
        int width = Math.Min(ConfirmWidth, Math.Max(40, termW - 8));
        int innerW = Math.Max(10, width - 4 - 4);   // border (2) + body padding (2 each side)
        int msgLines = message.Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling((double)MarkupParser.StripLength(l) / innerW)));
        int affectedLines = affectedItems is { Count: > 0 }
            ? 2 + Math.Min(affectedItems.Count, AffectedListCap) + (affectedItems.Count > AffectedListCap ? 1 : 0)
            : 0;
        // chrome: border 2 + title 1 + rule 1 + button row 1 + body padding 2 (top+bottom) = 7
        int height = Math.Clamp(msgLines + affectedLines + 7, 9, Math.Max(9, termH - 4));

        // Per-control padding on the body panel only — the message/Affected list breathe off the
        // border, while the sticky rule + centered toolbar below stay full-width.
        var body = Controls.ScrollablePanel().WithPadding(2, 1, 2, 1).Build();
        body.AddControl(new MarkupControl(new List<string> { $"[grey85]{Escape(message)}[/]" }));

        if (affectedItems is { Count: > 0 })
        {
            var lines = new List<string> { string.Empty, "[grey50]Affected[/]" };
            foreach (var name in affectedItems.Take(AffectedListCap))
                lines.Add($"  [grey70]•[/] {Escape(name)}");
            if (affectedItems.Count > AffectedListCap)
                lines.Add($"[grey50]  +{affectedItems.Count - AffectedListCap} more[/]");
            body.AddControl(new MarkupControl(lines));
        }

        void Close()
        {
            if (modal is not null) ws.CloseWindow(modal);
        }

        var toolbarRule = Controls.RuleBuilder()
            .WithColor(new Color(70, 88, 116))
            .WithBorderStyle(BorderStyle.Single)
            .Build();
        toolbarRule.StickyPosition = StickyPosition.Bottom;

        // Cancel FIRST so the toolbar's initial focus (first focusable) lands on it; we also set it
        // explicitly after show as a belt-and-suspenders safe default.
        var cancelButton = new ButtonBuilder()
            .WithText("Cancel")
            .OnClick((_, _) => Close())
            .Build();
        var confirmButton = new ButtonBuilder()
            .WithText(confirmLabel)
            .WithColorRole(ColorRole.Danger)
            .OnClick((_, _) => { Close(); onConfirm(); })
            .Build();

        var toolbar = Controls.Toolbar()
            .WithSpacing(2)
            .WithAlignment(HorizontalAlignment.Center)
            .AddButton(cancelButton)
            .AddButton(confirmButton);

        modal = new WindowBuilder(ws)
            .WithTitle($"⚠  {title}")
            .WithSize(width, height)
            .Centered()
            .AsModal()
            // Flat solid surface (not the elevated gradient used by info/detail modals) so a confirm
            // reads as a focused decision, not a document. The red border carries the danger signal.
            .WithBackgroundColor(ws.Theme.WindowBackgroundColor)
            .Minimizable(false)
            .Maximizable(false)
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(dangerColor)
            .OnKeyPressed((_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            })
            .AddControl(body)
            .AddControl(toolbarRule)
            .AddControl(toolbar.StickyBottom().Build())
            .BuildAndShow();

        // Default focus on Cancel (safe — a reflexive Enter cancels rather than destroys).
        modal?.FocusManager.SetFocus(cancelButton, FocusReason.Programmatic);
    }

    // Backward-compatible wrapper: every existing caller is a destructive remove, so it routes to
    // the danger dialog with a "Remove" confirm label and no affected-item list. Callers wanting the
    // "Affected" list call ConfirmDangerModal directly (see RemoveCheckedSkills).
    private void ConfirmModal(ConsoleWindowSystem ws, string title, string message, Action onConfirm)
        => ConfirmDangerModal(ws, title, message, confirmLabel: "Remove", onConfirm);

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
            .WithBackgroundGradient(ElevatedModalGradient, GradientDirection.Vertical)
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
            .WithBackgroundGradient(ElevatedModalGradient, GradientDirection.Vertical)
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
    /// Global command palette (Ctrl+P). Opens (or toggles closed) a portal overlay: commands-first by
    /// default, with a '/' prefix switching to searching skills/bundles/agents. Live-filters per
    /// keystroke. Replaces the old modal palette.
    /// </summary>
    private void ShowCommandPalette(ConsoleWindowSystem ws)
    {
        if (_palettePortal != null) { DismissPalette(); return; } // toggle closed
        if (_mainWindow is null || _nav is null) return;

        var portal = new CommandPalettePortal(BuildCommandRegistry(), BuildContentEntries(), _mainWindow.Width, _mainWindow.Height)
        {
            Container = _mainWindow
        };
        _palettePortal = portal;
        _palettePortalNode = _mainWindow.CreatePortal(_nav, portal);

        portal.CommandChosen += (_, cmd) => { DismissPalette(); cmd?.Execute(); };
        portal.ContentChosen += (_, entry) => { DismissPalette(); entry?.Activate(); };
        portal.DismissRequested += (_, _) => DismissPalette();
    }

    private void DismissPalette()
    {
        if (_palettePortalNode is not null && _mainWindow is not null && _nav is not null)
            _mainWindow.RemovePortal(_nav, _palettePortalNode);
        _palettePortal = null;
        _palettePortalNode = null;
    }

    /// <summary>
    /// The curated command set shown in the palette's default (commands) mode. Navigate commands route
    /// through NavigateTo (which also syncs the rail); actions run their handler.
    /// </summary>
    private CommandRegistry BuildCommandRegistry()
    {
        var r = new CommandRegistry();
        r.Register(new SkillCommand { Id = "nav.skills", Label = "Skills", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.BrowseSkills) });
        r.Register(new SkillCommand { Id = "nav.installed", Label = "Installed", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.ManageInstalled) });
        r.Register(new SkillCommand { Id = "nav.collections", Label = "Collections", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.BrowseCollections) });
        r.Register(new SkillCommand { Id = "nav.bundles", Label = "Bundles", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.BrowseBundles) });
        r.Register(new SkillCommand { Id = "nav.packages", Label = "Packages", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.BrowsePackages) });
        r.Register(new SkillCommand { Id = "nav.agents", Label = "Agents", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.BrowseAgents) });
        r.Register(new SkillCommand { Id = "nav.project", Label = "Project", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.SyncProject) });
        r.Register(new SkillCommand { Id = "nav.analysis", Label = "Analysis", Category = "Navigate", Icon = "→", Priority = 80, Execute = () => NavigateTo(HomeAction.Analysis) });
        r.Register(new SkillCommand { Id = "nav.about", Label = "About", Category = "Navigate", Icon = "→", Priority = 70, Execute = () => NavigateTo(HomeAction.About) });
        r.Register(new SkillCommand { Id = "nav.home", Label = "Home", Category = "Navigate", Icon = "◈", Priority = 70, Execute = () => { if (_ws is not null && _activePanel is not null) BuildHomePage(_ws, _activePanel); } });
        r.Register(new SkillCommand { Id = "act.refresh", Label = "Refresh catalog", Category = "Action", Icon = "↻", Keybinding = "Ctrl+R", Priority = 75, Execute = () => RefreshCatalogFromUi() });
        r.Register(new SkillCommand { Id = "act.settings", Label = "Settings", Category = "Action", Icon = "⚙", Priority = 60, Execute = () => NavigateTo(HomeAction.Workspace) });
        return r;
    }

    /// <summary>
    /// The searchable content (skills, bundles, agents) shown in the palette's '/' content mode. Each
    /// entry opens its detail modal on activation.
    /// </summary>
    private IReadOnlyList<PaletteEntry> BuildContentEntries()
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

        return entries;
    }

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
            bar.AddLeft("Esc/^Q", "Quit", () => RequestExit());

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
    /// Shows a transient toast. Info/Success render only as the corner toast card; Warning/Danger
    /// also leave a sticky line in the bottom status bar until the next page change so the user
    /// has time to read it. Default severity is Info.
    /// </summary>
    private void Toast(string message, NotificationSeverity? severity = null)
    {
        if (string.IsNullOrEmpty(message)) { ClearStickyStatus(); return; }

        var sev = severity ?? NotificationSeverity.Info;
        _ws?.ToastService.Show(message, sev);

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
        AddInlineNote(panel, $"matching “{Escape(_searchFilter)}”  [grey50]· press[/] [bold]Esc[/] [grey50]to clear[/]", NoteSeverity.Info);
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

    // Submit an install/remove to run off the UI thread, queued behind any in-flight op. Called FROM
    // the UI thread. `work` is the synchronous filesystem/catalog operation (runs off-thread); its
    // exceptions are caught by the runner and toasted (do NOT SafeGet the install call inside work).
    // `onComplete` runs back on the UI thread with the result (toast + page rebuild).
    private void RunOperationQueued<TResult>(string busyLabel, Func<TResult> work, Action<TResult> onComplete)
    {
        _operationQueue.Enqueue(() => StartOperation(busyLabel, work, onComplete));
        if (_operationInProgress)
            Toast($"Queued: {busyLabel}", NotificationSeverity.Info);
        PumpOperations();
    }

    // Runs the next queued op if idle. Re-invoked on each op's completion to drain the queue.
    private void PumpOperations()
    {
        if (_operationInProgress || _operationQueue.Count == 0) return;
        _operationQueue.Dequeue()();
    }

    private void StartOperation<TResult>(string busyLabel, Func<TResult> work, Action<TResult> onComplete)
    {
        _operationInProgress = true;
        var ws = _ws;

        if (ws is not null && _statusMessage is not null)
        {
            _operationSpinner = new SpinnerTextAnimator(ws, SpinnerStyle.Braille,
                frame => _statusMessage.Label = $"{frame} [grey70]{Escape(busyLabel)}[/]");
            _operationSpinner.Start();
        }

        _ = Task.Run(() =>
        {
            TResult result = default!;
            Exception? error = null;
            try { result = work(); }
            catch (Exception ex) { error = ex; }

            void Finish()
            {
                _operationSpinner?.Stop();
                _operationSpinner?.Dispose();
                _operationSpinner = null;
                ClearStickyStatus();

                if (error is not null)
                    Toast($"{busyLabel} failed: {error.Message}", NotificationSeverity.Danger);
                else
                    onComplete(result);

                _operationInProgress = false;
                PumpOperations();
            }

            if (ws is not null) ws.EnqueueOnUIThread(Finish);
            else Finish();
        });
    }

    private void RefreshCatalogFromUi()
    {
        // Invoked from UI handlers (Ctrl+R, refresh button, command palette). The catalog refresh
        // is async, so we must NOT block the UI thread on it (.Result/.GetAwaiter().GetResult()
        // deadlocks once a UI SynchronizationContext is installed and freezes the loop regardless).
        // Instead: toast immediately on the UI thread, run the refresh off-thread, then marshal the
        // result + rebuilds back onto the UI thread via EnqueueOnUIThread.
        Toast("Refreshing catalog…", NotificationSeverity.Info);

        var ws = _ws;
        _ = Task.Run(async () =>
        {
            string message;
            NotificationSeverity severity;
            try
            {
                await LoadCatalogsAsync(refreshCatalog: true).ConfigureAwait(false);
                message = $"Catalog refreshed: {skillCatalog.CatalogVersion} ({skillCatalog.Skills.Count} skills)";
                severity = NotificationSeverity.Success;
            }
            catch (Exception exception)
            {
                message = $"Refresh failed: {exception.Message}";
                severity = NotificationSeverity.Danger;
            }

            void ApplyResult()
            {
                Toast(message, severity);

                // RaiseSnapshotChanged fires the AttachSessionEvents handler which calls
                // RebuildTopStatusBar() + RebuildActivePage(); also bump the bottom bar.
                Session.RaiseSnapshotChanged();
                RebuildStatusBar(_currentPage);
            }

            if (ws is not null)
            {
                ws.EnqueueOnUIThread(ApplyResult);
            }
            else
            {
                ApplyResult();
            }
        });
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
            RunOperationQueued(
                "Removing all installed skills",
                work: () => new SkillInstaller(skillCatalog).Remove(installed.Select(record => record.Skill).ToArray(), layout),
                onComplete: summary =>
                {
                    ToastResult(summary, "Remove failed", summary is null ? string.Empty : $"Removed {summary.RemovedCount} skill(s)");
                    RebuildActivePage();
                }));
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
