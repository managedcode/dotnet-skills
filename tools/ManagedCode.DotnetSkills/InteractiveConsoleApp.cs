using ManagedCode.DotnetSkills.Runtime;
using Spectre.Console;

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
        this.prompts = prompts ?? new SpectreInteractivePrompts();
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
                        new MenuOption<HomeAction>("Sync with project", HomeAction.SyncProject),
                        new MenuOption<HomeAction>("Install skill stack", HomeAction.InstallSkillStack),
                        new MenuOption<HomeAction>("Install skills", HomeAction.InstallSkills),
                        new MenuOption<HomeAction>("Manage installed", HomeAction.ManageInstalled),
                        new MenuOption<HomeAction>("Agents", HomeAction.Agents),
                        new MenuOption<HomeAction>("Settings", HomeAction.Settings),
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
        grid.AddRow(new Markup("[dim]packages[/]"), new Markup(skillCatalog.Packages.Count.ToString()));
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
        overview.AddRow(new Markup("[dim]packages[/]"), new Markup($"{skillCatalog.Packages.Count} available"));
        overview.AddRow(new Markup("[dim]agents[/]"), new Markup($"{agentCatalog.Agents.Count} [dim]({Escape(agentStatus.Summary)})[/]"));
        AnsiConsole.Write(new Panel(overview).Header("[deepskyblue1]workspace[/]").Border(BoxBorder.Rounded).Expand());

        // Compact quick-reference hints
        AnsiConsole.MarkupLine("[dim]  Sync with project \u2022 Install skill stack \u2022 Install skills \u2022 Manage installed \u2022 Agents \u2022 Settings[/]");
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
                new("Remove installed skills", InstalledSkillsAction.Remove),
            };

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

                actions.Add(new MenuOption<SkillDetailAction>("Remove this skill", SkillDetailAction.Remove));
                actions.Add(new MenuOption<SkillDetailAction>("Force reinstall", SkillDetailAction.Reinstall));
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
                case SkillDetailAction.Reinstall:
                    if (prompts.Confirm($"Force reinstall {ToAlias(skill.Name)}?", defaultValue: true))
                    {
                        InstallSkills([skill], force: true);
                    }

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
                "Package actions",
                new[]
                {
                    new MenuOption<PackageAction>("Inspect a package", PackageAction.Inspect),
                    new MenuOption<PackageAction>("Install packages", PackageAction.Install),
                    new MenuOption<PackageAction>("Back", PackageAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case PackageAction.Inspect:
                {
                    if (skillCatalog.Packages.Count == 0)
                    {
                        RenderInfo("No packages are available in this catalog version yet.");
                        break;
                    }

                    var selectedPackage = prompts.Select(
                        "Inspect a package",
                        skillCatalog.Packages.OrderBy(package => package.Name, StringComparer.Ordinal).ToArray(),
                        package => $"{package.Name} ({package.Skills.Count} skills)");
                    ShowPackageDetail(selectedPackage);
                    break;
                }
                case PackageAction.Install:
                {
                    if (skillCatalog.Packages.Count == 0)
                    {
                        RenderInfo("No packages are available in this catalog version yet.");
                        break;
                    }

                    var selectedPackages = prompts.MultiSelect(
                        "Install packages",
                        skillCatalog.Packages.OrderBy(package => package.Name, StringComparer.Ordinal).ToArray(),
                        package => $"{package.Name} ({package.Skills.Count} skills)");
                    if (selectedPackages.Count == 0)
                    {
                        break;
                    }

                    var layout = ResolveSkillLayout();
                    if (prompts.Confirm($"Install {selectedPackages.Count} package(s) into {layout.PrimaryRoot.FullName}?", defaultValue: true))
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
                "Package actions",
                new[]
                {
                    new MenuOption<PackageDetailAction>("Install this package", PackageDetailAction.Install),
                    new MenuOption<PackageDetailAction>("Back", PackageDetailAction.Back),
                },
                option => option.Label);

            switch (action.Value)
            {
                case PackageDetailAction.Install:
                {
                    var layout = ResolveSkillLayout();
                    if (prompts.Confirm($"Install package {package.Name} into {layout.PrimaryRoot.FullName}?", defaultValue: true))
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
                    actions.Add(new MenuOption<AgentDetailAction>("Remove this agent", AgentDetailAction.Remove));
                    actions.Add(new MenuOption<AgentDetailAction>("Force reinstall", AgentDetailAction.Reinstall));
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
                case AgentDetailAction.Reinstall:
                    if (layout is not null && prompts.Confirm($"Force reinstall {ToAlias(agent.Name)} into {layout.PrimaryRoot.FullName}?", defaultValue: true))
                    {
                        InstallAgents([agent], layout, force: true);
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
        var installer = new SkillInstaller(skillCatalog);
        var installedBefore = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var summary = installer.Install(skills, layout, force);
        var rows = Program.BuildInstallRows(skills, installedBefore, force, summary);

        AnsiConsole.Clear();
        ConsoleUi.RenderInstallSummary(skillCatalog, layout, rows, summary);
        prompts.Pause("Press any key to return to the interactive shell...");
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
        var installer = new SkillInstaller(skillCatalog);
        var installedBefore = installer.GetInstalledSkills(layout)
            .ToDictionary(record => record.Skill.Name, StringComparer.OrdinalIgnoreCase);
        var summary = installer.Remove(skills, layout);
        var rows = Program.BuildRemoveRows(skills, installedBefore, summary);

        AnsiConsole.Clear();
        ConsoleUi.RenderRemoveSummary(skillCatalog, layout, rows, summary.RemovedCount, null);
        prompts.Pause("Press any key to return to the interactive shell...");
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
        grid.AddRow(new Markup("[dim]package[/]"), new Markup(Escape(package.Name)));
        grid.AddRow(new Markup("[dim]type[/]"), new Markup(Escape(package.Kind)));
        grid.AddRow(new Markup("[dim]category[/]"), new Markup(Escape(package.SourceCategory)));
        grid.AddRow(new Markup("[dim]skills[/]"), new Markup(package.Skills.Count.ToString()));
        grid.AddRow(new Markup("[dim]install[/]"), new Markup($"[green]{Escape($"dotnet skills install package {package.Name}")}[/]"));
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

    private static string Escape(string value) => Markup.Escape(value);
}

internal interface IInteractivePrompts
{
    T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull;

    IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull;

    bool Confirm(string title, bool defaultValue);

    void Pause(string title);
}

internal sealed class SpectreInteractivePrompts : IInteractivePrompts
{
    public T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        try
        {
            var prompt = new SelectionPrompt<T>()
                .Title(title)
                .PageSize(Math.Min(Math.Max(choices.Count, 3), 18))
                .UseConverter(formatter);
            prompt.AddChoices(choices);
            return AnsiConsole.Prompt(prompt);
        }
        catch (Exception exception) when (RequiresPlainTextFallback(exception))
        {
            return SelectPlainText(title, choices, formatter);
        }
    }

    public IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        try
        {
            var prompt = new MultiSelectionPrompt<T>()
                .Title(title)
                .NotRequired()
                .PageSize(Math.Min(Math.Max(choices.Count, 3), 18))
                .UseConverter(formatter);
            prompt.AddChoices(choices);
            return AnsiConsole.Prompt(prompt);
        }
        catch (Exception exception) when (RequiresPlainTextFallback(exception))
        {
            return MultiSelectPlainText(title, choices, formatter);
        }
    }

    public bool Confirm(string title, bool defaultValue)
    {
        try
        {
            return AnsiConsole.Confirm(title, defaultValue);
        }
        catch (Exception exception) when (RequiresPlainTextFallback(exception))
        {
            return ConfirmPlainText(title, defaultValue);
        }
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

    private static bool RequiresPlainTextFallback(Exception exception)
    {
        return exception.Message.Contains("ANSI escape sequences", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("isn't interactive", StringComparison.OrdinalIgnoreCase);
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
    Remove,
    Update,
    Back,
}

internal enum SkillDetailAction
{
    Install,
    Update,
    Remove,
    Reinstall,
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
    Remove,
    Back,
}

internal enum AgentDetailAction
{
    Install,
    Remove,
    Reinstall,
    Back,
}

internal enum SessionTargetAction
{
    Platform,
    Scope,
    Back,
}
