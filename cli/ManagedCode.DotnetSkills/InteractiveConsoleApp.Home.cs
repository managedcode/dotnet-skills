// -----------------------------------------------------------------------------
// Home surface — the landing page for the bare `dotnet skills` invocation.
//
// One partial of InteractiveConsoleApp; renders the session card + telemetry grid
// + quick-start hints. Split out of Shell.cs to keep that file under the 800-line
// ceiling once the polish PR adds graphs.
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
    private void BuildHomePage(ConsoleWindowSystem ws, ScrollablePanelControl panel)
    {
        if (_currentPage != null)
        {
            _searchFilter = string.Empty;
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

        AddIdentityStrip(panel, "session", AccentDeepSkyBlue,
            ("catalog", $"{Escape(skillCatalog.SourceLabel)} [grey50]({Escape(skillCatalog.CatalogVersion)})[/]"),
            ("platform", Escape(Session.Agent.ToString())),
            ("scope", Escape(Session.Scope.ToString())),
            ("project", Escape(CompactPath(Session.ProjectDirectory ?? Environment.CurrentDirectory))),
            ("target", $"[grey50]{Escape(CompactPath(layout.PrimaryRoot.FullName))}[/]"));

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

        AddProjectSummary(panel, layout, installed, outdated);
        AddPathsAndShell(panel, layout);
    }

    /// <summary>
    /// What this project needs, and the actions that answer it.
    /// </summary>
    /// <remarks>
    /// Replaces the old "quick start" block, which listed the rail two columns to the left: a
    /// line saying that Skills browses skills is read once and then becomes furniture. "I cd'd
    /// into a repo, what now" is the question this page is actually asked, and the scan already
    /// knows the answer.
    /// </remarks>
    private void AddProjectSummary(
        ScrollablePanelControl panel,
        SkillInstallLayout layout,
        IReadOnlyList<InstalledSkillRecord> installed,
        int outdated)
    {
        AddSectionHeader(panel, "this project", AccentDeepSkyBlue);

        var scan = SafeGet(() => new ProjectSkillRecommender(skillCatalog).Analyze(Session.ProjectDirectory), null);

        if (scan is null || scan.Recommendations.Count == 0)
        {
            panel.AddControl(new MarkupControl(new List<string>
            {
                installed.Count == 0
                    ? "[grey50]No .NET or browser UI signals found here, and nothing installed yet.[/]"
                    : $"[grey50]No signals found here.[/] [grey]{installed.Count}[/] [grey50]skill(s) installed in this target.[/]",
            }));
            return;
        }

        var byName = installed.ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var fresh = scan.Recommendations.Count(r => !byName.ContainsKey(r.Skill.Name));
        var high = scan.Recommendations.Count(r => r.Confidence == RecommendationConfidence.High);
        var medium = scan.Recommendations.Count(r => r.Confidence == RecommendationConfidence.Medium);
        var low = scan.Recommendations.Count - high - medium;
        var maxConfidence = Math.Max(1, Math.Max(high, Math.Max(medium, low)));

        // Left: how much of the catalog is actually here, as a bar rather than a fraction to
        // read. Right: the confidence split, the same three bars the Project page uses, so the
        // two surfaces agree on what a recommendation looks like.
        var coverage = Controls.ProgressBar()
            .WithMaxValue(skillCatalog.Skills.Count)
            .WithValue(installed.Count)
            .Stretch()
            .ShowPercentage()
            .WithColors(AccentSpringGreen, new Color(40, 50, 76))
            .Build();

        var confidence = Controls.ScrollablePanel().Build();
        confidence.Height = ProjectBlockHeight - 2;   // the panel frame around it adds two rows
        confidence.AddControl(Controls.BarGraph().WithLabel("high").WithLabelWidth(8)
            .WithValue(high).WithMaxValue(maxConfidence).WithValueFormat("0").ShowValue(true)
            .WithFilledColor(AccentSpringGreen).Build());
        confidence.AddControl(Controls.BarGraph().WithLabel("medium").WithLabelWidth(8)
            .WithValue(medium).WithMaxValue(maxConfidence).WithValueFormat("0").ShowValue(true)
            .WithFilledColor(AccentYellow).Build());
        confidence.AddControl(Controls.BarGraph().WithLabel("low").WithLabelWidth(8)
            .WithValue(low).WithMaxValue(maxConfidence).WithValueFormat("0").ShowValue(true)
            .WithFilledColor(AccentGrey).Build());

        var left = Controls.ScrollablePanel().Build();
        left.Height = ProjectBlockHeight - 2;   // the panel frame around it adds two rows
        left.AddControl(coverage);
        left.AddControl(new MarkupControl(new List<string>
        {
            $"[grey]{fresh}[/] [grey50]not installed yet[/]",
            $"[grey]{scan.Recommendations.Count(r => r.IsAutoInstallCandidate)}[/] [grey50]auto-install candidate(s)[/]",
            outdated > 0
                ? $"[yellow]{outdated}[/] [grey50]outdated[/]"
                : "[green]all current[/]",
        }));

        panel.AddControl(Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(c => c.Flex(1).Add(BuildPanel($" {installed.Count}/{skillCatalog.Skills.Count} installed here ", AccentSpringGreen, left)))
            .Column(c => c.Flex(1).Add(BuildPanel($" {scan.Recommendations.Count} recommended ", AccentDeepSkyBlue, confidence)))
            .Build());
    }

    /// <summary>
    /// Where this tool writes, and how to drive it. Merged in from the old About page: a user
    /// reading "target .claude/skills" in the strip above should not have to leave the page to
    /// learn where the catalog came from.
    /// </summary>
    private void AddPathsAndShell(ScrollablePanelControl panel, SkillInstallLayout layout)
    {
        AddSectionHeader(panel, "where things go", AccentTurquoise);
        panel.AddControl(new MarkupControl(new List<string>
        {
            $"  [grey]skills target [/]  [grey50]{Escape(layout.PrimaryPath)}[/]",
            $"  [grey]catalog source[/]  [grey50]{Escape(skillCatalog.CatalogRoot.FullName)}[/]",
            $"  [grey]agent catalog [/]  [grey50]{Escape(agentCatalog.CatalogRoot.FullName)}[/]",
        }));

        AddSectionHeader(panel, "keys", AccentMediumPurple);

        panel.AddControl(new MarkupControl(new List<string>
        {
            "  [grey]Ctrl+P[/] [grey50]command palette[/]      [grey50]·[/]   [grey]/[/] [grey50]filter the current list[/]",
            "  [grey]Ctrl+R[/] [grey50]refresh the catalog[/]  [grey50]·[/]   [grey]Esc[/] [grey50]or[/] [grey]Ctrl+Q[/] [grey50]quit[/]",
        }));

        AddSectionHeader(panel, "this tool", AccentGrey);

        // What a user of this shell would want to know about the thing they are running: which
        // version, whether a newer one exists, and that there is a command line behind the
        // windows. The old wording ("Every CLI sub-command is unchanged") answered a question
        // only someone who knew a previous release would think to ask.
        var version = $"  [grey]{Escape(ToolIdentity.DisplayCommand)}[/] [grey50]{Escape(ToolVersionInfo.CurrentVersion)}[/]"
            + (ToolVersionInfo.IsDevelopmentBuild ? " [grey50]· local build[/]" : string.Empty);

        panel.AddControl(new MarkupControl(new List<string>
        {
            toolUpdateStatus?.HasUpdate == true
                ? version + $"   [grey50]·[/]   [yellow]{Escape(toolUpdateStatus.LatestVersion ?? "?")} available[/]"
                    + $" [grey50]·[/] [green]{Escape(GlobalToolUpdateCommand)}[/]"
                : version + "   [grey50]·[/]   [green]up to date[/]",
            $"  [grey50]Everything here is available from the command line too:[/]"
                + $" [green]{Escape(ToolIdentity.DisplayCommand)} --help[/]",
            // Restores the mention the About page carried before Home absorbed it: "This is the
            // SharpConsoleUI command center". No copyright line - the repo never had one in the
            // UI, and adding one is the maintainers' call, not ours.
            "  [grey50]UI built with SharpConsoleUI[/]",
        }));
    }

    /// <summary>
    /// Rows each half of "this project" occupies, so the two sit level. Without it the coverage
    /// column is three rows and the confidence panel five, and the pair reads as two unrelated
    /// blocks rather than one.
    /// </summary>
    private const int ProjectBlockHeight = 6;

    /// <summary>A titled rounded panel around one control, for the home page's paired charts.</summary>
    private static PanelControl BuildPanel(string header, Color accent, IWindowControl content) =>
        Controls.Panel()
            .WithHeader(header)
            .HeaderLeft()
            .Rounded()
            .WithBorderColor(accent)
            .AddControl(content)
            .StretchHorizontal()
            .Build();

}
