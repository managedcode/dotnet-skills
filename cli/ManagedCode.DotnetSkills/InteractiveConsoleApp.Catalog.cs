// -----------------------------------------------------------------------------
// Catalog surfaces — the browse pages for Skills, Collections, Bundles,
// Packages, and Agents. Each page is a list/table of catalog entries with
// modal-on-Enter detail. Split out of Shell.cs so each surface group lives
// near its peers.
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

        AddIdentityStrip(panel, "skill browser", AccentTurquoise, "Enter ▸ details",
            ("available", $"{filtered.Length}/{available.Length}"),
            ("installed", $"{installed.Count}/{skillCatalog.Skills.Count}"));
        AddSearchChip(panel);

        if (available.Length == 0)
        {
            AddEmptyState(panel, "Every catalog skill is already installed in this target.");
            return;
        }
        if (filtered.Length == 0)
        {
            AddEmptyState(panel, $"No skills match “{Escape(_searchFilter)}”.");
            return;
        }

        // Sortable table — five logical dimensions (Collection, Lane, Skill, Version, Tokens)
        // are columns instead of a single bracketed markup-salad row. Default sort matches the
        // legacy ListControl order: by collection rank then collection name then skill name,
        // already baked into the `available` ordering above.
        // Collection and Lane were columns, which meant every row repeated the two values above
        // it - 201 rows carrying 23 distinct collections. They are a hierarchy, so the tree
        // holds them once and the Skill column gets the width back.
        //
        // Expanded by default, unlike Collections: this page exists to FIND one skill, and a
        // skill hidden behind two closed levels is a skill the user cannot see. The grouping is
        // here to remove repetition, not to hide rows.
        var table = BuildStyledTreeTableBorderless(AccentTurquoise)
            .AddColumn("Skill")
            .AddColumn("Version", TextJustification.Right, width: 9)
            .AddColumn("Tokens", TextJustification.Right, width: 9);
        var builtTable = ApplyStyledTableRuntime(table.Build());

        foreach (var collectionGroup in filtered.GroupBy(skill => skill.Stack, StringComparer.Ordinal))
        {
            var collectionSkills = collectionGroup.ToArray();
            var collectionRow = builtTable.AddRootRow(
                Escape(collectionGroup.Key),
                string.Empty,
                FormatTokenCount(collectionSkills.Sum(skill => skill.TokenCount)));

            foreach (var laneGroup in collectionSkills.GroupBy(skill => skill.Lane, StringComparer.Ordinal))
            {
                var laneSkills = laneGroup.ToArray();
                var laneRow = collectionRow.AddChild(
                    Escape(laneGroup.Key),
                    string.Empty,
                    FormatTokenCount(laneSkills.Sum(skill => skill.TokenCount)));

                foreach (var skill in laneSkills)
                {
                    var skillRow = laneRow.AddChild(
                        Escape(ToAlias(skill.Name)),
                        Escape(skill.Version),
                        FormatTokenCount(skill.TokenCount));
                    skillRow.Tag = skill;
                }
            }
        }

        // Use SelectedRow.Tag to recover the skill — the display index changes when the user
        // re-sorts via column header click, so indexing into `filtered` would be wrong.
        builtTable.RowActivated += (_, _) =>
        {
            if (builtTable.SelectedRow?.Tag is SkillEntry skill)
            {
                ShowSkillDetailModal(ws, panel, skill);
            }
        };
        panel.AddControl(builtTable);
    }

    /// <param name="onChanged">
    /// What to rebuild after an install. Defaults to the skill browser; pages that show skills in
    /// another shape (the Project scan, say) pass their own rebuild so the modal can be reused
    /// without leaving the caller's page stale.
    /// </param>
    private void ShowSkillDetailModal(
        ConsoleWindowSystem ws,
        ScrollablePanelControl owner,
        SkillEntry skill,
        Action? onChanged = null)
    {
        var refresh = onChanged ?? (() => BuildSkillBrowserPage(ws, owner));

        // Fold the one-line summary into the top of the scrolling markdown document so the modal
        // reads as one cohesive skill page (summary lead-in, then the full SKILL.md) rather than
        // three stacked blocks. The identity strip stays pinned above.
        var body = ComposeSkillMarkdown(skill.Description, LoadSkillPreview(skill));

        var detail = new IWindowControl[]
        {
            BuildPropertyPanel(ToAlias(skill.Name), AccentTurquoise,
                ("skill", Escape(skill.Name)),
                ("collection", Escape(skill.Stack)),
                ("lane", Escape(skill.Lane)),
                ("version", Escape(skill.Version)),
                ("tokens", FormatTokenCount(skill.TokenCount))),
            BuildScrollingMarkdownBlock("preview", body),
        };

        ShowModalNative(ws, $"Skill · {ToAlias(skill.Name)}", detail,
            ("Install into current target", () =>
            {
                var layout = ResolveSkillLayout();
                RunOperationQueued(
                    $"Installing {ToAlias(skill.Name)}",
                    work: () => new SkillInstaller(skillCatalog).Install(new[] { skill }, layout, force: false),
                    onComplete: summary =>
                    {
                        if (summary is null)
                            Toast($"Install failed for {ToAlias(skill.Name)}", NotificationSeverity.Danger);
                        else
                            Toast($"{ToAlias(skill.Name)}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped", NotificationSeverity.Success);
                        refresh();
                    });
            }
        ),
            ("Force reinstall", () =>
            {
                var layout = ResolveSkillLayout();
                RunOperationQueued(
                    $"Reinstalling {ToAlias(skill.Name)}",
                    work: () => new SkillInstaller(skillCatalog).Install(new[] { skill }, layout, force: true),
                    onComplete: summary =>
                    {
                        if (summary is null)
                            Toast($"Install failed for {ToAlias(skill.Name)}", NotificationSeverity.Danger);
                        else
                            Toast($"{ToAlias(skill.Name)}: reinstalled ({summary.InstalledCount} written)", NotificationSeverity.Success);
                        refresh();
                    });
            }
        ));
    }
    // -------------------------------------------------------------------------
    // Collections
    // -------------------------------------------------------------------------

    private enum CollectionRowKind { Collection, Lane, Skill }

    // Identity carried in each grouped-table TableRow.Tag so RowActivated/selection can branch on row
    // kind and recover data without indexing into a list (row count changes on every expand/collapse).
    private sealed record CollectionRowTag(
        CollectionRowKind Kind,
        CollectionCatalogView Collection,
        CollectionLaneView? Lane = null,
        SkillEntry? Skill = null);

    // Fixed width of the in-cell weight bar so the columns after it stay aligned across all rows.
    private const int CollectionBarCellWidth = 16;

    // The "cool" blue→cyan gradient — the same one the Analysis token chart uses
    // (ColorGradient.Predefined["cool"] = Blue → Cyan1). Collection weight bars sweep this gradient
    // ALONG their length, so a glance reads the same way as the Analysis bars.
    private static readonly ColorGradient WeightBarGradient = ColorGradient.FromColors(Color.Blue, Color.Cyan1);

    // Builds an in-cell horizontal weight bar as markup: a run of █ that sweeps the cool gradient
    // along its length (dim blue at the start → bright cyan at the end), exactly like the Analysis
    // BarGraphs; sized to value/parentMax and padded with ░ to cellWidth so the columns after it stay
    // aligned. A long (heavy) bar shows the full blue→cyan sweep; a short bar shows only its start.
    // parentMax <= 0 → empty track.
    private static string WeightBarMarkup(int value, int parentMax, int cellWidth = CollectionBarCellWidth)
    {
        if (cellWidth <= 0) return string.Empty;
        int filled = 0;
        if (parentMax > 0 && value > 0)
        {
            double ratio = System.Math.Clamp((double)value / parentMax, 0.0, 1.0);
            filled = (int)System.Math.Round(ratio * cellWidth);
            filled = System.Math.Clamp(filled, 1, cellWidth); // any nonzero value shows at least 1 cell
        }
        int empty = cellWidth - filled;

        // Per-cell gradient along the bar: cell i maps to Interpolate(i/(filled-1)) so the run runs
        // the full Blue→Cyan sweep regardless of length. A 1-cell bar uses the gradient's start.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < filled; i++)
        {
            double t = filled > 1 ? (double)i / (filled - 1) : 0.0;
            var c = WeightBarGradient.Interpolate(t);
            sb.Append($"[#{c.R:X2}{c.G:X2}{c.B:X2}]█[/]");
        }
        if (empty > 0)
            sb.Append($"[grey30]{new string('░', empty)}[/]");
        return sb.ToString();
    }

    // Builds the collection -> lane -> skill hierarchy as real nested rows.
    //
    // This used to be a flat TableControl: every toggle cleared the rows and re-added them with
    // "    " / "      " indent literals and a hand-drawn caret, and the selection had to be
    // restored by tag identity because every row index moved. TreeTableControl holds the nesting
    // itself, so expansion is the control's business: the rows are built once, the caret and the
    // guide are drawn by the control, and the selection survives a toggle because the rows do.
    private void BuildCollectionRows(TreeTableControl table, IReadOnlyList<CollectionCatalogView> views)
    {
        table.ClearRows();

        int maxCollectionTokens = views.Count == 0 ? 0 : views.Max(v => v.TokenCount);

        foreach (var view in views)
        {
            var bandRow = table.AddRootRow(
                Escape(view.Collection),
                $"{view.InstalledCount}/{view.SkillCount}",
                WeightBarMarkup(view.TokenCount, maxCollectionTokens),
                FormatTokenCount(view.TokenCount));
            bandRow.Tag = new CollectionRowTag(CollectionRowKind.Collection, view);
            bandRow.BackgroundColor = CollectionBandBackground;

            foreach (var lane in view.Lanes)
            {
                var laneRow = bandRow.AddChild(
                    Escape(lane.Lane),
                    $"{lane.InstalledCount}/{lane.Skills.Count}",
                    string.Empty, // lanes are grouping headers - no bar
                    FormatTokenCount(lane.TokenCount));
                laneRow.Tag = new CollectionRowTag(CollectionRowKind.Lane, view, lane);

                foreach (var skill in lane.Skills)
                {
                    var skillRow = laneRow.AddChild(
                        Escape(ToAlias(skill.Name)),
                        "\u00b7",
                        string.Empty, // bars are a collection-level comparison only
                        FormatTokenCount(skill.TokenCount));
                    skillRow.Tag = new CollectionRowTag(CollectionRowKind.Skill, view, lane, skill);
                }
            }
        }

        // Collections start closed, as they did when the page tracked expansion itself: the
        // page is a browser, and 201 skills opened at once is a wall rather than a catalog.
        table.CollapseAll();
    }

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

        AddIdentityStrip(panel, "collection browser", AccentDeepSkyBlue,
            ("collections", string.IsNullOrEmpty(_searchFilter) ? views.Length.ToString() : $"{filtered.Length}/{views.Length}"),
            ("skills", skillCatalog.Skills.Count.ToString()),
            ("installed", $"{installed.Count}/{skillCatalog.Skills.Count}"));
        AddSearchChip(panel);

        if (views.Length == 0)
        {
            AddEmptyState(panel, "No collections in this catalog version.");
            return;
        }
        if (filtered.Length == 0)
        {
            AddEmptyState(panel, $"No collections match “{Escape(_searchFilter)}”.");
            return;
        }

        // Single borderless grouped table — Collection bands collapse/expand to reveal Lane sub-headers
        // and Skill rows. Matches the Phase-1 grammar of every other page (no splitter, no nested frame).
        var table = ApplyStyledTableRuntime(BuildStyledTreeTableBorderless(AccentDeepSkyBlue)
            .AddColumn("Name")
            .AddColumn("Installed", TextJustification.Right, width: 9)
            .AddColumn("Weight", TextJustification.Left, width: CollectionBarCellWidth)
            .AddColumn("Tokens", TextJustification.Right, width: 9)
            .Build());

        // Keyboard Enter / double-click: skills open detail. Collection and lane rows expand and
        // collapse themselves - the control owns that now, so there is no toggle to write, no
        // expansion set to keep, and no rebuild that would move the selection.
        table.RowActivated += (_, _) =>
        {
            if (table.SelectedRow?.Tag is not CollectionRowTag tag) return;
            if (tag.Kind == CollectionRowKind.Skill && tag.Skill is not null)
                ShowSkillDetailModal(ws, panel, tag.Skill);
        };

        panel.AddControl(table);
        BuildCollectionRows(table, filtered);

        // Resolves the collection the install action targets = the collection owning the highlighted
        // row (band, lane, or skill), falling back to the first when nothing is selected.
        CollectionCatalogView InstallTarget()
            => (table.SelectedRow?.Tag as CollectionRowTag)?.Collection ?? filtered[0];

        // Caption names the highlighted collection (explicit install target) AND reflects the two-stage
        // arm state.
        string InstallLabel(CollectionCatalogView t)
            => _collectionInstallArmed
                ? $"Click again to install “{t.Collection}” ({t.SkillCount} skill(s))"
                : $"Install “{t.Collection}” ({t.SkillCount} skill(s))";

        // Build the install button explicitly so we can keep a reference and update its caption live as
        // the selection moves between collections (the toolbar is built once and never rebuilt on a
        // mere selection change). Styling mirrors BuildPageToolbar (below-line, spacing 1).
        var installButton = Controls.Button(InstallLabel(InstallTarget()))
            .OnClick((_, _) =>
            {
                var current = InstallTarget();
                if (!_collectionInstallArmed)
                {
                    _collectionInstallArmed = true;
                    Toast($"Click again to confirm installing {current.SkillCount} skill(s)", NotificationSeverity.Warning);
                    BuildCollectionsPage(ws, panel);
                    return;
                }
                _collectionInstallArmed = false;
                var layout = ResolveSkillLayout();
                RunOperationQueued(
                    $"Installing {current.Collection} ({current.SkillCount} skills)",
                    work: () =>
                    {
                        var skills = new SkillInstaller(skillCatalog).SelectSkillsFromCollections(new[] { current.Collection });
                        return skills.Count == 0 ? null : new SkillInstaller(skillCatalog).Install(skills, layout, force: false);
                    },
                    onComplete: summary =>
                    {
                        ToastResult(summary, $"Could not install collection {current.Collection}", summary is null ? string.Empty : $"{current.Collection}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped");
                        BuildCollectionsPage(ws, panel);
                    });
            })
            .Build();
        installButton.IsEnabled = InstallTarget().SkillCount > 0;

        // Keep the install caption (and enabled state) in sync as the cursor moves between collections.
        // Use the ROW PASSED BY THE EVENT, not table.SelectedRow — the property may not be updated yet
        // when the event fires, which would leave the caption one row behind.
        //
        // The update is deferred via EnqueueOnUIThread: this handler fires synchronously from inside
        // input dispatch (mouse click → the control's own expand → SetSelectedRow → this event). Mutating
        // the button + repainting from here re-enters the renderer while the window content lock is held
        // and deadlocks the UI thread (watchdog stall: "blocked in Click / TableControl",
        // Window.EnsureContentReady → Monitor.Enter — do NOT call ws.ForceRender here). Running the
        // update on the next UI-loop drain applies it outside the dispatch stack, so it's both
        // deadlock-free AND lands on the very next frame (no one-selection lag).
        table.SelectedRowItemChanged += (_, row) =>
        {
            var t = (row?.Tag as CollectionRowTag)?.Collection ?? InstallTarget();
            ws.EnqueueOnUIThread(() =>
            {
                installButton.Text = InstallLabel(t);
                installButton.IsEnabled = t.SkillCount > 0;
            });
        };

        // Above-line (not below): the rule separates the table from the install button, so the order
        // reads table → ruler → button rather than button → ruler → empty space.
        var installToolbar = Controls.Toolbar()
            .WithSpacing(1)
            .WithAboveLine(true)
            .AddButton(installButton)
            .Build();
        panel.AddControl(installToolbar);
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

        AddIdentityStrip(panel, title, AccentDeepSkyBlue, "Enter ▸ details",
            (primaryOnly ? "bundles" : "packages", string.IsNullOrEmpty(_searchFilter) ? packages.Length.ToString() : $"{filtered.Length}/{packages.Length}"),
            ("skills covered", skillCatalog.Skills.Count.ToString()));
        AddSearchChip(panel);

        if (packages.Length == 0)
        {
            AddEmptyState(panel, "Nothing available in this catalog version.");
            return;
        }
        if (filtered.Length == 0)
        {
            AddEmptyState(panel, $"No bundles match “{Escape(_searchFilter)}”.");
            return;
        }

        var table = BuildStyledTableBorderless(AccentDeepSkyBlue)
            .AddColumn("Bundle")
            .AddColumn("Title")
            .AddColumn("Skills", TextJustification.Right, width: 8)
            .AddColumn("Tokens", TextJustification.Right, width: 9);
        var builtTable = ApplyStyledTableRuntime(table.Build());
        foreach (var package in filtered)
        {
            var tokenCount = package.Skills.Sum(name => skillTokens.TryGetValue(name, out var value) ? value : 0);
            builtTable.AddRow(new TableRow(
                package.Name,
                package.Title,
                package.Skills.Count.ToString(),
                FormatTokenCount(tokenCount))
            {
                Tag = package,
            });
        }
        builtTable.RowActivated += (_, _) =>
        {
            if (builtTable.SelectedRow?.Tag is SkillPackageEntry package)
            {
                ShowBundleModal(ws, panel, package, primaryOnly);
            }
        };
        panel.AddControl(builtTable);
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
            BuildMarkdownBlock("summary", package.Description),
        };

        ShowModalNative(ws, $"Bundle · {package.Name}", detail,
            ("Install bundle into current target", () =>
            {
                var layout = ResolveSkillLayout();
                RunOperationQueued(
                    $"Installing {package.Name}",
                    work: () =>
                    {
                        var skills = new SkillInstaller(skillCatalog).SelectSkillsFromPackages(new[] { package.Name });
                        return skills.Count == 0 ? null : new SkillInstaller(skillCatalog).Install(skills, layout, force: false);
                    },
                    onComplete: summary =>
                    {
                        ToastResult(summary, $"Could not install bundle {package.Name}", summary is null ? string.Empty : $"{package.Name}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped");
                        BuildBundlesPage(ws, owner, primaryOnly);
                    });
            }
        ));
    }

    // -------------------------------------------------------------------------
    // Packages — NuGet ids / prefixes → catalog skills
    // -------------------------------------------------------------------------

    private void BuildPackagesPage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        panel.ClearContents();

        var signals = SafeGet(BuildPackageSignals, Array.Empty<PackageSignalView>());
        var filtered = signals.Where(s => MatchesFilter(s.Signal, s.Skill.Name, s.Skill.Stack, s.Skill.Lane)).ToArray();

        AddIdentityStrip(panel, "package signals", AccentTurquoise, "Enter ▸ inspect linked skill",
            ("signals", string.IsNullOrEmpty(_searchFilter) ? signals.Count.ToString() : $"{filtered.Length}/{signals.Count}"),
            ("skills covered", signals.Select(s => s.Skill.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()));
        AddSearchChip(panel);

        if (signals.Count == 0)
        {
            AddEmptyState(panel, "No NuGet package or prefix signals are present in this catalog version.");
            return;
        }
        if (filtered.Length == 0)
        {
            AddEmptyState(panel, $"No signals match “{Escape(_searchFilter)}”.");
            return;
        }

        var table = BuildStyledTableBorderless(AccentTurquoise)
            .AddColumn("Signal")
            .AddColumn("Kind")
            .AddColumn("Skill")
            .AddColumn("Collection")
            .AddColumn("Lane")
            .AddColumn("Tokens", TextJustification.Right, width: 9);
        var builtTable = ApplyStyledTableRuntime(table.Build());
        foreach (var signal in filtered)
        {
            builtTable.AddRow(new TableRow(
                signal.Signal,
                signal.Kind,
                ToAlias(signal.Skill.Name),
                signal.Skill.Stack,
                signal.Skill.Lane,
                FormatTokenCount(signal.Skill.TokenCount))
            {
                Tag = signal,
            });
        }
        builtTable.RowActivated += (_, _) =>
        {
            if (builtTable.SelectedRow?.Tag is PackageSignalView signal)
            {
                ShowSkillDetailModal(ws, panel, signal.Skill);
            }
        };
        panel.AddControl(builtTable);
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

        // platform + target live in the top StatusBar; surface "unresolved" target here as a
        // first-class fact because the agent layout has a separate resolver from the skill one.
        AddIdentityStrip(panel, "orchestration agents", AccentMediumPurple, "Enter ▸ details",
            ("agents", string.IsNullOrEmpty(_searchFilter) ? agentCatalog.Agents.Count.ToString() : $"{filteredAgents.Length}/{agentCatalog.Agents.Count}"),
            ("installed", layout is null ? "[grey]-[/]" : $"{installed.Count}/{agentCatalog.Agents.Count}"),
            ("target", layout is null ? $"[#d70000]{Escape(layoutError ?? "unresolved")}[/]" : string.Empty));
        AddSearchChip(panel);

        if (agentCatalog.Agents.Count == 0)
        {
            AddEmptyState(panel, "No agents available in the catalog.");
            return;
        }
        if (filteredAgents.Length == 0)
        {
            AddEmptyState(panel, $"No agents match “{Escape(_searchFilter)}”.");
            return;
        }

        // Page toolbar at the top — "Install all into detected platforms" replaces the old
        // bottom-of-page button so the bulk action is visible without scrolling.
        var agentsToolbar = BuildPageToolbar(
            ("Install all into detected platforms", layout is not null, () =>
            {
                var detected = SafeGet(() => AgentInstallTarget.ResolveAllDetected(Session.ProjectDirectory, Session.Scope), Array.Empty<AgentInstallLayout>());
                if (detected.Count == 0)
                {
                    Toast("No shared or native agent directories resolved", NotificationSeverity.Warning);
                    return;
                }
                RunOperationQueued(
                    $"Installing all agents across {detected.Count} platform(s)",
                    work: () => new AgentInstaller(agentCatalog).InstallToMultiple(agentCatalog.Agents, detected, force: false),
                    onComplete: summary2 =>
                    {
                        ToastResult(summary2, "Install failed", summary2 is null ? string.Empty : $"Installed {summary2.InstalledCount} agent file(s) across {detected.Count} platform(s)");
                        BuildAgentsPage(ws, panel);
                    });
            }
        ));
        if (agentsToolbar is not null) panel.AddControl(agentsToolbar);

        // Status fits "○ available"/"✓ installed" (~11 cols); Agent gets a fixed width so it isn't
        // collapsed by the stretch Description column (two stretch columns starve each other).
        var table = BuildStyledTableBorderless(AccentMediumPurple)
            .AddColumn("Status", TextJustification.Left, width: 12)
            .AddColumn("Agent", TextJustification.Left, width: 28)
            .AddColumn("Description")
            .AddColumn("Skills", TextJustification.Right, width: 8);
        var builtTable = ApplyStyledTableRuntime(table.Build());
        foreach (var agent in filteredAgents)
        {
            var isInstalled = installed.Any(i => string.Equals(i.Agent.Name, agent.Name, StringComparison.OrdinalIgnoreCase));
            builtTable.AddRow(new TableRow(
                isInstalled ? "✓ installed" : "○ available",
                ToAlias(agent.Name),
                CompactDescription(agent.Description),
                agent.Skills.Count.ToString())
            {
                Tag = agent,
            });
        }
        builtTable.RowActivated += (_, _) =>
        {
            if (builtTable.SelectedRow?.Tag is AgentEntry agent)
            {
                ShowAgentModal(ws, panel, agent);
            }
        };
        panel.AddControl(builtTable);

        if (layout is null)
        {
            AddInlineNote(panel, "No agent directory resolved. Set the platform or target on the Settings page.", NoteSeverity.Warning);
        }
        // Bulk install lives in the page toolbar at the top.
    }

    private void ShowAgentModal(ConsoleWindowSystem ws, ScrollablePanelControl owner, AgentEntry agent)
    {
        var detail = new IWindowControl[]
        {
            BuildPropertyPanel(ToAlias(agent.Name), AccentMediumPurple,
                ("agent", Escape(agent.Name)),
                ("skills", agent.Skills.Count == 0 ? "[grey50]-[/]" : Escape(string.Join(", ", agent.Skills.Select(ToAlias)))),
                ("platform", Escape(Session.Agent.ToString()))),
            BuildMarkdownBlock("summary", agent.Description),
        };

        var buttons = new List<(string, Action)>();
        var layout = TryResolveAgentLayout(out _);
        if (layout is not null)
        {
            buttons.Add(("Install into current target", () =>
            {
                RunOperationQueued(
                    $"Installing agent {ToAlias(agent.Name)}",
                    work: () => new AgentInstaller(agentCatalog).Install(new[] { agent }, layout, force: false),
                    onComplete: summary =>
                    {
                        ToastResult(summary, "Install failed", summary is null ? string.Empty : $"{ToAlias(agent.Name)}: {summary.InstalledCount} written, {summary.SkippedExisting.Count} skipped");
                        BuildAgentsPage(ws, owner);
                    });
            }
            ));
            buttons.Add(("Remove from current target", () =>
            {
                RunOperationQueued(
                    $"Removing agent {ToAlias(agent.Name)}",
                    work: () => new AgentInstaller(agentCatalog).Remove(new[] { agent }, layout),
                    onComplete: summary =>
                    {
                        ToastResult(summary, "Remove failed", summary is null ? string.Empty : $"Removed {ToAlias(agent.Name)} ({summary.RemovedCount} file(s))");
                        BuildAgentsPage(ws, owner);
                    });
            }
            ));
        }

        ShowModalNative(ws, $"Agent · {ToAlias(agent.Name)}", detail, buttons.ToArray());
    }
}
