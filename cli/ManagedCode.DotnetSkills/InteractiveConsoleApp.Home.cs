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
}
