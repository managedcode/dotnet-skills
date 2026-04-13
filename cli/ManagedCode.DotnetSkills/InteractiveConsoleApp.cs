using ManagedCode.DotnetSkills.Runtime;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Panel;
using SharpConsoleUI.Rendering;

namespace ManagedCode.DotnetSkills;

internal sealed class InteractiveConsoleApp
{
    private readonly IInteractivePrompts prompts;
    private readonly Func<bool, string?, string?, bool, Task<SkillCatalogPackage>> loadSkillCatalogAsync;
    private readonly Func<AgentCatalogPackage> loadAgentCatalog;
    private readonly Func<string?, Task> maybeShowToolUpdateAsync;
    private readonly string? cachePath;
    private readonly string? catalogVersion;
    private SkillCatalogPackage skillCatalog = null!;
    private AgentCatalogPackage agentCatalog = null!;

    public InteractiveConsoleApp(
        IInteractivePrompts? prompts = null,
        Func<bool, string?, string?, bool, Task<SkillCatalogPackage>>? loadSkillCatalogAsync = null,
        Func<AgentCatalogPackage>? loadAgentCatalog = null,
        Func<string?, Task>? maybeShowToolUpdateAsync = null,
        string? cachePath = null,
        string? catalogVersion = null,
        bool bundledOnly = false,
        AgentPlatform initialAgent = AgentPlatform.Auto,
        InstallScope initialScope = InstallScope.Project,
        string? projectDirectory = null)
    {
        this.prompts = prompts ?? new SharpConsoleInteractivePrompts();
        this.loadSkillCatalogAsync = loadSkillCatalogAsync ?? Program.ResolveCatalogForInstallAsync;
        this.loadAgentCatalog = loadAgentCatalog ?? AgentCatalogPackage.LoadBundled;
        this.maybeShowToolUpdateAsync = maybeShowToolUpdateAsync ?? Program.MaybeShowToolUpdateAsync;
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
        await maybeShowToolUpdateAsync(cachePath);
        await LoadCatalogsAsync(refreshCatalog: false);

        while (true)
        {
            try
            {
                RenderDashboard();

                var action = prompts.Select(
                    "What would you like to do?",
                    new[]
                    {
                        new MenuOption<HomeAction>("Primary - sync from project", HomeAction.SyncProject),
                        new MenuOption<HomeAction>("Skills - browse and install", HomeAction.InstallSkills),
                        new MenuOption<HomeAction>("Bundles - grouped installs", HomeAction.InstallSkillStack),
                        new MenuOption<HomeAction>("Installed - control skills", HomeAction.ManageInstalled),
                        new MenuOption<HomeAction>("Agents - control agents", HomeAction.Agents),
                        new MenuOption<HomeAction>("Workspace - destination and catalog", HomeAction.Settings),
                        new MenuOption<HomeAction>("Exit", HomeAction.Exit),
                    },
                    option => option.Label);

                switch (action.Value)
                {
                    case HomeAction.SyncProject:
                        ShowProjectSync();
                        break;
                    case HomeAction.InstallSkillStack:
                        ShowPackages();
                        break;
                    case HomeAction.InstallSkills:
                        await ShowCatalogSkillsAsync();
                        break;
                    case HomeAction.ManageInstalled:
                        ShowInstalledSkills();
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

    private async Task LoadCatalogsAsync(bool refreshCatalog)
    {
        skillCatalog = await loadSkillCatalogAsync(Session.BundledOnly, cachePath, catalogVersion, refreshCatalog);
        agentCatalog = loadAgentCatalog();
    }

    private async Task RefreshCatalogAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[dim]Refreshing catalog...[/]");
        await LoadCatalogsAsync(refreshCatalog: true);

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[green]\u2714[/] [dim]catalog[/]"), new Markup(Escape(skillCatalog.CatalogVersion)));
        grid.AddRow(new Markup("[dim]source[/]"), new Markup(Escape(skillCatalog.SourceLabel)));
        grid.AddRow(new Markup("[dim]skills[/]"), new Markup(skillCatalog.Skills.Count.ToString()));
        grid.AddRow(new Markup("[dim]bundles[/]"), new Markup(skillCatalog.Packages.Count.ToString()));
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]refreshed[/]").Border(BoxBorder.Rounded).Expand());
        prompts.Pause("Press any key to continue...");
    }

    private void RenderDashboard()
    {
        AnsiConsole.Clear();

        // Minimal header — Claude Code-inspired understated identity
        AnsiConsole.MarkupLine("[bold deepskyblue1]dotnet skills[/] [dim]v{0}[/]", Escape(ToolVersionInfo.CurrentVersion));
        AnsiConsole.MarkupLine("[dim].NET skill catalog for AI-assisted development[/]");
        AnsiConsole.WriteLine();

        var skillLayout = ResolveSkillLayout();
        var skillInstaller = new SkillInstaller(skillCatalog);
        var installedSkills = skillInstaller.GetInstalledSkills(skillLayout);
        var outdatedSkills = installedSkills.Count(record => !record.IsCurrent);
        var agentStatus = ResolveAgentStatus();

        var ratio = skillCatalog.Skills.Count > 0 ? (double)installedSkills.Count / skillCatalog.Skills.Count : 0;
        var barWidth = 20;
        var filled = (int)(ratio * barWidth);
        var bar = new string('\u2588', filled) + new string('\u2591', barWidth - filled);

        var overview = new Grid();
        overview.AddColumn(new GridColumn().NoWrap());
        overview.AddColumn();
        overview.AddRow(new Markup("[dim]catalog[/]"), new Markup($"{Escape(skillCatalog.SourceLabel)} [dim]({Escape(skillCatalog.CatalogVersion)})[/]"));
        overview.AddRow(new Markup("[dim]session[/]"), new Markup($"{Escape(Session.Agent.ToString())} [dim]/[/] {Escape(Session.Scope.ToString())}"));
        overview.AddRow(new Markup("[dim]project[/]"), new Markup($"[dim]{Escape(Program.ResolveProjectRoot(Session.ProjectDirectory))}[/]"));
        overview.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(skillLayout.PrimaryRoot.FullName)}[/]"));
        overview.AddRow(new Markup("[dim]skills[/]"), new Markup($"[green]{bar}[/] {installedSkills.Count}/{skillCatalog.Skills.Count}" + (outdatedSkills > 0 ? $" [yellow]({outdatedSkills} outdated)[/]" : "")));
        overview.AddRow(new Markup("[dim]bundles[/]"), new Markup($"{skillCatalog.Packages.Count} available"));
        overview.AddRow(new Markup("[dim]agents[/]"), new Markup($"{agentCatalog.Agents.Count} [dim]({Escape(agentStatus.Summary)})[/]"));
        AnsiConsole.Write(new Panel(overview).Header("[deepskyblue1]workspace[/]").Border(BoxBorder.Rounded).Expand());

        // Compact quick-reference hints
        AnsiConsole.MarkupLine("[dim]  Primary: sync project  |  Skills: install/remove/repair/move  |  Bundles: grouped installs  |  Agents: install/remove/repair/move[/]");
        AnsiConsole.WriteLine();
    }

    private async Task ShowCatalogSkillsAsync()
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var scopeInventory = Program.BuildScopeInventory(layout, Session.ProjectDirectory, installer, installedSkills);

            AnsiConsole.Clear();
            ConsoleUi.RenderList(
                skillCatalog,
                layout,
                installedSkills,
                scopeInventory,
                layout.Scope == InstallScope.Project ? Program.ResolveProjectRoot(Session.ProjectDirectory) : null,
                showInstalledSection: false,
                showAvailableSection: true);

            var actions = new List<MenuOption<SkillCatalogAction>>
            {
                new("Inspect a skill", SkillCatalogAction.Inspect),
                new("Install skills", SkillCatalogAction.Install),
            };

            if (installedSkills.Any(record => !record.IsCurrent))
            {
                actions.Add(new MenuOption<SkillCatalogAction>("Update outdated skills", SkillCatalogAction.UpdateOutdated));
            }

            actions.Add(new MenuOption<SkillCatalogAction>("Back", SkillCatalogAction.Back));

            var action = prompts.Select("Catalog actions", actions, option => option.Label);
            switch (action.Value)
            {
                case SkillCatalogAction.Inspect:
                {
                    var selectedSkill = prompts.Select(
                        "Inspect a skill",
                        skillCatalog.Skills.OrderBy(skill => skill.Name, StringComparer.Ordinal).ToArray(),
                        skill => BuildSkillChoiceLabel(skill, installedSkills));
                    ShowSkillDetail(selectedSkill);
                    break;
                }
                case SkillCatalogAction.Install:
                {
                    var installableSkills = skillCatalog.Skills
                        .Where(skill => installedSkills.All(record => !string.Equals(record.Skill.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                        .ToArray();

                    if (installableSkills.Length == 0)
                    {
                        RenderInfo("Everything in this catalog is already installed in the current target.");
                        break;
                    }

                    var selectedSkills = prompts.MultiSelect(
                        "Install skills",
                        installableSkills,
                        skill => $"{ToAlias(skill.Name)} [{skill.Category}]");
                    if (selectedSkills.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Install {selectedSkills.Count} skill(s) into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallSkills(selectedSkills, force: false);
                    }

                    break;
                }
                case SkillCatalogAction.UpdateOutdated:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent)
                        .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    if (outdatedSkills.Length == 0)
                    {
                        RenderInfo("No outdated skills are installed in this target.");
                        break;
                    }

                    var selected = prompts.MultiSelect(
                        "Update outdated skills",
                        outdatedSkills,
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion} -> {record.Skill.Version})");
                    if (selected.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Update {selected.Count} skill(s) in {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        UpdateSkills(selected);
                    }

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
            ConsoleUi.RenderList(
                skillCatalog,
                layout,
                installedSkills,
                scopeInventory,
                layout.Scope == InstallScope.Project ? Program.ResolveProjectRoot(Session.ProjectDirectory) : null,
                showInstalledSection: true,
                showAvailableSection: false);

            var actions = new List<MenuOption<InstalledSkillsAction>>
            {
                new("Inspect an installed skill", InstalledSkillsAction.Inspect),
            };

            if (installedSkills.Count > 0)
            {
                actions.Add(new MenuOption<InstalledSkillsAction>("Repair/optimize installed skills", InstalledSkillsAction.Repair));
                actions.Add(new MenuOption<InstalledSkillsAction>("Copy or move skills to another target", InstalledSkillsAction.CopyOrMove));
                actions.Add(new MenuOption<InstalledSkillsAction>("Remove installed skills", InstalledSkillsAction.Remove));
            }

            if (installedSkills.Any(record => !record.IsCurrent))
            {
                actions.Add(new MenuOption<InstalledSkillsAction>("Update outdated skills", InstalledSkillsAction.Update));
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
                case InstalledSkillsAction.Update:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent)
                        .OrderBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    if (outdatedSkills.Length == 0)
                    {
                        RenderInfo("No outdated skills are installed in this target.");
                        break;
                    }

                    var selected = prompts.MultiSelect(
                        "Update outdated skills",
                        outdatedSkills,
                        record => $"{ToAlias(record.Skill.Name)} ({record.InstalledVersion} -> {record.Skill.Version})");
                    if (selected.Count == 0)
                    {
                        break;
                    }

                    if (prompts.Confirm($"Update {selected.Count} skill(s) in {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        UpdateSkills(selected);
                    }

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
                    if (prompts.Confirm($"Install {ToAlias(skill.Name)} into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallSkills([skill], force: false);
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
            AnsiConsole.Clear();
            ConsoleUi.RenderPackageList(skillCatalog);

            var action = prompts.Select(
                "Bundle actions",
                new[]
                {
                    new MenuOption<PackageAction>("Inspect a bundle", PackageAction.Inspect),
                    new MenuOption<PackageAction>("Install bundles", PackageAction.Install),
                    new MenuOption<PackageAction>("Back", PackageAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case PackageAction.Inspect:
                {
                    if (skillCatalog.Packages.Count == 0)
                    {
                        RenderInfo("No bundles are available in this catalog version yet.");
                        break;
                    }

                    var selectedPackage = prompts.Select(
                        "Inspect a bundle",
                        skillCatalog.Packages.OrderBy(package => package.Name, StringComparer.Ordinal).ToArray(),
                        package => $"{package.Name} ({package.Skills.Count} skills)");
                    ShowPackageDetail(selectedPackage);
                    break;
                }
                case PackageAction.Install:
                {
                    if (skillCatalog.Packages.Count == 0)
                    {
                        RenderInfo("No bundles are available in this catalog version yet.");
                        break;
                    }

                    var selectedPackages = prompts.MultiSelect(
                        "Install bundles",
                        skillCatalog.Packages.OrderBy(package => package.Name, StringComparer.Ordinal).ToArray(),
                        package => $"{package.Name} ({package.Skills.Count} skills)");
                    if (selectedPackages.Count == 0)
                    {
                        break;
                    }

                    var layout = ResolveSkillLayout();
                    if (prompts.Confirm($"Install {selectedPackages.Count} bundle(s) into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallPackages(selectedPackages);
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
                "Bundle actions",
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
                    if (prompts.Confirm($"Install bundle {package.Name} into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallPackages([package]);
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

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]project[/]"), new Markup($"[dim]{Escape(plan.ScanResult.ProjectRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]scanned[/]"), new Markup($"{plan.ScanResult.ProjectFiles.Count} .csproj files"));
        grid.AddRow(new Markup("[dim]matched[/]"), new Markup($"{plan.DesiredSkills.Count} skills"));
        grid.AddRow(new Markup("[dim]new[/]"), new Markup(newSkills.Length > 0 ? $"[green]{newSkills.Length}[/]" : "0"));
        grid.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]project sync[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        var table = new Table().Expand();
        table.AddColumn("Skill");
        table.AddColumn("Confidence");
        table.AddColumn("Status");

        foreach (var recommendation in plan.ScanResult.Recommendations
                     .Where(recommendation => recommendation.IsAutoInstallCandidate)
                     .OrderByDescending(recommendation => recommendation.Confidence)
                     .ThenBy(recommendation => recommendation.Skill.Name, StringComparer.Ordinal))
        {
            var isInstalled = installedBefore.ContainsKey(recommendation.Skill.Name);
            table.AddRow(
                Escape(ToAlias(recommendation.Skill.Name)),
                Escape(recommendation.Confidence.ToString()),
                isInstalled ? "[dim]already installed[/]" : "[green]new[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (newSkills.Length == 0)
        {
            AnsiConsole.MarkupLine("[dim]All matched skills are already installed.[/]");
            prompts.Pause("Press any key to continue...");
            return;
        }

        if (prompts.Confirm($"Install {newSkills.Length} new skill(s) into {layout.PrimaryRoot.FullName}?", defaultValue: true))
        {
            InstallSkills(plan.DesiredSkills, force: false);
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
            if (layout is null)
            {
                RenderAgentFallback(layoutError ?? "No agent target is available for the current session.");
            }
            else
            {
                ConsoleUi.RenderAgentList(agentCatalog, layout, installedAgents);
            }

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
                    ConsoleUi.RenderUsage();
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
        ConsoleUi.RenderInstallSummary(skillCatalog, layout, rows, summary);
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
        ConsoleUi.RenderRemoveSummary(skillCatalog, layout, rows, summary.RemovedCount, null);
        if (pause)
        {
            prompts.Pause("Press any key to return to the interactive shell...");
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
        ConsoleUi.RenderUpdateSummary(skillCatalog, layout, rows, rows.Length, null);
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
        ConsoleUi.RenderAgentInstallSummary(agentCatalog, layout, agents, summary);
        prompts.Pause("Press any key to return to the interactive shell...");
    }

    private void RemoveAgents(IReadOnlyList<AgentEntry> agents, AgentInstallLayout layout)
    {
        var installer = new AgentInstaller(agentCatalog);
        var summary = installer.Remove(agents, layout);

        AnsiConsole.Clear();
        ConsoleUi.RenderAgentRemoveSummary(agentCatalog, layout, agents, summary);
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

    private void RenderSkillDetailPanel(SkillEntry skill, InstalledSkillRecord? installed, SkillInstallLayout layout)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]alias[/]"), new Markup(Escape(ToAlias(skill.Name))));
        grid.AddRow(new Markup("[dim]skill[/]"), new Markup($"[dim]{Escape(skill.Name)}[/]"));
        grid.AddRow(new Markup("[dim]category[/]"), new Markup(Escape(skill.Category)));
        grid.AddRow(new Markup("[dim]version[/]"), new Markup(Escape(skill.Version)));
        grid.AddRow(new Markup("[dim]status[/]"), new Markup(installed is null ? "[dim]not installed[/]" : $"{Escape(installed.InstalledVersion)} {(installed.IsCurrent ? "[green](current)[/]" : "[yellow](update available)[/]")}"));
        grid.AddRow(new Markup("[dim]compat[/]"), new Markup(Escape(skill.Compatibility)));
        grid.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]install[/]"), new Markup($"[green]{Escape($"dotnet skills install {ToAlias(skill.Name)}")}[/]"));
        AnsiConsole.Write(new Panel(grid).Header($"[deepskyblue1]{Escape(ToAlias(skill.Name))}[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(skill.Description))).Border(BoxBorder.Rounded).Expand());
    }

    private void RenderPackageDetailPanel(SkillPackageEntry package)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]bundle[/]"), new Markup(Escape(package.Name)));
        grid.AddRow(new Markup("[dim]type[/]"), new Markup(Escape(package.Kind)));
        grid.AddRow(new Markup("[dim]category[/]"), new Markup(Escape(package.SourceCategory)));
        grid.AddRow(new Markup("[dim]skills[/]"), new Markup(package.Skills.Count.ToString()));
        grid.AddRow(new Markup("[dim]install[/]"), new Markup($"[green]{Escape($"dotnet skills install bundle {package.Name}")}[/]"));
        AnsiConsole.Write(new Panel(grid).Header($"[deepskyblue1]{Escape(package.Name)}[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(package.Description))).Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        var table = new Table().Expand();
        table.Title = new TableTitle("Included skills");
        table.AddColumn("Alias");
        table.AddColumn("Skill");

        foreach (var skillName in package.Skills.OrderBy(name => name, StringComparer.Ordinal))
        {
            table.AddRow(Escape(ToAlias(skillName)), Escape(skillName));
        }

        AnsiConsole.Write(table);
    }

    private void RenderAgentDetailPanel(AgentEntry agent, AgentInstallLayout? layout, string? layoutError, bool installed)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]alias[/]"), new Markup(Escape(ToAlias(agent.Name))));
        grid.AddRow(new Markup("[dim]agent[/]"), new Markup($"[dim]{Escape(agent.Name)}[/]"));
        grid.AddRow(new Markup("[dim]model[/]"), new Markup(Escape(agent.Model)));
        grid.AddRow(new Markup("[dim]skills[/]"), new Markup(agent.Skills.Count == 0 ? "-" : Escape(string.Join(", ", agent.Skills.Select(ToAlias)))));
        grid.AddRow(new Markup("[dim]tools[/]"), new Markup(Escape(agent.Tools)));
        grid.AddRow(new Markup("[dim]status[/]"), new Markup(layout is null ? "[dim]target unavailable[/]" : installed ? "[green]installed[/]" : "[dim]not installed[/]"));
        grid.AddRow(new Markup("[dim]target[/]"), new Markup(layout is null ? $"[dim]{Escape(layoutError ?? "Unavailable")}[/]" : $"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]install[/]"), new Markup($"[green]{Escape($"dotnet skills agent install {ToAlias(agent.Name)} --agent {Session.Agent.ToString().ToLowerInvariant()}")}[/]"));
        AnsiConsole.Write(new Panel(grid).Header($"[deepskyblue1]{Escape(ToAlias(agent.Name))}[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(agent.Description))).Border(BoxBorder.Rounded).Expand());
    }

    private void RenderSessionTargetPanel()
    {
        var skillLayout = ResolveSkillLayout();
        var agentLayout = TryResolveAgentLayout(out var agentLayoutError);

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]platform[/]"), new Markup(Escape(Session.Agent.ToString())));
        grid.AddRow(new Markup("[dim]scope[/]"), new Markup(Escape(Session.Scope.ToString())));
        grid.AddRow(new Markup("[dim]project[/]"), new Markup($"[dim]{Escape(Program.ResolveProjectRoot(Session.ProjectDirectory))}[/]"));
        grid.AddRow(new Markup("[dim]skill target[/]"), new Markup($"[dim]{Escape(skillLayout.PrimaryRoot.FullName)}[/]"));
        grid.AddRow(new Markup("[dim]agent target[/]"), new Markup($"[dim]{(agentLayout is null ? Escape(agentLayoutError ?? "Unavailable") : Escape(agentLayout.PrimaryRoot.FullName))}[/]"));
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]install destination[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[dim]Choose the platform and scope that control where skills and agents are installed. Keep Auto for fallback behavior.[/]");
    }

    private void RenderAgentFallback(string message)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]session[/]"), new Markup($"{Escape(Session.Agent.ToString())} [dim]/[/] {Escape(Session.Scope.ToString())}"));
        grid.AddRow(new Markup("[dim]status[/]"), new Markup($"[dim]{Escape(message)}[/]"));
        AnsiConsole.Write(new Panel(grid).Header("[yellow]agent target unavailable[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        var table = new Table().Expand();
        table.Title = new TableTitle("Bundled agents");
        table.AddColumn("Agent");
        table.AddColumn("Skills");
        table.AddColumn("Model");

        foreach (var agent in agentCatalog.Agents.OrderBy(entry => entry.Name, StringComparer.Ordinal))
        {
            table.AddRow(
                Escape(ToAlias(agent.Name)),
                agent.Skills.Count == 0 ? "-" : Escape(string.Join(", ", agent.Skills.Take(4).Select(ToAlias))),
                Escape(agent.Model));
        }

        AnsiConsole.Write(table);
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
            return $"{ToAlias(skill.Name)} [{skill.Category}]";
        }

        return installed.IsCurrent
            ? $"{ToAlias(skill.Name)} [{skill.Category}] (installed {installed.InstalledVersion})"
            : $"{ToAlias(skill.Name)} [{skill.Category}] (update {installed.InstalledVersion} -> {skill.Version})";
    }

    private static string ToAlias(string value) => value.StartsWith("dotnet-", StringComparison.OrdinalIgnoreCase)
        ? value["dotnet-".Length..]
        : value;

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

    IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull;

    bool Confirm(string title, bool defaultValue);

    void Pause(string title);
}

internal sealed class SharpConsoleInteractivePrompts : IInteractivePrompts
{
    public T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (CanUseSharpConsoleUi()
            && TrySelectWithSharpConsoleUi(title, choices, formatter, compact: false, out var selected))
        {
            return selected;
        }

        return SelectPlainText(title, choices, formatter);
    }

    public IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (CanUseSharpConsoleUi()
            && TryMultiSelectWithSharpConsoleUi(title, choices, formatter, out var selected))
        {
            return selected;
        }

        return MultiSelectPlainText(title, choices, formatter);
    }

    public bool Confirm(string title, bool defaultValue)
    {
        var choices = defaultValue
            ? new[] { new MenuOption<bool>("Yes", true), new MenuOption<bool>("No", false) }
            : new[] { new MenuOption<bool>("No", false), new MenuOption<bool>("Yes", true) };

        if (CanUseSharpConsoleUi()
            && TrySelectWithSharpConsoleUi(title, choices, option => option.Label, compact: true, out var selected))
        {
            return selected.Value;
        }

        return ConfirmPlainText(title, defaultValue);
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

    private static bool TrySelectWithSharpConsoleUi<T>(
        string title,
        IReadOnlyList<T> choices,
        Func<T, string> formatter,
        bool compact,
        out T selected) where T : notnull
    {
        selected = default!;
        if (choices.Count == 0)
        {
            return false;
        }

        if (!compact && TrySelectDashboardWithSharpConsoleUi(title, choices, formatter, out selected))
        {
            return true;
        }

        var completed = false;
        var escapeChoice = FindEscapeChoice(choices, formatter);
        var selectedValue = default(T)!;

        try
        {
            var windowSystem = CreatePromptWindowSystem();
            var items = choices
                .Select((choice, index) => new ListItem(formatter(choice), icon: index == 0 ? ">" : " ") { Tag = index })
                .ToArray();

            var list = Controls.List(title)
                .AddItems(items)
                .MaxVisibleItems(Math.Min(Math.Max(choices.Count, 4), 18))
                .WithColors(Color.White, new Color(8, 16, 28))
                .WithHighlightColors(Color.White, new Color(0, 120, 180))
                .OnItemActivated((sender, item, window) =>
                {
                    if (item.Tag is int index && index >= 0 && index < choices.Count)
                    {
                        selectedValue = choices[index];
                        completed = true;
                        windowSystem.Shutdown(0);
                    }
                })
                .Build();
            list.SelectedIndex = 0;

            new WindowBuilder(windowSystem)
                .WithTitle(title)
                .WithSize(ResolvePromptWidth(choices.Select(formatter)), ResolvePromptHeight(choices.Count, extraRows: 8))
                .Centered()
                .WithBackgroundGradient(
                    ColorGradient.FromColors(new Color(5, 15, 30), new Color(0, 36, 52)),
                    GradientDirection.Vertical)
                .WithColors(Color.White, new Color(5, 15, 30))
                .WithBorderStyle(BorderStyle.Rounded)
                .Resizable(false)
                .Movable(false)
                .AddControl(Controls.Markup()
                    .AddLine("[bold cyan]dotnet skills command center[/]")
                    .AddLine("[dim]Use Up/Down and Enter. Esc goes back when available.[/]")
                    .AddEmptyLine()
                    .Build())
                .AddControl(list)
                .OnKeyPressed((sender, args) =>
                {
                    if (args.KeyInfo.Key != ConsoleKey.Escape)
                    {
                        return;
                    }

                    if (escapeChoice >= 0)
                    {
                        selectedValue = choices[escapeChoice];
                        completed = true;
                    }

                    args.Handled = true;
                    windowSystem.Shutdown(completed ? 0 : 1);
                })
                .BuildAndShow();

            windowSystem.Run();
            selected = selectedValue;
            AnsiConsole.Clear();
            return completed;
        }
        catch
        {
            AnsiConsole.Clear();
            return false;
        }
    }

    private static bool TryMultiSelectWithSharpConsoleUi<T>(
        string title,
        IReadOnlyList<T> choices,
        Func<T, string> formatter,
        out IReadOnlyList<T> selected) where T : notnull
    {
        selected = [];
        if (choices.Count == 0)
        {
            return true;
        }

        if (TryMultiSelectDashboardWithSharpConsoleUi(title, choices, formatter, out selected))
        {
            return true;
        }

        var completed = false;
        IReadOnlyList<T> selectedValues = [];

        try
        {
            var windowSystem = CreatePromptWindowSystem();
            var items = choices
                .Select((choice, index) => new ListItem(formatter(choice), icon: " ") { Tag = index })
                .ToArray();

            var list = Controls.List(title)
                .AddItems(items)
                .MaxVisibleItems(Math.Min(Math.Max(choices.Count, 4), 18))
                .WithCheckboxMode()
                .WithColors(Color.White, new Color(8, 16, 28))
                .WithHighlightColors(Color.White, new Color(0, 120, 180))
                .OnItemActivated((sender, item, window) =>
                {
                    item.IsChecked = !item.IsChecked;
                })
                .Build();
            list.SelectedIndex = 0;

            new WindowBuilder(windowSystem)
                .WithTitle(title)
                .WithSize(ResolvePromptWidth(choices.Select(formatter)), ResolvePromptHeight(choices.Count, extraRows: 10))
                .Centered()
                .WithBackgroundGradient(
                    ColorGradient.FromColors(new Color(5, 15, 30), new Color(0, 36, 52)),
                    GradientDirection.Vertical)
                .WithColors(Color.White, new Color(5, 15, 30))
                .WithBorderStyle(BorderStyle.Rounded)
                .Resizable(false)
                .Movable(false)
                .AddControl(Controls.Markup()
                    .AddLine("[bold cyan]dotnet skills command center[/]")
                    .AddLine("[dim]Space or Enter toggles. F10 accepts. Esc cancels.[/]")
                    .AddEmptyLine()
                    .Build())
                .AddControl(list)
                .OnKeyPressed((sender, args) =>
                {
                    if (args.KeyInfo.Key == ConsoleKey.F10)
                    {
                        selectedValues = list.GetCheckedItems()
                            .Select(item => item.Tag is int index && index >= 0 && index < choices.Count ? choices[index] : default)
                            .Where(item => item is not null)
                            .Cast<T>()
                            .ToArray();
                        completed = true;
                        args.Handled = true;
                        windowSystem.Shutdown(0);
                    }
                    else if (args.KeyInfo.Key == ConsoleKey.Escape)
                    {
                        selectedValues = [];
                        completed = true;
                        args.Handled = true;
                        windowSystem.Shutdown(0);
                    }
                })
                .BuildAndShow();

            windowSystem.Run();
            selected = selectedValues;
            AnsiConsole.Clear();
            return completed;
        }
        catch
        {
            AnsiConsole.Clear();
            return false;
        }
    }

    private static bool TrySelectDashboardWithSharpConsoleUi<T>(
        string title,
        IReadOnlyList<T> choices,
        Func<T, string> formatter,
        out T selected) where T : notnull
    {
        selected = default!;
        if (!CanUseDashboardChrome())
        {
            return false;
        }

        var labels = choices.Select(formatter).ToArray();
        var completed = false;
        var selectedValue = default(T)!;
        var escapeChoice = FindEscapeChoice(choices, formatter);

        try
        {
            var windowSystem = CreatePromptWindowSystem();
            var detailMarkup = Controls.Markup()
                .AddLines(BuildSelectionDetailLines(title, labels[0]).ToArray())
                .WithMargin(2, 1, 2, 0)
                .FillVertical()
                .Build();
            var statusBar = Controls.Markup(BuildSelectStatusLine(title, choices.Count))
                .StickyBottom()
                .Build();

            var items = choices
                .Select((choice, index) => new ListItem(labels[index], icon: index == 0 ? ">" : " ") { Tag = index })
                .ToArray();

            var list = Controls.List("Actions")
                .AddItems(items)
                .MaxVisibleItems(Math.Min(Math.Max(choices.Count, 6), 24))
                .WithAutoHighlightOnFocus(true)
                .WithHoverHighlighting(true)
                .WithScrollbarVisibility(ScrollbarVisibility.Auto)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .WithColors(Color.White, new Color(18, 21, 24))
                .WithHighlightColors(Color.Cyan1, new Color(78, 82, 86))
                .OnSelectionChanged((sender, index) =>
                {
                    if (index >= 0 && index < labels.Length)
                    {
                        detailMarkup.SetContent(BuildSelectionDetailLines(title, labels[index]));
                    }
                })
                .OnItemActivated((sender, item, window) =>
                {
                    if (item.Tag is int index && index >= 0 && index < choices.Count)
                    {
                        selectedValue = choices[index];
                        completed = true;
                        windowSystem.Shutdown(0);
                    }
                })
                .Build();
            list.SelectedIndex = 0;

            var detailPanel = Controls.ScrollablePanel()
                .AddControl(detailMarkup)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .Build();

            var grid = Controls.HorizontalGrid()
                .Column(column => column.Width(ResolveSidebarWidth(labels)).Add(list))
                .Column(column => column.Flex().Add(detailPanel))
                .WithSplitterAfter(0)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .Build();

            var header = Controls.Markup()
                .AddLine(BuildDashboardHeader(title, choices.Count))
                .StickyTop()
                .Build();

            new WindowBuilder(windowSystem)
                .WithTitle("dotnet skills")
                .WithSize(ResolveDashboardWidth(), ResolveDashboardHeight())
                .Centered()
                .WithBackgroundGradient(
                    ColorGradient.FromColors(new Color(12, 14, 16), new Color(22, 25, 28)),
                    GradientDirection.Vertical)
                .WithColors(Color.White, new Color(12, 14, 16))
                .WithBorderStyle(BorderStyle.Single)
                .Resizable(false)
                .Movable(false)
                .AddControls(header, grid, statusBar)
                .OnKeyPressed((sender, args) =>
                {
                    if (args.KeyInfo.Key != ConsoleKey.Escape)
                    {
                        return;
                    }

                    if (escapeChoice >= 0)
                    {
                        selectedValue = choices[escapeChoice];
                        completed = true;
                    }

                    args.Handled = true;
                    windowSystem.Shutdown(completed ? 0 : 1);
                })
                .BuildAndShow();

            windowSystem.Run();
            selected = selectedValue;
            AnsiConsole.Clear();
            return completed;
        }
        catch
        {
            AnsiConsole.Clear();
            return false;
        }
    }

    private static bool TryMultiSelectDashboardWithSharpConsoleUi<T>(
        string title,
        IReadOnlyList<T> choices,
        Func<T, string> formatter,
        out IReadOnlyList<T> selected) where T : notnull
    {
        selected = [];
        if (!CanUseDashboardChrome())
        {
            return false;
        }

        var labels = choices.Select(formatter).ToArray();
        var completed = false;
        IReadOnlyList<T> selectedValues = [];

        try
        {
            var windowSystem = CreatePromptWindowSystem();
            var detailMarkup = Controls.Markup()
                .AddLines(BuildMultiSelectDetailLines(title, labels[0], selectedCount: 0).ToArray())
                .WithMargin(2, 1, 2, 0)
                .FillVertical()
                .Build();
            var statusBar = Controls.Markup(BuildMultiSelectStatusLine(title, selectedCount: 0, choices.Count))
                .StickyBottom()
                .Build();

            var items = choices
                .Select((choice, index) => new ListItem(labels[index], icon: " ") { Tag = index })
                .ToArray();

            ListControl? listControl = null;
            var list = Controls.List("Selections")
                .AddItems(items)
                .MaxVisibleItems(Math.Min(Math.Max(choices.Count, 6), 24))
                .WithCheckboxMode()
                .WithAutoHighlightOnFocus(true)
                .WithHoverHighlighting(true)
                .WithScrollbarVisibility(ScrollbarVisibility.Auto)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .WithColors(Color.White, new Color(18, 21, 24))
                .WithHighlightColors(Color.Cyan1, new Color(78, 82, 86))
                .OnSelectionChanged((sender, index) =>
                {
                    if (index >= 0 && index < labels.Length)
                    {
                        detailMarkup.SetContent(BuildMultiSelectDetailLines(title, labels[index], listControl?.GetCheckedItems().Count ?? 0));
                    }
                })
                .OnCheckedItemsChanged((sender, _) =>
                {
                    var selectedCount = listControl?.GetCheckedItems().Count ?? 0;
                    statusBar.SetContent([BuildMultiSelectStatusLine(title, selectedCount, choices.Count)]);
                    var index = listControl?.SelectedIndex ?? -1;
                    if (index >= 0 && index < labels.Length)
                    {
                        detailMarkup.SetContent(BuildMultiSelectDetailLines(title, labels[index], selectedCount));
                    }
                })
                .OnItemActivated((sender, item, window) =>
                {
                    item.IsChecked = !item.IsChecked;
                })
                .Build();
            listControl = list;
            list.SelectedIndex = 0;

            var detailPanel = Controls.ScrollablePanel()
                .AddControl(detailMarkup)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .Build();

            var grid = Controls.HorizontalGrid()
                .Column(column => column.Width(ResolveSidebarWidth(labels)).Add(list))
                .Column(column => column.Flex().Add(detailPanel))
                .WithSplitterAfter(0)
                .WithAlignment(HorizontalAlignment.Stretch)
                .WithVerticalAlignment(VerticalAlignment.Fill)
                .Build();

            var header = Controls.Markup()
                .AddLine(BuildDashboardHeader(title, choices.Count))
                .StickyTop()
                .Build();

            new WindowBuilder(windowSystem)
                .WithTitle("dotnet skills")
                .WithSize(ResolveDashboardWidth(), ResolveDashboardHeight())
                .Centered()
                .WithBackgroundGradient(
                    ColorGradient.FromColors(new Color(12, 14, 16), new Color(22, 25, 28)),
                    GradientDirection.Vertical)
                .WithColors(Color.White, new Color(12, 14, 16))
                .WithBorderStyle(BorderStyle.Single)
                .Resizable(false)
                .Movable(false)
                .AddControls(header, grid, statusBar)
                .OnKeyPressed((sender, args) =>
                {
                    if (args.KeyInfo.Key == ConsoleKey.F10)
                    {
                        selectedValues = list.GetCheckedItems()
                            .Select(item => item.Tag is int index && index >= 0 && index < choices.Count ? choices[index] : default)
                            .Where(item => item is not null)
                            .Cast<T>()
                            .ToArray();
                        completed = true;
                        args.Handled = true;
                        windowSystem.Shutdown(0);
                    }
                    else if (args.KeyInfo.Key == ConsoleKey.Escape)
                    {
                        selectedValues = [];
                        completed = true;
                        args.Handled = true;
                        windowSystem.Shutdown(0);
                    }
                })
                .BuildAndShow();

            windowSystem.Run();
            selected = selectedValues;
            AnsiConsole.Clear();
            return completed;
        }
        catch
        {
            AnsiConsole.Clear();
            return false;
        }
    }

    private static ConsoleWindowSystem CreatePromptWindowSystem()
    {
        var windowSystem = new ConsoleWindowSystem(
            new NetConsoleDriver(RenderMode.Buffer),
            options: new ConsoleWindowSystemOptions(
                TopPanelConfig: panel => panel.Left(Elements.StatusText("")),
                BottomPanelConfig: panel => panel.Center(Elements.TaskBar())));
        windowSystem.PanelStateService.TopStatus = "dotnet skills";
        windowSystem.PanelStateService.BottomStatus = "Skills | Bundles | Agents | Workspace";
        return windowSystem;
    }

    private static int ResolvePromptWidth(IEnumerable<string> labels)
    {
        var contentWidth = labels.Select(label => label.Length).DefaultIfEmpty(32).Max() + 12;
        var terminalWidth = Console.IsOutputRedirected ? 100 : Math.Max(60, Console.WindowWidth);
        return Math.Clamp(contentWidth, 64, Math.Max(64, terminalWidth - 8));
    }

    private static int ResolvePromptHeight(int choiceCount, int extraRows)
    {
        var contentHeight = Math.Min(Math.Max(choiceCount, 4), 18) + extraRows;
        var terminalHeight = Console.IsOutputRedirected ? 30 : Math.Max(20, Console.WindowHeight);
        return Math.Clamp(contentHeight, 14, Math.Max(14, terminalHeight - 4));
    }

    private static bool CanUseDashboardChrome()
    {
        try
        {
            return Console.WindowWidth >= 88 && Console.WindowHeight >= 24;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static int ResolveDashboardWidth()
    {
        var terminalWidth = Math.Max(88, Console.WindowWidth);
        return Math.Clamp(terminalWidth - 4, 88, 168);
    }

    private static int ResolveDashboardHeight()
    {
        var terminalHeight = Math.Max(24, Console.WindowHeight);
        return Math.Clamp(terminalHeight - 4, 24, 48);
    }

    private static int ResolveSidebarWidth(IReadOnlyList<string> labels)
    {
        var contentWidth = labels.Select(label => label.Length).DefaultIfEmpty(28).Max() + 6;
        return Math.Clamp(contentWidth, 34, 54);
    }

    private static string BuildDashboardHeader(string title, int choiceCount)
    {
        return $"[bold cyan] dotnet skills[/] [dim]| {Markup.Escape(title)} |[/] [yellow]{choiceCount} actions[/] [dim]| {DateTime.Now:HH:mm}[/]";
    }

    private static string BuildSelectStatusLine(string title, int choiceCount)
    {
        return $"[cyan]↑↓[/]:navigate  [cyan]Enter[/]:select  [cyan]Esc[/]:back/exit  [dim]{Markup.Escape(title)} • {choiceCount} actions[/]";
    }

    private static string BuildMultiSelectStatusLine(string title, int selectedCount, int choiceCount)
    {
        return $"[cyan]↑↓[/]:navigate  [cyan]Space/Enter[/]:toggle  [cyan]F10[/]:apply  [cyan]Esc[/]:cancel  [yellow]{selectedCount}/{choiceCount} selected[/] [dim]{Markup.Escape(title)}[/]";
    }

    private static List<string> BuildSelectionDetailLines(string title, string label)
    {
        var lines = new List<string>
        {
            $"[bold cyan]{Markup.Escape(label)}[/]",
            $"[dim]{Markup.Escape(title)}[/]",
            "",
        };

        lines.AddRange(BuildWorkflowLines(label));
        lines.AddRange(
        [
            "",
            "[dim]Controls[/]",
            "  [cyan]Enter[/]  run selected flow",
            "  [cyan]Esc[/]    return to the previous level when available",
            "",
            "[dim]Command shape[/]",
            $"  {BuildCommandHint(label)}",
        ]);
        return lines;
    }

    private static List<string> BuildMultiSelectDetailLines(string title, string label, int selectedCount)
    {
        var lines = new List<string>
        {
            $"[bold cyan]{Markup.Escape(label)}[/]",
            $"[dim]{Markup.Escape(title)}[/]",
            "",
            $"[yellow]{selectedCount} selected[/]",
            "",
        };

        lines.AddRange(BuildWorkflowLines(label));
        lines.AddRange(
        [
            "",
            "[dim]Controls[/]",
            "  [cyan]Space[/]  toggle the highlighted row",
            "  [cyan]Enter[/]  toggle the highlighted row",
            "  [cyan]F10[/]    apply the selected rows",
            "  [cyan]Esc[/]    cancel",
        ]);
        return lines;
    }

    private static IEnumerable<string> BuildWorkflowLines(string label)
    {
        var normalized = label.ToLowerInvariant();
        if (normalized.StartsWith("primary", StringComparison.Ordinal)
            || normalized.Contains("sync", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Scan the current project.",
                "  Match detected .NET signals to catalog skills.",
                "  Install missing recommendations into the active target.",
            ];
        }

        if (normalized.StartsWith("skills", StringComparison.Ordinal)
            || normalized.Contains("install skills", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Browse individual catalog skills.",
                "  Pick focused capabilities by alias, category, or package signal.",
                "  Install only the selected skill contracts.",
            ];
        }

        if (normalized.StartsWith("bundles", StringComparison.Ordinal)
            || normalized.Contains("bundle", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Use curated grouped installs.",
                "  Keep public wording as bundles, not packages.",
                "  Good for AI, testing, architecture, and MCAF skill sets.",
            ];
        }

        if (normalized.Contains("repair", StringComparison.Ordinal)
            || normalized.Contains("optimize", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Force reinstall selected content from the active catalog.",
                "  Refresh generated vendor adapters and overwritten payload files.",
                "  Use this when installed content looks stale or broken.",
            ];
        }

        if (normalized.Contains("copy", StringComparison.Ordinal)
            || normalized.Contains("move", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Pick a destination platform and scope.",
                "  Copy and refresh selected content into that target.",
                "  Optionally remove the copied content from the current target.",
            ];
        }

        if (normalized.StartsWith("installed", StringComparison.Ordinal)
            || normalized.Contains("manage", StringComparison.Ordinal)
            || normalized.Contains("remove", StringComparison.Ordinal)
            || normalized.Contains("update", StringComparison.Ordinal)
            || normalized.Contains("installed", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Inspect installed skills.",
                "  Update outdated entries.",
                "  Repair by force reinstalling from the catalog.",
                "  Copy or move content between supported targets.",
                "  Remove stale or unwanted local skill contracts.",
            ];
        }

        if (normalized.StartsWith("agents", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Manage orchestration agents separately from skills.",
                "  Install into vendor-native agent targets.",
                "  Repair or copy agents across supported targets.",
                "  Keep skill-first and agent-first surfaces distinct.",
            ];
        }

        if (normalized.StartsWith("workspace", StringComparison.Ordinal)
            || normalized.Contains("destination", StringComparison.Ordinal)
            || normalized.Contains("catalog", StringComparison.Ordinal)
            || normalized.Contains("settings", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Change target platform and install scope.",
                "  Inspect catalog source and refresh state.",
                "  Keep project/user destinations explicit.",
            ];
        }

        if (normalized.Contains("back", StringComparison.Ordinal)
            || normalized.Contains("exit", StringComparison.Ordinal))
        {
            return
            [
                "[dim]Workflow[/]",
                "  Leave this screen without changing installed content.",
            ];
        }

        return
        [
            "[dim]Workflow[/]",
            "  Review the selected item.",
            "  Press Enter to continue.",
        ];
    }

    private static string BuildCommandHint(string label)
    {
        var normalized = label.ToLowerInvariant();
        if (normalized.StartsWith("primary", StringComparison.Ordinal)
            || normalized.Contains("sync", StringComparison.Ordinal))
        {
            return "[green]dotnet skills sync --force[/]";
        }

        if (normalized.StartsWith("skills", StringComparison.Ordinal))
        {
            return "[green]dotnet skills install aspire orleans[/]";
        }

        if (normalized.StartsWith("bundles", StringComparison.Ordinal)
            || normalized.Contains("bundle", StringComparison.Ordinal))
        {
            return "[green]dotnet skills install bundle ai[/]";
        }

        if (normalized.StartsWith("installed", StringComparison.Ordinal)
            || normalized.Contains("remove", StringComparison.Ordinal))
        {
            return "[green]dotnet skills remove --all[/]";
        }

        if (normalized.StartsWith("agents", StringComparison.Ordinal))
        {
            return "[green]dotnet agents install --agent codex[/]";
        }

        if (normalized.Contains("repair", StringComparison.Ordinal))
        {
            return "[green]force reinstall from the selected catalog[/]";
        }

        if (normalized.Contains("copy", StringComparison.Ordinal)
            || normalized.Contains("move", StringComparison.Ordinal))
        {
            return "[green]copy to another vendor-native target, then optionally remove source[/]";
        }

        if (normalized.StartsWith("workspace", StringComparison.Ordinal))
        {
            return "[green]dotnet skills --agent codex --scope project[/]";
        }

        return "[dim]interactive flow[/]";
    }

    private static int FindEscapeChoice<T>(IReadOnlyList<T> choices, Func<T, string> formatter)
    {
        for (var index = choices.Count - 1; index >= 0; index--)
        {
            var label = formatter(choices[index]);
            if (string.Equals(label, "Back", StringComparison.OrdinalIgnoreCase)
                || string.Equals(label, "Exit", StringComparison.OrdinalIgnoreCase)
                || label.EndsWith(" - back", StringComparison.OrdinalIgnoreCase)
                || label.EndsWith(" - exit", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool CanUseSharpConsoleUi()
    {
        if (Console.IsInputRedirected
            || Console.IsOutputRedirected
            || string.Equals(Environment.GetEnvironmentVariable("DOTNET_SKILLS_PLAIN_CONSOLE"), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable("CODEX_CI"), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable("CODEX_SHELL"), "1", StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_THREAD_ID")))
        {
            return false;
        }

        // SharpConsoleUI uses native terminal raw-mode calls on macOS. Keep that path opt-in
        // until the driver is stable there, because AccessViolationException can terminate
        // the process before the managed fallback path can run.
        if (OperatingSystem.IsMacOS()
            && !string.Equals(Environment.GetEnvironmentVariable("DOTNET_SKILLS_SHARP_CONSOLE"), "1", StringComparison.Ordinal))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        var terminal = Environment.GetEnvironmentVariable("TERM");
        return !string.IsNullOrWhiteSpace(terminal)
            && !string.Equals(terminal, "dumb", StringComparison.OrdinalIgnoreCase);
    }

    private static T SelectPlainText<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (choices.Count == 0)
        {
            throw new InvalidOperationException($"No choices are available for {title}.");
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(title);

            for (var index = 0; index < choices.Count; index++)
            {
                Console.WriteLine($"  {index + 1}. {formatter(choices[index])}");
            }

            Console.Write("> ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var choiceIndex) && choiceIndex >= 1 && choiceIndex <= choices.Count)
            {
                return choices[choiceIndex - 1];
            }

            Console.WriteLine("Enter the number of the item you want.");
        }
    }

    private static IReadOnlyList<T> MultiSelectPlainText<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (choices.Count == 0)
        {
            return [];
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(title);

            for (var index = 0; index < choices.Count; index++)
            {
                Console.WriteLine($"  {index + 1}. {formatter(choices[index])}");
            }

            Console.Write("Enter one or more numbers separated by commas, or press Enter to cancel: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return [];
            }

            var selectedIndexes = input
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var parsed) ? parsed : -1)
                .ToArray();

            if (selectedIndexes.All(index => index >= 1 && index <= choices.Count))
            {
                return selectedIndexes
                    .Distinct()
                    .Select(index => choices[index - 1])
                    .ToArray();
            }

            Console.WriteLine("Enter valid item numbers separated by commas.");
        }
    }

    private static bool ConfirmPlainText(string title, bool defaultValue)
    {
        while (true)
        {
            Console.Write($"{title} {(defaultValue ? "[Y/n]" : "[y/N]")} ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            if (string.Equals(input, "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(input, "n", StringComparison.OrdinalIgnoreCase)
                || string.Equals(input, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Console.WriteLine("Enter y or n.");
        }
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

internal enum HomeAction
{
    SyncProject,
    InstallSkillStack,
    InstallSkills,
    ManageInstalled,
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
    UpdateOutdated,
    Back,
}

internal enum InstalledSkillsAction
{
    Inspect,
    Repair,
    CopyOrMove,
    Remove,
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
