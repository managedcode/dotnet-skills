using ManagedCode.DotnetSkills.Runtime;
using Spectre.Console;
using SpectreConsole = Spectre.Console.AnsiConsole;

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
        this.prompts = prompts ?? new CommandCenterInteractivePrompts();
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
                        new MenuOption<HomeAction>("Project - sync from project", HomeAction.SyncProject),
                        new MenuOption<HomeAction>("Stacks - browse by stack", HomeAction.InstallSkills),
                        new MenuOption<HomeAction>("Analysis - tree, tokens, package signals", HomeAction.Analysis),
                        new MenuOption<HomeAction>("Bundles - focused installs", HomeAction.InstallSkillStack),
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
                    case HomeAction.Analysis:
                        ShowCatalogAnalysis();
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
        grid.AddRow(new Markup("[dim]focused bundles[/]"), new Markup(GetPrimaryBundles().Count.ToString()));
        AnsiConsole.Write(new Panel(grid).Header("[deepskyblue1]refreshed[/]").Border(BoxBorder.Rounded).Expand());
        prompts.Pause("Press any key to continue...");
    }

    private void RenderDashboard()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold deepskyblue1]dotnet skills[/] [dim]v{0}[/]", Escape(ToolVersionInfo.CurrentVersion));
        AnsiConsole.MarkupLine("[dim].NET skill catalog for AI-assisted development[/]");
        AnsiConsole.WriteLine();

        var skillLayout = ResolveSkillLayout();
        var skillInstaller = new SkillInstaller(skillCatalog);
        var installedSkills = skillInstaller.GetInstalledSkills(skillLayout);
        var outdatedSkills = installedSkills.Count(record => !record.IsCurrent);
        var agentStatus = ResolveAgentStatus();
        var primaryBundleCount = GetPrimaryBundles().Count;
        var stackViews = BuildStackViews(installedSkills);
        var totalTokens = skillCatalog.Skills.Sum(skill => skill.TokenCount);
        var largestSkills = skillCatalog.Skills
            .OrderByDescending(skill => skill.TokenCount)
            .ThenBy(skill => skill.Name, StringComparer.Ordinal)
            .Take(5)
            .ToArray();
        var featuredBundles = GetPrimaryBundles()
            .Take(6)
            .ToArray();
        var largestStack = stackViews
            .OrderByDescending(stack => stack.TokenCount)
            .ThenBy(stack => stack.Stack, StringComparer.Ordinal)
            .FirstOrDefault();

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
        overview.AddRow(new Markup("[dim]bundles[/]"), new Markup($"{primaryBundleCount} focused installs"));
        overview.AddRow(new Markup("[dim]tokenizer[/]"), new Markup($"{Escape(SkillTokenCounter.ModelName)} [dim]({FormatTokenCount(totalTokens)} tokens)[/]"));
        overview.AddRow(new Markup("[dim]agents[/]"), new Markup($"{agentCatalog.Agents.Count} [dim]({Escape(agentStatus.Summary)})[/]"));
        var flowTable = new Table().Border(TableBorder.None).Expand();
        flowTable.AddColumn("Flow");
        flowTable.AddColumn("Intent");
        flowTable.AddColumn("Command");
        flowTable.AddRow("[deepskyblue1]Project[/]", "[dim]sync from .csproj signals[/]", "[grey]dotnet skills install --auto[/]");
        flowTable.AddRow("[deepskyblue1]Stacks[/]", "[dim]Stack -> Lane -> Skill[/]", "[grey]dotnet skills list --available-only[/]");
        flowTable.AddRow("[deepskyblue1]Analysis[/]", "[dim]tree, tokens, package signals[/]", "[grey]dotnet skills catalog tokens[/]");
        flowTable.AddRow("[deepskyblue1]Bundles[/]", "[dim]focused multi-skill installs[/]", "[grey]dotnet skills bundle list[/]");
        flowTable.AddRow("[deepskyblue1]Installed[/]", "[dim]repair, move, remove, update[/]", "[grey]dotnet skills list --installed-only[/]");
        flowTable.AddRow("[deepskyblue1]Workspace[/]", "[dim]platform, scope, catalog source[/]", "[grey]dotnet skills where[/]");

        var telemetryTable = new Table().Border(TableBorder.None).Expand();
        telemetryTable.AddColumn("Signal");
        telemetryTable.AddColumn("Value");
        telemetryTable.AddRow("Stacks", stackViews.Count.ToString());
        telemetryTable.AddRow("Lanes", stackViews.Sum(stack => stack.Lanes.Count).ToString());
        telemetryTable.AddRow("Package signals", BuildPackageSignals().Count.ToString());
        telemetryTable.AddRow("Installed coverage", $"{installedSkills.Count}/{skillCatalog.Skills.Count}");
        telemetryTable.AddRow("Largest stack", largestStack is null ? "-" : $"{Escape(largestStack.Stack)} [dim]({FormatTokenCount(largestStack.TokenCount)})[/]");
        telemetryTable.AddRow("Outdated", outdatedSkills == 0 ? "[green]0[/]" : $"[yellow]{outdatedSkills}[/]");

        var stackTable = new Table().Border(TableBorder.None).Expand();
        stackTable.AddColumn("Stack");
        stackTable.AddColumn("Lanes");
        stackTable.AddColumn("Skills");
        stackTable.AddColumn("Tokens");

        foreach (var stack in stackViews.Take(6))
        {
            stackTable.AddRow(
                Escape(stack.Stack),
                stack.Lanes.Count.ToString(),
                $"{stack.InstalledCount}/{stack.SkillCount}",
                FormatTokenCount(stack.TokenCount));
        }

        var heavyTable = new Table().Border(TableBorder.None).Expand();
        heavyTable.AddColumn("Skill");
        heavyTable.AddColumn("Area");
        heavyTable.AddColumn("Tokens");

        foreach (var skill in largestSkills)
        {
            heavyTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape($"{skill.Stack} / {skill.Lane}"),
                FormatTokenCount(skill.TokenCount));
        }

        var bundleTable = new Table().Border(TableBorder.None).Expand();
        bundleTable.AddColumn("Bundle");
        bundleTable.AddColumn("Area");
        bundleTable.AddColumn("Tokens");

        foreach (var bundle in featuredBundles)
        {
            var bundleTokens = bundle.Skills
                .Select(skillName => skillCatalog.Skills.FirstOrDefault(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase)))
                .Where(skill => skill is not null)
                .Sum(skill => skill!.TokenCount);
            bundleTable.AddRow(
                Escape(bundle.Name),
                Escape(CatalogOrganization.ResolveBundleAreaLabel(bundle)),
                FormatTokenCount(bundleTokens));
        }

        var topGrid = new Grid();
        topGrid.AddColumn();
        topGrid.AddColumn();
        topGrid.AddColumn();
        topGrid.AddRow(
            new Panel(flowTable).Header("[deepskyblue1]navigation rail[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(overview).Header("[deepskyblue1]workspace[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(telemetryTable).Header("[deepskyblue1]catalog telemetry[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(topGrid);
        AnsiConsole.WriteLine();
        var bottomGrid = new Grid();
        bottomGrid.AddColumn();
        bottomGrid.AddColumn();
        bottomGrid.AddColumn();
        bottomGrid.AddRow(
            new Panel(stackTable).Header("[deepskyblue1]stack coverage[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(heavyTable).Header("[deepskyblue1]largest skills[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(bundleTable).Header("[deepskyblue1]focused bundles[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(bottomGrid);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup("[dim]Enter[/] [grey]select[/] [dim]\u2022[/] [dim]Space[/] [grey]multi-select[/] [dim]\u2022[/] [dim]Stacks now narrow before install[/] [dim]\u2022[/] [dim]Bundles stay focused[/] [dim]\u2022[/] [dim]Token counts use[/] [green]gpt-5 / o200k_base[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
        AnsiConsole.WriteLine();
    }

    private async Task ShowCatalogSkillsAsync()
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var stackViews = BuildStackViews(installedSkills);

            AnsiConsole.Clear();
            RenderStackBrowserPanel(stackViews, layout);

            var actions = new List<MenuOption<SkillCatalogAction>>
            {
                new("Browse a stack", SkillCatalogAction.Inspect),
            };

            if (installedSkills.Any(record => !record.IsCurrent))
            {
                actions.Add(new MenuOption<SkillCatalogAction>("Update outdated skills", SkillCatalogAction.UpdateOutdated));
            }

            actions.Add(new MenuOption<SkillCatalogAction>("Back", SkillCatalogAction.Back));

            var action = prompts.Select("Stack catalog actions", actions, option => option.Label);
            switch (action.Value)
            {
                case SkillCatalogAction.Inspect:
                {
                    var selectedStack = prompts.Select(
                        "Browse a stack",
                        stackViews,
                        BuildStackChoiceLabel);
                    ShowStackDetail(selectedStack.Stack);
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

    private void ShowCatalogAnalysis()
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var stackViews = BuildStackViews(installedSkills);
            var packageSignals = BuildPackageSignals();

            AnsiConsole.Clear();
            RenderCatalogAnalysisPanel(stackViews, layout, packageSignals);

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
                    ShowCatalogTree(stackViews);
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

    private void ShowCatalogTree(IReadOnlyList<StackCatalogView> stackViews)
    {
        while (true)
        {
            AnsiConsole.Clear();
            RenderCatalogTreePanel(stackViews);

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

    private void ShowStackDetail(string stackName)
    {
        while (true)
        {
            var layout = ResolveSkillLayout();
            var installer = new SkillInstaller(skillCatalog);
            var installedSkills = installer.GetInstalledSkills(layout);
            var stackView = BuildStackViews(installedSkills)
                .FirstOrDefault(view => string.Equals(view.Stack, stackName, StringComparison.OrdinalIgnoreCase));

            if (stackView is null)
            {
                RenderInfo($"Stack {stackName} is not available in this catalog version.");
                return;
            }

            AnsiConsole.Clear();
            RenderStackDetailPanel(stackView, layout);

            var actions = new List<MenuOption<SkillCatalogAction>>
            {
                new("Inspect a lane", SkillCatalogAction.Inspect),
                new("Install from a lane", SkillCatalogAction.Install),
            };

            if (installedSkills.Any(record => !record.IsCurrent && string.Equals(record.Skill.Stack, stackView.Stack, StringComparison.OrdinalIgnoreCase)))
            {
                actions.Add(new MenuOption<SkillCatalogAction>("Update outdated skills in this stack", SkillCatalogAction.UpdateOutdated));
            }

            actions.Add(new MenuOption<SkillCatalogAction>("Back", SkillCatalogAction.Back));

            var action = prompts.Select("Stack actions", actions, option => option.Label);
            switch (action.Value)
            {
                case SkillCatalogAction.Inspect:
                {
                    var selectedLane = prompts.Select(
                        "Inspect a lane",
                        stackView.Lanes,
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
                        stackView.Lanes,
                        BuildLaneChoiceLabel);
                    var installableSkills = selectedLane.Skills
                        .Where(skill => installedSkills.All(record => !string.Equals(record.Skill.Name, skill.Name, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(skill => skill.Name, StringComparer.Ordinal)
                        .ToArray();

                    if (installableSkills.Length == 0)
                    {
                        RenderInfo($"Everything in {stackView.Stack} / {selectedLane.Lane} is already installed in this target.");
                        break;
                    }

                    var selectedSkills = prompts.MultiSelect(
                        "Install skills",
                        installableSkills,
                        skill => $"{ToAlias(skill.Name)} [{skill.Lane}] ({FormatTokenCount(skill.TokenCount)} tokens)");
                    if (selectedSkills.Count == 0)
                    {
                        break;
                    }

                    if (ConfirmSkillInstallPreview($"Lane install: {stackView.Stack} / {selectedLane.Lane}", selectedSkills, layout, force: false))
                    {
                        InstallSkills(selectedSkills, layout, force: false);
                    }

                    break;
                }
                case SkillCatalogAction.UpdateOutdated:
                {
                    var outdatedSkills = installedSkills
                        .Where(record => !record.IsCurrent && string.Equals(record.Skill.Stack, stackView.Stack, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(record => record.Skill.Lane, StringComparer.Ordinal)
                        .ThenBy(record => record.Skill.Name, StringComparer.Ordinal)
                        .ToArray();
                    if (outdatedSkills.Length == 0)
                    {
                        RenderInfo($"No outdated skills are installed in the {stackView.Stack} stack.");
                        break;
                    }

                    var selected = prompts.MultiSelect(
                        "Update outdated skills",
                        outdatedSkills,
                        record => $"{ToAlias(record.Skill.Name)} [{record.Skill.Lane}] ({record.InstalledVersion} -> {record.Skill.Version})");
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
            ConsoleUi.RenderPackageList(skillCatalog);

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
                    if (selectedPackages.Count == 0)
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

    private IReadOnlyList<StackCatalogView> BuildStackViews(IReadOnlyList<InstalledSkillRecord> installedSkills)
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
                        return new LaneCatalogView(
                            group.Key,
                            laneGroup.Key,
                            laneSkills,
                            laneSkills.Count(skill => installedNames.Contains(skill.Name)),
                            laneSkills.Sum(skill => skill.TokenCount));
                    })
                    .ToArray();

                var stackSkills = group.ToArray();
                return new StackCatalogView(
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

    private void RenderStackBrowserPanel(IReadOnlyList<StackCatalogView> stackViews, SkillInstallLayout layout)
    {
        var overview = new Grid();
        overview.AddColumn(new GridColumn().NoWrap());
        overview.AddColumn();
        overview.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        overview.AddRow(new Markup("[dim]stacks[/]"), new Markup(stackViews.Count.ToString()));
        overview.AddRow(new Markup("[dim]skills[/]"), new Markup(skillCatalog.Skills.Count.ToString()));
        overview.AddRow(new Markup("[dim]tokens[/]"), new Markup(FormatTokenCount(skillCatalog.Skills.Sum(skill => skill.TokenCount))));
        var flow = new Table().Border(TableBorder.None).Expand();
        flow.AddColumn("Step");
        flow.AddColumn("Action");
        flow.AddRow("1", "[deepskyblue1]Choose a stack[/] [dim]to narrow the catalog surface[/]");
        flow.AddRow("2", "[deepskyblue1]Inspect a lane[/] [dim]to see concrete skills and size[/]");
        flow.AddRow("3", "[deepskyblue1]Install from a lane[/] [dim]without broad mixed bundles[/]");

        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle("[bold]Stack -> Lane -> Skill[/]");
        table.AddColumn("Stack");
        table.AddColumn("Lanes");
        table.AddColumn("Installed");
        table.AddColumn("Tokens");
        table.AddColumn("Sample lanes");

        foreach (var stack in stackViews)
        {
            var sampleLanes = stack.Lanes
                .Take(3)
                .Select(lane => lane.Lane)
                .ToArray();
            table.AddRow(
                Escape(stack.Stack),
                stack.Lanes.Count.ToString(),
                $"{stack.InstalledCount}/{stack.SkillCount}",
                FormatTokenCount(stack.TokenCount),
                Escape(string.Join(", ", sampleLanes)) + (stack.Lanes.Count > sampleLanes.Length ? $" [grey](+{stack.Lanes.Count - sampleLanes.Length})[/]" : string.Empty));
        }

        var spotlight = new Table().Expand().Border(TableBorder.None);
        spotlight.AddColumn("Focus");
        spotlight.AddColumn("Value");

        foreach (var stack in stackViews
                     .OrderByDescending(entry => entry.TokenCount)
                     .ThenBy(entry => entry.Stack, StringComparer.Ordinal)
                     .Take(5))
        {
            spotlight.AddRow(
                Escape(stack.Stack),
                $"{FormatTokenCount(stack.TokenCount)} [dim]({stack.InstalledCount}/{stack.SkillCount})[/]");
        }

        var headerGrid = new Grid();
        headerGrid.AddColumn();
        headerGrid.AddColumn();
        headerGrid.AddRow(
            new Panel(flow).Header("[deepskyblue1]navigation[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(overview).Header("[deepskyblue1]stack browser[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(headerGrid);
        AnsiConsole.WriteLine();

        var bodyGrid = new Grid();
        bodyGrid.AddColumn();
        bodyGrid.AddColumn();
        bodyGrid.AddRow(
            new Panel(table).Header("[deepskyblue1]stack matrix[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(spotlight).Header("[deepskyblue1]heaviest stacks[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(bodyGrid);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup("[dim]Choose a stack first. Install surfaces only appear after the lane boundary is explicit.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderStackDetailPanel(StackCatalogView stackView, SkillInstallLayout layout)
    {
        var summary = new Grid();
        summary.AddColumn(new GridColumn().NoWrap());
        summary.AddColumn();
        summary.AddRow(new Markup("[dim]stack[/]"), new Markup(Escape(stackView.Stack)));
        summary.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        summary.AddRow(new Markup("[dim]lanes[/]"), new Markup(stackView.Lanes.Count.ToString()));
        summary.AddRow(new Markup("[dim]skills[/]"), new Markup($"{stackView.InstalledCount}/{stackView.SkillCount}"));
        summary.AddRow(new Markup("[dim]tokens[/]"), new Markup(FormatTokenCount(stackView.TokenCount)));
        var flow = new Table().Border(TableBorder.None).Expand();
        flow.AddColumn("Mode");
        flow.AddColumn("Effect");
        flow.AddRow("[deepskyblue1]Inspect a lane[/]", "[dim]see each skill and its detail card[/]");
        flow.AddRow("[deepskyblue1]Install from a lane[/]", "[dim]install only the narrowed slice[/]");
        flow.AddRow("[deepskyblue1]Update outdated[/]", "[dim]refresh this stack only[/]");

        var laneTable = new Table().Expand().Border(TableBorder.Rounded);
        laneTable.Title = new TableTitle("[bold]Lanes[/]");
        laneTable.AddColumn("Lane");
        laneTable.AddColumn("Skills");
        laneTable.AddColumn("Installed");
        laneTable.AddColumn("Tokens");
        laneTable.AddColumn("Examples");

        foreach (var lane in stackView.Lanes)
        {
            var examples = lane.Skills.Take(3).Select(skill => ToAlias(skill.Name)).ToArray();
            laneTable.AddRow(
                Escape(lane.Lane),
                lane.Skills.Count.ToString(),
                lane.InstalledCount.ToString(),
                FormatTokenCount(lane.TokenCount),
                Escape(string.Join(", ", examples)) + (lane.Skills.Count > examples.Length ? $" [grey](+{lane.Skills.Count - examples.Length})[/]" : string.Empty));
        }

        var skillTable = new Table().Expand().Border(TableBorder.Rounded);
        skillTable.Title = new TableTitle("[bold]Heaviest skills in stack[/]");
        skillTable.AddColumn("Alias");
        skillTable.AddColumn("Lane");
        skillTable.AddColumn("Tokens");
        skillTable.AddColumn("Summary");

        foreach (var skill in stackView.Lanes
                     .SelectMany(lane => lane.Skills)
                     .OrderByDescending(skill => skill.TokenCount)
                     .ThenBy(skill => skill.Name, StringComparer.Ordinal)
                     .Take(8))
        {
            skillTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape(skill.Lane),
                FormatTokenCount(skill.TokenCount),
                Escape(CompactDescription(skill.Description)));
        }

        var relatedBundles = GetPrimaryBundles()
            .Where(package => string.Equals(package.Stack, stackView.Stack, StringComparison.OrdinalIgnoreCase)
                              || package.Skills.Any(skillName =>
                                  stackView.Lanes.SelectMany(lane => lane.Skills)
                                      .Any(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase))))
            .DistinctBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(CatalogOrganization.FormatBundleSortKey, StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        var relatedBundleTable = new Table().Expand().Border(TableBorder.None);
        relatedBundleTable.AddColumn("Bundle");
        relatedBundleTable.AddColumn("Area");
        relatedBundleTable.AddColumn("Tokens");

        foreach (var bundle in relatedBundles)
        {
            var tokenCount = bundle.Skills
                .Select(skillName => skillCatalog.Skills.FirstOrDefault(skill => string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase)))
                .Where(skill => skill is not null)
                .Sum(skill => skill!.TokenCount);
            relatedBundleTable.AddRow(
                Escape(bundle.Name),
                Escape(CatalogOrganization.ResolveBundleAreaLabel(bundle)),
                FormatTokenCount(tokenCount));
        }

        var headerGrid = new Grid();
        headerGrid.AddColumn();
        headerGrid.AddColumn();
        headerGrid.AddRow(
            new Panel(flow).Header("[deepskyblue1]stack modes[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(summary).Header($"[deepskyblue1]{Escape(stackView.Stack)}[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(headerGrid);
        AnsiConsole.WriteLine();

        var detailGrid = new Grid();
        detailGrid.AddColumn();
        detailGrid.AddColumn();
        detailGrid.AddRow(
            new Panel(laneTable).Header("[deepskyblue1]lane map[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(skillTable).Header("[deepskyblue1]heavy skills[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(detailGrid);
        AnsiConsole.WriteLine();

        if (relatedBundles.Length > 0)
        {
            AnsiConsole.Write(new Panel(relatedBundleTable).Header("[deepskyblue1]related bundles[/]").Border(BoxBorder.Rounded).Expand());
            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(new Panel(new Markup("[dim]This stack is already narrowed. Next selection happens at the lane level, not against the full catalog.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderCatalogAnalysisPanel(IReadOnlyList<StackCatalogView> stackViews, SkillInstallLayout layout, IReadOnlyList<PackageSignalView> packageSignals)
    {
        var summary = new Grid();
        summary.AddColumn(new GridColumn().NoWrap());
        summary.AddColumn();
        summary.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        summary.AddRow(new Markup("[dim]stacks[/]"), new Markup(stackViews.Count.ToString()));
        summary.AddRow(new Markup("[dim]skills[/]"), new Markup(skillCatalog.Skills.Count.ToString()));
        summary.AddRow(new Markup("[dim]package signals[/]"), new Markup(packageSignals.Count.ToString()));
        summary.AddRow(new Markup("[dim]tokens[/]"), new Markup(FormatTokenCount(skillCatalog.Skills.Sum(skill => skill.TokenCount))));

        var heavyTable = new Table().Expand().Border(TableBorder.Rounded);
        heavyTable.Title = new TableTitle("[bold]Heaviest skills[/]");
        heavyTable.AddColumn("Skill");
        heavyTable.AddColumn("Area");
        heavyTable.AddColumn("Tokens");

        foreach (var skill in skillCatalog.Skills
                     .OrderByDescending(skill => skill.TokenCount)
                     .ThenBy(skill => skill.Name, StringComparer.Ordinal)
                     .Take(10))
        {
            heavyTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape($"{skill.Stack} / {skill.Lane}"),
                FormatTokenCount(skill.TokenCount));
        }

        var packageTable = new Table().Expand().Border(TableBorder.Rounded);
        packageTable.Title = new TableTitle("[bold]Package entry points[/]");
        packageTable.AddColumn("Signal");
        packageTable.AddColumn("Kind");
        packageTable.AddColumn("Skill");
        packageTable.AddColumn("Area");

        foreach (var entry in packageSignals.Take(12))
        {
            packageTable.AddRow(
                Escape(entry.Signal),
                Escape(entry.Kind),
                Escape(ToAlias(entry.Skill.Name)),
                Escape($"{entry.Skill.Stack} / {entry.Skill.Lane}"));
        }

        var stackTable = new Table().Expand().Border(TableBorder.Rounded);
        stackTable.Title = new TableTitle("[bold]Stack composition[/]");
        stackTable.AddColumn("Stack");
        stackTable.AddColumn("Lanes");
        stackTable.AddColumn("Skills");
        stackTable.AddColumn("Tokens");

        foreach (var stack in stackViews)
        {
            stackTable.AddRow(
                Escape(stack.Stack),
                stack.Lanes.Count.ToString(),
                stack.SkillCount.ToString(),
                FormatTokenCount(stack.TokenCount));
        }

        var headerGrid = new Grid();
        headerGrid.AddColumn();
        headerGrid.AddColumn();
        headerGrid.AddRow(
            new Panel(summary).Header("[deepskyblue1]catalog analysis[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(packageTable).Header("[deepskyblue1]package signals[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(headerGrid);
        AnsiConsole.WriteLine();

        var bodyGrid = new Grid();
        bodyGrid.AddColumn();
        bodyGrid.AddColumn();
        bodyGrid.AddRow(
            new Panel(stackTable).Header("[deepskyblue1]stack matrix[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(heavyTable).Header("[deepskyblue1]token hotspots[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(bodyGrid);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup("[dim]Use the tree view for hierarchy-first browsing, then inspect heavy skills or package entry points before installing.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderCatalogTreePanel(IReadOnlyList<StackCatalogView> stackViews)
    {
        var tree = new Tree("[bold]catalog[/]");

        foreach (var stack in stackViews)
        {
            var stackNode = tree.AddNode($"{Escape(stack.Stack)} [dim]({stack.SkillCount} skills, {FormatTokenCount(stack.TokenCount)} tokens)[/]");
            foreach (var lane in stack.Lanes)
            {
                var laneNode = stackNode.AddNode($"{Escape(lane.Lane)} [dim]({lane.Skills.Count} skills, {FormatTokenCount(lane.TokenCount)} tokens)[/]");
                foreach (var skill in lane.Skills.OrderByDescending(skill => skill.TokenCount).ThenBy(skill => skill.Name, StringComparer.Ordinal))
                {
                    laneNode.AddNode($"{Escape(ToAlias(skill.Name))} [dim]({FormatTokenCount(skill.TokenCount)})[/]");
                }
            }
        }

        AnsiConsole.MarkupLine("[bold deepskyblue1]stack tree[/]");
        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup("[dim]This is the full Stack -> Lane -> Skill hierarchy for the current catalog payload.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderPackageSignalPanel(IReadOnlyList<PackageSignalView> packageSignals)
    {
        var summary = new Grid();
        summary.AddColumn(new GridColumn().NoWrap());
        summary.AddColumn();
        summary.AddRow(new Markup("[dim]signals[/]"), new Markup(packageSignals.Count.ToString()));
        summary.AddRow(new Markup("[dim]exact[/]"), new Markup(packageSignals.Count(entry => string.Equals(entry.Kind, "Exact", StringComparison.Ordinal)).ToString()));
        summary.AddRow(new Markup("[dim]prefix[/]"), new Markup(packageSignals.Count(entry => string.Equals(entry.Kind, "Prefix", StringComparison.Ordinal)).ToString()));
        summary.AddRow(new Markup("[dim]skills[/]"), new Markup(skillCatalog.Skills.Count.ToString()));
        AnsiConsole.Write(new Panel(summary).Header("[deepskyblue1]package signals[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        var table = new Table().Expand().Border(TableBorder.Rounded);
        table.Title = new TableTitle("[bold]NuGet entry points[/]");
        table.AddColumn("Signal");
        table.AddColumn("Kind");
        table.AddColumn("Skill");
        table.AddColumn("Area");
        table.AddColumn("Tokens");

        foreach (var entry in packageSignals)
        {
            table.AddRow(
                Escape(entry.Signal),
                Escape(entry.Kind),
                Escape(ToAlias(entry.Skill.Name)),
                Escape($"{entry.Skill.Stack} / {entry.Skill.Lane}"),
                FormatTokenCount(entry.Skill.TokenCount));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup("[dim]Package signals connect exact NuGet ids and prefixes to the skill that should be installed when that package appears in a project.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
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

        var summary = new Grid();
        summary.AddColumn(new GridColumn().NoWrap());
        summary.AddColumn();
        summary.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        summary.AddRow(new Markup("[dim]mode[/]"), new Markup(force ? "Repair / overwrite" : "Install"));
        summary.AddRow(new Markup("[dim]skills[/]"), new Markup(selectedSkills.Length.ToString()));
        summary.AddRow(new Markup("[dim]stacks[/]"), new Markup(stackCount.ToString()));
        summary.AddRow(new Markup("[dim]tokens[/]"), new Markup(FormatTokenCount(totalTokens)));
        if (bundles is not null)
        {
            summary.AddRow(new Markup("[dim]bundles[/]"), new Markup(bundles.Count.ToString()));
        }

        var stackTable = new Table().Expand().Border(TableBorder.Rounded);
        stackTable.Title = new TableTitle("[bold]Write set by stack[/]");
        stackTable.AddColumn("Stack");
        stackTable.AddColumn("Lanes");
        stackTable.AddColumn("Skills");
        stackTable.AddColumn("Tokens");

        foreach (var group in selectedSkills
                     .GroupBy(skill => skill.Stack, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => CatalogOrganization.GetStackRank(group.Key))
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            var lanes = group
                .Select(skill => skill.Lane)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(CatalogOrganization.GetLaneRank)
                .ThenBy(lane => lane, StringComparer.Ordinal)
                .ToArray();
            stackTable.AddRow(
                Escape(group.Key),
                Escape(string.Join(", ", lanes.Take(3))) + (lanes.Length > 3 ? $" [grey](+{lanes.Length - 3})[/]" : string.Empty),
                group.Count().ToString(),
                FormatTokenCount(group.Sum(skill => skill.TokenCount)));
        }

        var skillTable = new Table().Expand().Border(TableBorder.Rounded);
        skillTable.Title = new TableTitle("[bold]Selected skills[/]");
        skillTable.AddColumn("Skill");
        skillTable.AddColumn("Area");
        skillTable.AddColumn("Tokens");

        foreach (var skill in selectedSkills.Take(12))
        {
            skillTable.AddRow(
                Escape(ToAlias(skill.Name)),
                Escape($"{skill.Stack} / {skill.Lane}"),
                FormatTokenCount(skill.TokenCount));
        }

        var headerGrid = new Grid();
        headerGrid.AddColumn();
        headerGrid.AddColumn();
        headerGrid.AddRow(
            new Panel(summary).Header($"[deepskyblue1]{Escape(title)}[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(skillTable).Header("[deepskyblue1]selected skills[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(headerGrid);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(stackTable).Header("[deepskyblue1]install overview[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.WriteLine();

        if (bundles is not null && bundles.Count > 0)
        {
            var bundleTable = new Table().Expand().Border(TableBorder.Rounded);
            bundleTable.Title = new TableTitle("[bold]Selected bundles[/]");
            bundleTable.AddColumn("Bundle");
            bundleTable.AddColumn("Area");
            bundleTable.AddColumn("Skills");

            foreach (var bundle in bundles.OrderBy(bundle => bundle.Name, StringComparer.Ordinal))
            {
                bundleTable.AddRow(
                    Escape(bundle.Name),
                    Escape(CatalogOrganization.ResolveBundleAreaLabel(bundle)),
                    bundle.Skills.Count.ToString());
            }

            AnsiConsole.Write(bundleTable);
            AnsiConsole.WriteLine();
        }

        AnsiConsole.Write(new Panel(new Markup("[dim]This preview is the exact write set that will be installed into the selected target if you confirm.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderSkillDetailPanel(SkillEntry skill, InstalledSkillRecord? installed, SkillInstallLayout layout)
    {
        var summary = new Grid();
        summary.AddColumn(new GridColumn().NoWrap());
        summary.AddColumn();
        summary.AddRow(new Markup("[dim]alias[/]"), new Markup(Escape(ToAlias(skill.Name))));
        summary.AddRow(new Markup("[dim]skill[/]"), new Markup($"[dim]{Escape(skill.Name)}[/]"));
        summary.AddRow(new Markup("[dim]area[/]"), new Markup($"{Escape(skill.Stack)} [dim]/[/] {Escape(skill.Lane)}"));
        summary.AddRow(new Markup("[dim]category[/]"), new Markup(Escape(skill.Category)));
        summary.AddRow(new Markup("[dim]version[/]"), new Markup(Escape(skill.Version)));
        summary.AddRow(new Markup("[dim]tokens[/]"), new Markup($"{FormatTokenCount(skill.TokenCount)} [dim]({Escape(SkillTokenCounter.ModelName)})[/]"));
        summary.AddRow(new Markup("[dim]status[/]"), new Markup(installed is null ? "[dim]not installed[/]" : $"{Escape(installed.InstalledVersion)} {(installed.IsCurrent ? "[green](current)[/]" : "[yellow](update available)[/]")}"));
        summary.AddRow(new Markup("[dim]compat[/]"), new Markup(Escape(skill.Compatibility)));
        summary.AddRow(new Markup("[dim]target[/]"), new Markup($"[dim]{Escape(layout.PrimaryRoot.FullName)}[/]"));
        summary.AddRow(new Markup("[dim]install[/]"), new Markup($"[green]{Escape($"dotnet skills install {ToAlias(skill.Name)}")}[/]"));

        var surface = new Table().Border(TableBorder.None).Expand();
        surface.AddColumn("Surface");
        surface.AddColumn("Value");
        surface.AddRow("NuGet packages", skill.Packages.Count == 0 ? "[dim]-[/]" : Escape(string.Join(", ", skill.Packages.Take(4))));
        surface.AddRow("Package prefix", string.IsNullOrWhiteSpace(skill.PackagePrefix) ? "[dim]-[/]" : Escape($"{skill.PackagePrefix}.*"));
        surface.AddRow("Docs", string.IsNullOrWhiteSpace(skill.Links.Docs) ? "[dim]not declared[/]" : "[green]available[/]");
        surface.AddRow("Repository", string.IsNullOrWhiteSpace(skill.Links.Repository) ? "[dim]not declared[/]" : "[green]available[/]");
        surface.AddRow("NuGet link", string.IsNullOrWhiteSpace(skill.Links.NuGet) ? "[dim]not declared[/]" : "[green]available[/]");

        var headerGrid = new Grid();
        headerGrid.AddColumn();
        headerGrid.AddColumn();
        headerGrid.AddRow(
            new Panel(summary).Header($"[deepskyblue1]{Escape(ToAlias(skill.Name))}[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(surface).Header("[deepskyblue1]skill surface[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(headerGrid);
        AnsiConsole.WriteLine();

        var bodyGrid = new Grid();
        bodyGrid.AddColumn();
        bodyGrid.AddColumn();
        bodyGrid.AddRow(
            new Panel(new Markup(Escape(skill.Description))).Border(BoxBorder.Rounded).Header("[deepskyblue1]summary[/]").Expand(),
            new Panel(new Markup(Escape(LoadSkillPreview(skill)))).Border(BoxBorder.Rounded).Header("[deepskyblue1]preview[/]").Expand());
        AnsiConsole.Write(bodyGrid);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup("[dim]Preview comes from the current[/] [green]SKILL.md[/] [dim]payload, so token size and visible content stay aligned.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
    }

    private void RenderPackageDetailPanel(SkillPackageEntry package)
    {
        var skillIndex = skillCatalog.Skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
        var tokenCount = package.Skills.Sum(skillName => skillIndex.TryGetValue(skillName, out var skill) ? skill.TokenCount : 0);
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn();
        grid.AddRow(new Markup("[dim]bundle[/]"), new Markup(Escape(package.Name)));
        grid.AddRow(new Markup("[dim]area[/]"), new Markup(Escape(CatalogOrganization.ResolveBundleAreaLabel(package))));
        grid.AddRow(new Markup("[dim]type[/]"), new Markup(Escape(package.Kind)));
        grid.AddRow(new Markup("[dim]skills[/]"), new Markup(package.Skills.Count.ToString()));
        grid.AddRow(new Markup("[dim]tokens[/]"), new Markup(FormatTokenCount(tokenCount)));
        grid.AddRow(new Markup("[dim]install[/]"), new Markup($"[green]{Escape($"dotnet skills install bundle {package.Name}")}[/]"));

        var coverage = new Table().Border(TableBorder.None).Expand();
        coverage.AddColumn("Coverage");
        coverage.AddColumn("Value");
        coverage.AddRow("Stack", Escape(package.Stack));
        coverage.AddRow("Lane", Escape(package.Lane));
        coverage.AddRow("Included skills", package.Skills.Count.ToString());
        coverage.AddRow("Tokenizer", Escape(SkillTokenCounter.ModelName));

        var headerGrid = new Grid();
        headerGrid.AddColumn();
        headerGrid.AddColumn();
        headerGrid.AddRow(
            new Panel(grid).Header($"[deepskyblue1]{Escape(package.Name)}[/]").Border(BoxBorder.Rounded).Expand(),
            new Panel(coverage).Header("[deepskyblue1]bundle coverage[/]").Border(BoxBorder.Rounded).Expand());
        AnsiConsole.Write(headerGrid);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(Escape(package.Description))).Border(BoxBorder.Rounded).Header("[deepskyblue1]summary[/]").Expand());
        AnsiConsole.WriteLine();

        var table = new Table().Expand();
        table.Title = new TableTitle("Included skills");
        table.AddColumn("Alias");
        table.AddColumn("Area");
        table.AddColumn("Tokens");

        foreach (var skillName in package.Skills.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!skillIndex.TryGetValue(skillName, out var skill))
            {
                table.AddRow(Escape(ToAlias(skillName)), "[dim]missing[/]", "0");
                continue;
            }

            table.AddRow(
                Escape(ToAlias(skillName)),
                Escape($"{skill.Stack} / {skill.Lane}"),
                FormatTokenCount(skill.TokenCount));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup("[dim]Focused bundles stay narrow by stack or workflow. Broad category-wide installs are intentionally not exposed here.[/]"))
            .Header("[deepskyblue1]status rail[/]")
            .Border(BoxBorder.Rounded)
            .Expand());
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

    private static string BuildStackChoiceLabel(StackCatalogView stack)
    {
        return $"{stack.Stack} ({stack.Lanes.Count} lanes, {stack.InstalledCount}/{stack.SkillCount} skills, {FormatTokenCount(stack.TokenCount)} tokens)";
    }

    private static string BuildLaneChoiceLabel(LaneCatalogView lane)
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
    T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull;

    IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull;

    bool Confirm(string title, bool defaultValue);

    void Pause(string title);
}

internal sealed class CommandCenterInteractivePrompts : IInteractivePrompts
{
    public T Select<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (choices.Count == 0)
        {
            throw new InvalidOperationException($"No choices are available for {title}.");
        }

        if (CanUseSpectrePrompts())
        {
            try
            {
                var labels = choices.Select(formatter).ToArray();
                var prompt = new Spectre.Console.SelectionPrompt<string>
                {
                    Title = $"[deepskyblue1]{EscapeMarkup(title)}[/]",
                    PageSize = Math.Min(Math.Max(labels.Length, 5), 18),
                    HighlightStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Aqua),
                };
                prompt.AddChoices(labels);
                var selectedLabel = SpectreConsole.Prompt(prompt);
                var index = Array.FindIndex(labels, label => string.Equals(label, selectedLabel, StringComparison.Ordinal));
                if (index >= 0)
                {
                    return choices[index];
                }
            }
            catch
            {
                // Fall back to plain-text prompts when the terminal does not support Spectre well.
            }
        }

        return SelectPlainText(title, choices, formatter);
    }

    public IReadOnlyList<T> MultiSelect<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
        if (choices.Count == 0)
        {
            return [];
        }

        if (CanUseSpectrePrompts())
        {
            try
            {
                var labels = choices.Select(formatter).ToArray();
                var prompt = new Spectre.Console.MultiSelectionPrompt<string>
                {
                    Title = $"[deepskyblue1]{EscapeMarkup(title)}[/]",
                    InstructionsText = "[dim](Press <space> to toggle, <enter> to accept)[/]",
                    PageSize = Math.Min(Math.Max(labels.Length, 5), 18),
                    HighlightStyle = new Spectre.Console.Style(foreground: Spectre.Console.Color.Aqua),
                };
                prompt.NotRequired();
                prompt.AddChoices(labels);
                var selectedLabels = SpectreConsole.Prompt(prompt);
                var selectedSet = selectedLabels.ToHashSet(StringComparer.Ordinal);
                var selected = labels
                    .Select((label, index) => (label, index))
                    .Where(item => selectedSet.Contains(item.label))
                    .Select(item => choices[item.index])
                    .ToArray();
                return selected;
            }
            catch
            {
                // Fall back to plain-text prompts when the terminal does not support Spectre well.
            }
        }

        return MultiSelectPlainText(title, choices, formatter);
    }

    public bool Confirm(string title, bool defaultValue)
    {
        if (CanUseSpectrePrompts())
        {
            try
            {
                return SpectreConsole.Confirm($"[deepskyblue1]{EscapeMarkup(title)}[/]", defaultValue);
            }
            catch
            {
                // Fall back to plain-text prompts when the terminal does not support Spectre well.
            }
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

    private static bool CanUseSpectrePrompts()
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

        var terminal = Environment.GetEnvironmentVariable("TERM");
        return !string.IsNullOrWhiteSpace(terminal)
            && !string.Equals(terminal, "dumb", StringComparison.OrdinalIgnoreCase);
    }

    private static T SelectPlainText<T>(string title, IReadOnlyList<T> choices, Func<T, string> formatter) where T : notnull
    {
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

    private static string EscapeMarkup(string value)
    {
        return value
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
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

internal enum HomeAction
{
    SyncProject,
    InstallSkillStack,
    InstallSkills,
    Analysis,
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
