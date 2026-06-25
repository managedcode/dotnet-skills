using ManagedCode.DotnetSkills.Runtime;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Rectangle = System.Drawing.Rectangle;

namespace ManagedCode.DotnetSkills;

/// <summary>
/// Command palette as a portal overlay (not a modal window). Commands-first: the default view lists
/// curated <see cref="SkillCommand"/>s from the registry; typing a leading '/' switches to searching
/// the content list (skills/bundles/agents). Filters live on every keystroke. Modeled on LazyDotIDE's
/// CommandPalettePortal.
/// </summary>
internal sealed class CommandPalettePortal : PortalContentContainer
{
    private const int PaletteMaxWidth = 85;
    private const int PaletteMaxHeight = 22;

    private readonly CommandRegistry _registry;
    private readonly IReadOnlyList<PaletteEntry> _content;
    private readonly PromptControl _searchInput;
    private readonly ListControl _list;
    private readonly MarkupControl _statusText;

    public event EventHandler<SkillCommand?>? CommandChosen;
    public event EventHandler<PaletteEntry?>? ContentChosen;

    public CommandPalettePortal(CommandRegistry registry, IReadOnlyList<PaletteEntry> content, int windowWidth, int windowHeight)
    {
        _registry = registry;
        _content = content;

        DismissOnOutsideClick = true;
        BorderStyle = BoxChars.Rounded;
        BorderColor = Color.Grey50;
        BorderBackgroundColor = Color.Grey15;
        BackgroundColor = Color.Grey15;
        ForegroundColor = Color.Grey93;

        _searchInput = Controls.Prompt("> ")
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 0)
            .Build();
        AddChild(_searchInput);

        AddChild(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        _list = Controls.List()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(Color.Grey93, Color.Grey15)
            .WithFocusedColors(Color.Grey93, Color.Grey15)
            .WithHighlightColors(Color.White, Color.Grey35)
            .WithDoubleClickActivation(true)
            .WithTitle(string.Empty)
            .Build();
        AddChild(_list);

        AddChild(Controls.RuleBuilder().WithColor(Color.Grey23).StickyBottom().Build());

        _statusText = Controls.Markup()
            .AddLine($"[grey50]{_registry.All.Count} commands[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 0, 1, 0)
            .StickyBottom()
            .Build();
        AddChild(_statusText);

        AddChild(Controls.Markup()
            .AddLine("[grey70]Enter: run  •  / : search content  •  Esc: cancel  •  ↑↓: navigate[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .StickyBottom()
            .Build());

        int w = Math.Min(PaletteMaxWidth, windowWidth - 4);
        int h = Math.Min(PaletteMaxHeight, windowHeight - 2);
        int x = (windowWidth - w) / 2;
        PortalBounds = new Rectangle(x, 1, w, h);

        // total height − border(2) − fixed children (prompt 1 + 2 rules + status 1 + hint 1 = 5)
        _list.MaxVisibleItems = Math.Max(1, h - 2 - 5);

        UpdateList(string.Empty);

        _searchInput.InputChanged += (_, text) => UpdateList(text);
        _list.ItemActivated += (_, item) => Activate(item);

        SetFocusOnFirstChild();
    }

    private void Activate(ListItem? item)
    {
        switch (item?.Tag)
        {
            case SkillCommand cmd: CommandChosen?.Invoke(this, cmd); break;
            case PaletteEntry entry: ContentChosen?.Invoke(this, entry); break;
        }
    }

    private void UpdateList(string rawText)
    {
        _list.ClearItems();
        bool contentMode = rawText.StartsWith("/", StringComparison.Ordinal);

        if (contentMode)
        {
            string q = rawText[1..].Trim();
            var results = (string.IsNullOrEmpty(q)
                    ? _content
                    : _content.Where(e => e.SearchHaystack.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .Take(80).ToList();
            foreach (var e in results)
                _list.AddItem(new ListItem($"[{e.AccentMarkup}]{e.IconLabel}[/]  {e.Label}  [grey50]{e.Detail}[/]") { Tag = e });
            _statusText.SetContent(new List<string> { $"[grey50]{results.Count} result(s) · content[/]" });
        }
        else
        {
            string q = rawText.Trim();
            var results = _registry.Search(q);
            foreach (var c in results)
                _list.AddItem(new ListItem($"{c.Icon}  [grey50]{c.Category,-9}[/]  {c.Label}  [grey50]{c.Keybinding ?? string.Empty}[/]") { Tag = c });
            string status = string.IsNullOrWhiteSpace(q)
                ? $"[grey50]{results.Count} commands · type / to search content[/]"
                : $"[grey50]{results.Count} of {_registry.All.Count} commands[/]";
            _statusText.SetContent(new List<string> { status });
        }

        // Auto-select the first result so Enter activates it without needing a Down first.
        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
    }

    // The portal isn't part of the host window's focus tree (the shell forwards keys here manually), so
    // the usual prompt↔list focus dance via the window FocusManager doesn't apply. Instead the prompt is
    // always "active" for typing (keys delegate to it via base.ProcessKey), Up/Down move the LIST
    // selection directly, and Enter activates it. Simple and focus-independent.
    public new bool ProcessKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
                CommandChosen?.Invoke(this, null); // null command → shell dismisses without executing
                return true;

            case ConsoleKey.Enter:
                Activate(_list.SelectedItem);
                return true;

            case ConsoleKey.DownArrow:
                MoveSelection(+1);
                return true;

            case ConsoleKey.UpArrow:
                MoveSelection(-1);
                return true;

            case ConsoleKey.PageDown:
                MoveSelection(+(_list.MaxVisibleItems ?? 10));
                return true;

            case ConsoleKey.PageUp:
                MoveSelection(-(_list.MaxVisibleItems ?? 10));
                return true;

            case ConsoleKey.Home:
                SetSelection(0);
                return true;

            case ConsoleKey.End:
                SetSelection(_list.Items.Count - 1);
                return true;
        }

        // Everything else (typing, backspace) goes to the focused child — the prompt — which updates
        // the query and re-filters via InputChanged.
        base.ProcessKey(key);
        return true; // swallow all keys while the palette is open
    }

    private void MoveSelection(int delta)
    {
        int count = _list.Items.Count;
        if (count == 0) return;
        int cur = _list.SelectedIndex < 0 ? 0 : _list.SelectedIndex;
        _list.SelectedIndex = Math.Clamp(cur + delta, 0, count - 1);
    }

    private void SetSelection(int index)
    {
        int count = _list.Items.Count;
        if (count == 0) return;
        _list.SelectedIndex = Math.Clamp(index, 0, count - 1);
    }
}
