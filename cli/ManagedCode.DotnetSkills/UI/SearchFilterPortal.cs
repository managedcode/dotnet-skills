using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;
using Rectangle = System.Drawing.Rectangle;

namespace ManagedCode.DotnetSkills;

/// <summary>
/// Live list/table filter as a slim portal overlay (not a modal window). A single search input sits
/// in a slim bar near the top of the window; every keystroke raises <see cref="TextChanged"/> so the
/// shell can re-filter the page behind it live. Enter accepts (closes, keeps the filter), Esc closes.
/// Modeled on <see cref="CommandPalettePortal"/>.
/// </summary>
internal sealed class SearchFilterPortal : PortalContentContainer
{
    private const int MaxWidth = 90;

    private readonly PromptControl _searchInput;
    private readonly MarkupControl _resultCaption;

    /// <summary>Raised on every keystroke with the current (untrimmed) query text.</summary>
    public event EventHandler<string>? TextChanged;

    /// <summary>Raised when the user closes the portal (Esc) or accepts it (Enter).</summary>
    public event EventHandler? Closed;

    public SearchFilterPortal(string initialQuery, int windowWidth, int windowHeight)
    {
        DismissOnOutsideClick = true;
        BorderStyle = BoxChars.Rounded;
        BorderColor = Color.Gold1;
        BorderBackgroundColor = Color.Grey15;
        BackgroundColor = Color.Grey15;
        ForegroundColor = Color.Grey93;

        AddChild(Controls.Markup()
            .AddLine("[grey62] ⌕  Filter the current list — [bold]Enter[/] apply · [bold]Esc[/] close[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 0, 1, 0)
            .Build());

        AddChild(Controls.RuleBuilder().WithColor(Color.Grey23).Build());

        _searchInput = Controls.Prompt(" / ")
            .WithInput(initialQuery ?? string.Empty)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 0, 1, 0)
            .Build();
        AddChild(_searchInput);

        _resultCaption = Controls.Markup()
            .AddLine(string.Empty)
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 0, 1, 0)
            .StickyBottom()
            .Build();
        AddChild(_resultCaption);

        // Slim bar near the top: most of the width, only a few rows tall, so the filtered table
        // stays visible below it.
        int w = Math.Min(MaxWidth, Math.Max(40, windowWidth - 4));
        int h = 6; // border(2) + hint(1) + rule(1) + input(1) + caption(1)
        int x = (windowWidth - w) / 2;
        PortalBounds = new Rectangle(x, 1, w, h);

        _searchInput.InputChanged += (_, text) => TextChanged?.Invoke(this, text);

        SetFocusOnFirstChild();
    }

    /// <summary>Updates the small result caption (e.g. "12 / 40 shown"). Shell calls this after a rebuild.</summary>
    public void SetResultCaption(string markup)
        => _resultCaption.SetContent(new List<string> { markup });

    // The portal isn't part of the host window's focus tree (the shell forwards keys here manually).
    // The prompt is always active for typing; Enter accepts, Esc closes, everything else types.
    public new bool ProcessKey(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.Enter:
                Closed?.Invoke(this, EventArgs.Empty);
                return true;
        }

        // Typing / backspace goes to the focused child (the prompt), which raises InputChanged and
        // re-filters live.
        base.ProcessKey(key);
        return true; // swallow all keys while the portal is open
    }
}
