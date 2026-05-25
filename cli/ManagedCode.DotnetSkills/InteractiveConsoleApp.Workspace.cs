// -----------------------------------------------------------------------------
// Workspace surfaces — the pages that operate on the current target rather
// than browse the catalog: Installed, Project sync, Analysis, Settings,
// Update All / Remove All confirmations, and About. Split out of Shell.cs
// so workspace-mutating surfaces live together.
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

        AddIdentityStrip(panel, "installed skills", AccentGreen,
            ("installed", string.IsNullOrEmpty(_searchFilter) ? installed.Length.ToString() : $"{filtered.Length}/{installed.Length}"),
            ("outdated", outdated.Length == 0 ? "[green]0[/]" : $"[yellow]{outdated.Length}[/]"),
            ("tokens", FormatTokenCount(installed.Sum(record => record.Skill.TokenCount))));
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

        // Page toolbar — bulk actions live at the top above the table so they're always on
        // screen, not buried under a 16-row scrolling list. Update is disabled when nothing is
        // outdated; the modal-on-Enter flow handles per-row Update/Reinstall/Remove.
        var installedToolbar = BuildPageToolbar(
            ($"Browse skills", true, () => NavigateTo(HomeAction.BrowseSkills)),
            ($"Update all outdated ({outdated.Length})", outdated.Length > 0, () =>
            {
                var summaryText = UpdateSkillRecords(outdated);
                Toast(summaryText, summaryText.Contains("failed", StringComparison.OrdinalIgnoreCase) ? NotificationSeverity.Danger : NotificationSeverity.Success);
                BuildInstalledPage(ws, panel);
            }),
            ($"Remove all ({installed.Length})", installed.Length > 0, () => ConfirmModal(ws, "Remove all installed skills?",
                $"This removes every catalog skill from {layout.PrimaryRoot.FullName}.",
                () =>
                {
                    var summary = SafeGet(() => new SkillInstaller(skillCatalog).Remove(installed.Select(r => r.Skill).ToArray(), layout), default(SkillRemoveSummary));
                    ToastResult(summary, "Remove failed", summary is null ? string.Empty : $"Removed {summary.RemovedCount} skill(s)");
                    BuildInstalledPage(ws, panel);
                })));
        if (installedToolbar is not null) panel.AddControl(installedToolbar);

        // Real sortable TableControl — columns can be sorted by clicking the header. Per-row
        // foreground color flags outdated rows yellow without needing markup escaping per cell.
        var table = BuildStyledTable("Installed skills (Enter for details)", AccentGreen)
            .AddColumn("Status", TextJustification.Center, width: 8)
            .AddColumn("Skill")
            .AddColumn("Collection")
            .AddColumn("Lane")
            .AddColumn("Installed", TextJustification.Right)
            .AddColumn("Latest", TextJustification.Right)
            .AddColumn("Tokens", TextJustification.Right);
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
                ForegroundColor = record.IsCurrent ? null : OutdatedRowFg,
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
        panel.AddControl(ApplyStyledTableRuntime(table.Build()));
        // Bulk actions live in the page toolbar at the top — no bottom-of-page Button stack.
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

        AddIdentityStrip(panel, "project scan", AccentDeepSkyBlue,
            ("scanned", $"{scan.ProjectFiles.Count} project file(s)"),
            ("frameworks", scan.TargetFrameworks.Count == 0 ? "[grey50]unknown[/]" : Escape(string.Join(", ", scan.TargetFrameworks))),
            ("recommendations", scan.Recommendations.Count.ToString()));

        // Confidence trio — 3 horizontal BarGraphs (high green, med yellow, low grey) so the
        // user can see the shape of the recommendation set without reading numbers. Confidence
        // here IS severity-coded (high = trust, low = noise) so a threshold gradient is the
        // right call rather than a smooth magnitude ramp.
        if (scan.Recommendations.Count > 0)
        {
            var maxConfidence = Math.Max(1, Math.Max(high, Math.Max(med, low)));
            var confidencePanel = new ScrollablePanelControl
            {
                ShowScrollbar = false,
                EnableMouseWheel = false,
            };
            confidencePanel.AddControl(Controls.BarGraph().WithLabel("high").WithLabelWidth(8).WithValue(high).WithMaxValue(maxConfidence).WithValueFormat("0").ShowValue(true).WithFilledColor(AccentGreen).Build());
            confidencePanel.AddControl(Controls.BarGraph().WithLabel("medium").WithLabelWidth(8).WithValue(med).WithMaxValue(maxConfidence).WithValueFormat("0").ShowValue(true).WithFilledColor(AccentYellow).Build());
            confidencePanel.AddControl(Controls.BarGraph().WithLabel("low").WithLabelWidth(8).WithValue(low).WithMaxValue(maxConfidence).WithValueFormat("0").ShowValue(true).WithFilledColor(AccentGrey).Build());
            AddSectionHeader(panel, "confidence", AccentDeepSkyBlue);
            panel.AddControl(confidencePanel);
        }

        if (scan.Recommendations.Count == 0)
        {
            panel.AddControl(BuildNotePanel("recommendations", "[grey50]No package or framework signals matched the catalog. Start with the[/] [green]dotnet[/] [grey50]and[/] [green]modern-csharp[/] [grey50]skills from the Skills page.[/]", AccentDeepSkyBlue));
            return;
        }

        // Split recommendations: new ones install with force=false, outdated ones need
        // force=true so the existing skill directory is overwritten with the latest version.
        // Computed before the toolbar so the "Install all" button can carry the count and a
        // disabled state when there's nothing to install.
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

        var projectToolbar = BuildPageToolbar(
            ($"Install all recommended ({installable.Length})", installable.Length > 0, () =>
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
            }),
            ("Browse installed", true, () => NavigateTo(HomeAction.ManageInstalled)));
        if (projectToolbar is not null) panel.AddControl(projectToolbar);

        // Confidence cell renders as the same ●●● marker as the legacy list so the visual
        // grammar is preserved; the column itself is sortable, and the default sort
        // (Confidence desc) is applied via the row insertion order.
        var table = BuildStyledTable("Recommended skills (Enter to install)", AccentDeepSkyBlue)
            .AddColumn("Confidence", TextJustification.Center, width: 12)
            .AddColumn("Status", width: 12)
            .AddColumn("Skill")
            .AddColumn("Reasons");
        var builtTable = ApplyStyledTableRuntime(table.Build());
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
            builtTable.AddRow(new TableRow(
                marker,
                status,
                ToAlias(recommendation.Skill.Name),
                Escape(string.Join("; ", recommendation.Reasons.Take(2))))
            {
                Tag = recommendation,
            });
        }
        builtTable.RowActivated += (_, _) =>
        {
            if (builtTable.SelectedRow?.Tag is ProjectSkillRecommendation recommendation)
            {
                // Outdated recommendations need force=true: SkillInstaller.Install skips
                // existing skill directories unless forced, so an "update" entry would
                // otherwise be reported as skipped and stay outdated.
                var isOutdated = installedByName.TryGetValue(recommendation.Skill.Name, out var existing) && !existing.IsCurrent;
                var summary2 = SafeGet(() => new SkillInstaller(skillCatalog).Install(new[] { recommendation.Skill }, ResolveSkillLayout(), force: isOutdated), default(SkillInstallSummary));
                ToastResult(summary2, $"Install failed for {ToAlias(recommendation.Skill.Name)}", summary2 is null ? string.Empty : $"{ToAlias(recommendation.Skill.Name)}: {summary2.InstalledCount} written, {summary2.SkippedExisting.Count} skipped");
                BuildProjectPage(ws, panel);
            }
        };
        panel.AddControl(builtTable);
        // Bulk install lives in the page toolbar at the top — no bottom-of-page button.
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

        AddIdentityStrip(panel, "catalog analysis", AccentDeepSkyBlue,
            ("collections", views.Length.ToString()),
            ("skills", skillCatalog.Skills.Count.ToString()),
            ("total tokens", FormatTokenCount(skillCatalog.Skills.Sum(skill => skill.TokenCount))),
            ("package signals", signals.Count.ToString()));

        // The previous collection-card grid (12 BuildBulletPanels in HorizontalGrid columns)
        // was dropped — the same data is shown in the "skills per collection (top 8)" BarGraph
        // section further down, so the grid was redundant and crowded the page with rounded
        // panels masquerading as content cards.

        var heavyTable = BuildStyledTable("Heaviest skills (Enter for details)", AccentDeepSkyBlue)
            .AddColumn("Skill")
            .AddColumn("Collection")
            .AddColumn("Lane")
            .AddColumn("Tokens", TextJustification.Right);
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
        panel.AddControl(ApplyStyledTableRuntime(heavyTable.Build()));

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
            AddSectionHeader(panel, "tokens by skill (top 12)", AccentDeepSkyBlue);
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
            AddSectionHeader(panel, "skills per collection (top 8)", AccentTurquoise);
            panel.AddControl(chart2);
        }

        // Catalog token distribution — the long-tail shape of skill weights across the entire
        // catalog. X axis is skill index sorted by tokens desc, Y is tokens. A small number of
        // mega-skills versus a flat curve is the question this chart answers at a glance.
        if (skillCatalog.Skills.Count >= 2)
        {
            var sortedTokens = skillCatalog.Skills
                .OrderByDescending(s => s.TokenCount)
                .Select(s => (double)s.TokenCount)
                .ToArray();
            var distribution = Controls.LineGraph()
                .WithHeight(7)
                .WithMode(LineGraphMode.Braille)
                .WithMinValue(0)
                .WithMaxValue(sortedTokens.Length == 0 ? 1 : sortedTokens.Max())
                .WithBorder(BorderStyle.Rounded, AccentDeepSkyBlue)
                .WithBackgroundColor(new Color(15, 22, 38))
                .WithYAxisLabels(true, "N0")
                .WithAxisLabelColor(AccentGrey)
                .WithHighLowLabels(true, AccentDeepSkyBlue, AccentGrey)
                .AddSeries("tokens", AccentDeepSkyBlue, "cool")
                .WithData("tokens", sortedTokens)
                .Build();
            AddSectionHeader(panel, "token distribution (long tail)", AccentDeepSkyBlue);
            panel.AddControl(distribution);
        }

        // The previous bottom-of-page bullet list of package signals was dropped —
        // the count is already in the identity strip and per-signal detail belongs on the
        // dedicated Packages page (which is a sortable Table of the same data).
    }

    /// <summary>
    /// A horizontal bar showing one skill's token weight against the chart's max. Uses a smooth
    /// "cool" gradient (blue → cyan): heavy tokens are not severity, they're magnitude — the
    /// previous green→yellow→red gradient read as "warning" against the user instead of
    /// information about the catalog.
    /// </summary>
    private static BarGraphControl BuildSkillTokenBar(SkillEntry skill, int maxTokens)
        => Controls.BarGraph()
            .WithLabel($"{ToAlias(skill.Name)}")
            .WithLabelWidth(28)
            .WithValue(skill.TokenCount)
            .WithMaxValue(maxTokens == 0 ? 1 : maxTokens)
            .WithValueFormat("N0")
            .ShowValue(true)
            .WithSmoothGradient("cool")
            .Build();

    /// <summary>
    /// A horizontal bar showing one collection's skill count against the chart's max. Uses a
    /// custom warm gradient (yellow → orange) so the two Analysis charts are visually
    /// distinguishable at a glance.
    /// </summary>
    private static BarGraphControl BuildCollectionCountBar(CollectionCatalogView view, int maxCount)
        => Controls.BarGraph()
            .WithLabel(view.Collection)
            .WithLabelWidth(28)
            .WithValue(view.SkillCount)
            .WithMaxValue(maxCount == 0 ? 1 : maxCount)
            .WithValueFormat("0")
            .ShowValue(true)
            .WithSmoothGradient(AccentTurquoise, AccentMediumPurple)
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

        AddIdentityStrip(panel, "remove all installed skills", new Color(200, 60, 60),
            ("installed", installed.Count.ToString()));

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

        AddIdentityStrip(panel, "update all outdated skills", AccentYellow,
            ("outdated", outdated.Length.ToString()));

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
        // Settings is a form, so the strip carries an at-a-glance summary of the install targets
        // (the StatusBar already carries project/scope/platform; this adds skill+agent targets
        // because those are the form's subject and aren't surfaced anywhere else).
        AddIdentityStrip(panel, "workspace", AccentDeepSkyBlue,
            ("skill target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"),
            ("agent target", agentStatus.Layout is null ? $"[red]{Escape(agentStatus.Summary)}[/]" : $"[grey50]{Escape(CompactPath(agentStatus.Layout.PrimaryRoot.FullName))}[/]"),
            ("build", ToolVersionInfo.IsDevelopmentBuild ? "[grey50]local development[/]" : "[green]published[/]"));

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
        // About is a static metadata page — keep the original PropertyPanel because the
        // information IS the page (no list/table follows). Identity-strip the title row only.
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

}
