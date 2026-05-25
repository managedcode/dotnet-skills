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

        panel.AddControl(BuildIdentityStrip("skill browser", AccentTurquoise,
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

        // Sortable table — five logical dimensions (Collection, Lane, Skill, Version, Tokens)
        // are columns instead of a single bracketed markup-salad row. Default sort matches the
        // legacy ListControl order: by collection rank then collection name then skill name,
        // already baked into the `available` ordering above.
        var table = Controls.Table()
            .WithTitle("Available skills (Enter for details)")
            .AddColumn("Collection")
            .AddColumn("Lane")
            .AddColumn("Skill")
            .AddColumn("Version", TextJustification.Right)
            .AddColumn("Tokens", TextJustification.Right)
            .WithSorting()
            .Rounded()
            .WithBorderColor(AccentTurquoise);
        var builtTable = table.Build();
        foreach (var skill in filtered)
        {
            builtTable.AddRow(new TableRow(
                skill.Stack,
                skill.Lane,
                ToAlias(skill.Name),
                skill.Version,
                FormatTokenCount(skill.TokenCount))
            {
                Tag = skill,
            });
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

        panel.AddControl(BuildIdentityStrip("collection browser", AccentDeepSkyBlue,
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

        panel.AddControl(BuildIdentityStrip(title, AccentDeepSkyBlue,
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

        var table = Controls.Table()
            .WithTitle($"{(primaryOnly ? "Bundles" : "Packages")} (Enter for details)")
            .AddColumn("Bundle")
            .AddColumn("Title")
            .AddColumn("Skills", TextJustification.Right)
            .AddColumn("Tokens", TextJustification.Right)
            .WithSorting()
            .Rounded()
            .WithBorderColor(AccentDeepSkyBlue);
        var builtTable = table.Build();
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

        panel.AddControl(BuildIdentityStrip("package signals", AccentTurquoise,
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

        var table = Controls.Table()
            .WithTitle("Package signals (Enter to inspect linked skill)")
            .AddColumn("Signal")
            .AddColumn("Kind")
            .AddColumn("Skill")
            .AddColumn("Collection")
            .AddColumn("Lane")
            .AddColumn("Tokens", TextJustification.Right)
            .WithSorting()
            .Rounded()
            .WithBorderColor(AccentTurquoise);
        var builtTable = table.Build();
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
        panel.AddControl(BuildIdentityStrip("orchestration agents", AccentMediumPurple,
            ("agents", string.IsNullOrEmpty(_searchFilter) ? agentCatalog.Agents.Count.ToString() : $"{filteredAgents.Length}/{agentCatalog.Agents.Count}"),
            ("installed", layout is null ? "[grey]-[/]" : $"{installed.Count}/{agentCatalog.Agents.Count}"),
            ("target", layout is null ? $"[red]{Escape(layoutError ?? "unresolved")}[/]" : string.Empty)));
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

        // Page toolbar at the top — "Install all into detected platforms" replaces the old
        // bottom-of-page button so the bulk action is visible without scrolling. Disabled
        // when no native agent directory resolved (the user must set the platform first).
        var agentsToolbar = BuildPageToolbar(
            ("Install all into detected platforms", layout is not null, () =>
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
            }));
        if (agentsToolbar is not null) panel.AddControl(agentsToolbar);

        var table = Controls.Table()
            .WithTitle("Agents (Enter for details)")
            .AddColumn("Status", TextJustification.Center, width: 8)
            .AddColumn("Agent")
            .AddColumn("Description")
            .AddColumn("Skills", TextJustification.Right)
            .WithSorting()
            .Rounded()
            .WithBorderColor(AccentMediumPurple);
        var builtTable = table.Build();
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
            panel.AddControl(BuildNotePanel("note", "[yellow]No native agent directory resolved. Set the platform on the Settings page, or create one of .codex/.claude/.github/.gemini/.junie.[/]", AccentYellow));
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
}
