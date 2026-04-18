using System.Globalization;
using System.Text;
using SpectreConsole = Spectre.Console.AnsiConsole;

namespace ManagedCode.DotnetSkills;

internal interface IConsoleRenderable
{
    string Render();
}

internal static class AnsiConsole
{
    public static void Clear()
    {
        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
        }
    }

    public static void WriteLine() => Console.WriteLine();

    public static void Write(object? value)
    {
        switch (value)
        {
            case null:
                return;
            case Spectre.Console.Rendering.IRenderable spectreRenderable:
                SpectreConsole.Write(spectreRenderable);
                break;
            case IConsoleRenderable renderable:
                Console.Write(renderable.Render());
                break;
            case string text:
                Console.Write(AnsiMarkup.ToAnsi(text));
                break;
            default:
                Console.Write(value);
                break;
        }
    }

    public static void MarkupLine(string markup)
    {
        Console.WriteLine(AnsiMarkup.ToAnsi(markup));
    }

    public static void MarkupLine(string format, params object?[] args)
    {
        MarkupLine(string.Format(CultureInfo.InvariantCulture, format, args));
    }
}

internal sealed class Markup(string text) : IConsoleRenderable
{
    public string Text { get; } = text ?? string.Empty;

    public string Render() => AnsiMarkup.ToAnsi(Text);

    public override string ToString() => Text;

    public static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }
}

internal sealed class Grid : IConsoleRenderable
{
    private readonly List<string[]> rows = [];

    public Grid AddColumn() => this;

    public Grid AddColumn(GridColumn column) => this;

    public Grid AddRow(params object?[] cells)
    {
        rows.Add(cells.Select(ConsoleRender.CoerceMarkup).ToArray());
        return this;
    }

    public string Render()
    {
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var columnCount = rows.Max(row => row.Length);
        var widths = new int[columnCount];
        foreach (var row in rows)
        {
            for (var index = 0; index < row.Length; index++)
            {
                widths[index] = Math.Max(widths[index], ConsoleRender.VisibleLength(AnsiMarkup.ToPlain(row[index])));
            }
        }

        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            for (var index = 0; index < row.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append("  ");
                }

                var cell = AnsiMarkup.ToAnsi(row[index]);
                builder.Append(ConsoleRender.PadRightVisible(cell, widths[index]));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}

internal sealed class GridColumn
{
    public GridColumn NoWrap() => this;
}

internal sealed class ConsoleStack(params IConsoleRenderable[] items) : IConsoleRenderable
{
    private readonly IReadOnlyList<IConsoleRenderable> renderables = items;

    public string Render()
    {
        if (renderables.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < renderables.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine();
            }

            builder.Append(renderables[index].Render().TrimEnd());
        }

        return builder.ToString();
    }
}

internal sealed class Panel(IConsoleRenderable content) : IConsoleRenderable
{
    private string? header;

    public Panel Header(string value)
    {
        header = value;
        return this;
    }

    public Panel Border(BoxBorder border) => this;

    public Panel Expand() => this;

    public string Render()
    {
        var renderedContent = content.Render();
        var allLines = renderedContent.Split(Environment.NewLine, StringSplitOptions.None);
        var splitLines = allLines
            .Where((line, index) => line.Length > 0 || index < allLines.Length - 1)
            .ToArray();
        var lines = splitLines.Length == 0 ? [string.Empty] : splitLines;
        var headerText = header is null ? string.Empty : AnsiMarkup.ToAnsi(header);
        var visibleHeader = header is null ? string.Empty : AnsiMarkup.ToPlain(header);
        var width = Math.Max(
            lines.Select(ConsoleRender.VisibleLength).DefaultIfEmpty(0).Max(),
            ConsoleRender.VisibleLength(visibleHeader));
        width = Math.Min(Math.Max(width, 20), ConsoleRender.MaxContentWidth());

        var builder = new StringBuilder();
        var title = headerText.Length > 0 ? $" {headerText} " : string.Empty;
        var topFill = Math.Max(0, width - ConsoleRender.VisibleLength(title));
        builder.Append('╭').Append(title).Append(new string('─', topFill)).AppendLine("╮");

        foreach (var line in lines)
        {
            var trimmed = ConsoleRender.TruncateVisible(line, width);
            builder.Append('│').Append(ConsoleRender.PadRightVisible(trimmed, width)).AppendLine("│");
        }

        builder.Append('╰').Append(new string('─', width)).AppendLine("╯");
        return builder.ToString();
    }
}

internal sealed class BoxBorder
{
    public static BoxBorder Rounded { get; } = new();
}

internal sealed class Rule : IConsoleRenderable
{
    public Style? Style { get; init; }

    public string Render()
    {
        return $"\u001b[2;90m{new string('─', ConsoleRender.MaxContentWidth())}{AnsiMarkup.Reset}{Environment.NewLine}";
    }
}

internal sealed class Style
{
    public static Style Parse(string value) => new();
}

internal enum TableBorder
{
    None,
    Rounded,
}

internal sealed class Table : IConsoleRenderable
{
    private readonly List<TableColumn> columns = [];
    private readonly List<string[]?> rows = [];
    private TableBorder border = TableBorder.None;
    private bool hideHeaders;

    public TableTitle? Title { get; set; }

    public Table Expand() => this;

    public Table Border(TableBorder value)
    {
        border = value;
        return this;
    }

    public Table HideHeaders()
    {
        hideHeaders = true;
        return this;
    }

    public Table AddColumn(string name)
    {
        columns.Add(new TableColumn(name));
        return this;
    }

    public Table AddColumn(TableColumn column)
    {
        columns.Add(column);
        return this;
    }

    public Table AddRow(params object?[] cells)
    {
        rows.Add(cells.Select(ConsoleRender.CoerceMarkup).ToArray());
        return this;
    }

    public Table AddEmptyRow()
    {
        rows.Add(null);
        return this;
    }

    public string Render()
    {
        var columnCount = Math.Max(columns.Count, rows.Where(row => row is not null).Select(row => row!.Length).DefaultIfEmpty(0).Max());
        if (columnCount == 0)
        {
            return string.Empty;
        }

        var widths = new int[columnCount];
        for (var index = 0; index < columns.Count; index++)
        {
            widths[index] = Math.Max(widths[index], ConsoleRender.VisibleLength(AnsiMarkup.ToPlain(columns[index].Header)));
        }

        foreach (var row in rows.Where(row => row is not null).Cast<string[]>())
        {
            for (var index = 0; index < row.Length; index++)
            {
                widths[index] = Math.Max(widths[index], ConsoleRender.VisibleLength(AnsiMarkup.ToPlain(row[index])));
            }
        }

        var maxTableWidth = ConsoleRender.MaxContentWidth();
        var separators = border == TableBorder.None ? (columnCount - 1) * 2 : (columnCount - 1) * 3 + 2;
        while (widths.Sum() + separators > maxTableWidth && widths.Any(width => width > 12))
        {
            var largest = Array.IndexOf(widths, widths.Max());
            widths[largest]--;
        }

        var builder = new StringBuilder();
        if (Title is not null)
        {
            builder.AppendLine(AnsiMarkup.ToAnsi(Title.Text));
        }

        if (border == TableBorder.Rounded)
        {
            builder.Append('╭').AppendJoin('┬', widths.Select(width => new string('─', width + 2))).AppendLine("╮");
        }

        if (!hideHeaders && columns.Count > 0)
        {
            AppendRow(builder, columns.Select(column => column.Header).ToArray(), widths, header: true);
            if (border == TableBorder.Rounded)
            {
                builder.Append('├').AppendJoin('┼', widths.Select(width => new string('─', width + 2))).AppendLine("┤");
            }
        }

        foreach (var row in rows)
        {
            if (row is null)
            {
                if (border == TableBorder.Rounded)
                {
                    builder.Append('├').AppendJoin('┼', widths.Select(width => new string('─', width + 2))).AppendLine("┤");
                }
                else
                {
                    builder.AppendLine();
                }

                continue;
            }

            AppendRow(builder, row, widths, header: false);
        }

        if (border == TableBorder.Rounded)
        {
            builder.Append('╰').AppendJoin('┴', widths.Select(width => new string('─', width + 2))).AppendLine("╯");
        }

        return builder.ToString();
    }

    private void AppendRow(StringBuilder builder, IReadOnlyList<string> cells, IReadOnlyList<int> widths, bool header)
    {
        if (border == TableBorder.Rounded)
        {
            builder.Append('│');
        }

        for (var index = 0; index < widths.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(border == TableBorder.Rounded ? "│" : "  ");
            }

            var raw = index < cells.Count ? cells[index] : string.Empty;
            var cell = ConsoleRender.FormatCell(raw, widths[index], header);
            builder.Append(border == TableBorder.Rounded ? $" {cell} " : cell);
        }

        if (border == TableBorder.Rounded)
        {
            builder.Append('│');
        }

        builder.AppendLine();
    }
}

internal sealed class TableTitle(string text)
{
    public string Text { get; } = text;
}

internal sealed class TableColumn(string header)
{
    public string Header { get; } = header;

    public TableColumn NoWrap() => this;

    public TableColumn PadLeft(int value) => this;

    public TableColumn PadRight(int value) => this;
}

internal static class ConsoleRender
{
    public static string CoerceMarkup(object? value)
    {
        return value switch
        {
            null => string.Empty,
            Markup markup => markup.Text,
            IConsoleRenderable renderable => Markup.Escape(renderable.Render()),
            string text => text,
            _ => Markup.Escape(value.ToString() ?? string.Empty),
        };
    }

    public static int MaxContentWidth()
    {
        if (Console.IsOutputRedirected)
        {
            return 120;
        }

        try
        {
            return Math.Clamp(Console.WindowWidth - 4, 60, 160);
        }
        catch (IOException)
        {
            return 120;
        }
    }

    public static string FormatCell(string markup, int width, bool header)
    {
        var plain = AnsiMarkup.ToPlain(markup).ReplaceLineEndings(" ");
        if (VisibleLength(plain) > width)
        {
            plain = width <= 3 ? plain[..width] : $"{plain[..(width - 3)]}...";
            return PadRightVisible(AnsiMarkup.ToAnsi(Markup.Escape(plain)), width);
        }

        var rendered = AnsiMarkup.ToAnsi(header ? $"[bold]{markup}[/]" : markup);
        return PadRightVisible(rendered, width);
    }

    public static int VisibleLength(string value)
    {
        var length = 0;
        var inEscape = false;
        foreach (var character in value)
        {
            if (character == '\u001b')
            {
                inEscape = true;
                continue;
            }

            if (inEscape)
            {
                if (character == 'm')
                {
                    inEscape = false;
                }

                continue;
            }

            length++;
        }

        return length;
    }

    public static string PadRightVisible(string value, int width)
    {
        var visibleLength = VisibleLength(value);
        return visibleLength >= width ? value : value + new string(' ', width - visibleLength);
    }

    public static string TruncateVisible(string value, int width)
    {
        if (VisibleLength(value) <= width)
        {
            return value;
        }

        var plain = StripAnsi(value);
        return width <= 3 ? plain[..width] : $"{plain[..Math.Min(plain.Length, width - 3)]}...";
    }

    private static string StripAnsi(string value)
    {
        var builder = new StringBuilder();
        var inEscape = false;
        foreach (var character in value)
        {
            if (character == '\u001b')
            {
                inEscape = true;
                continue;
            }

            if (inEscape)
            {
                if (character == 'm')
                {
                    inEscape = false;
                }

                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}

internal static class AnsiMarkup
{
    public const string Reset = "\u001b[0m";

    public static string ToAnsi(string markup) => Parse(markup, emitAnsi: true);

    public static string ToPlain(string markup) => Parse(markup, emitAnsi: false);

    private static string Parse(string markup, bool emitAnsi)
    {
        if (string.IsNullOrEmpty(markup))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var emittedAnsi = false;
        for (var index = 0; index < markup.Length; index++)
        {
            var character = markup[index];
            if (character == '\\' && index + 1 < markup.Length && markup[index + 1] is '[' or ']' or '\\')
            {
                builder.Append(markup[++index]);
                continue;
            }

            if (character == '[')
            {
                var close = markup.IndexOf(']', index + 1);
                if (close > index)
                {
                    var tag = markup[(index + 1)..close];
                    if (TryMapTag(tag, emitAnsi, out var mapped))
                    {
                        builder.Append(mapped);
                        emittedAnsi |= mapped.Length > 0;
                        index = close;
                        continue;
                    }
                }
            }

            builder.Append(character);
        }

        if (emittedAnsi)
        {
            builder.Append(Reset);
        }

        return builder.ToString();
    }

    private static bool TryMapTag(string tag, bool emitAnsi, out string mapped)
    {
        mapped = string.Empty;
        if (tag == "/")
        {
            mapped = emitAnsi ? Reset : string.Empty;
            return true;
        }

        var codes = new List<string>();
        foreach (var part in tag.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith('#'))
            {
                continue;
            }

            switch (part.ToLowerInvariant())
            {
                case "bold":
                    codes.Add("1");
                    break;
                case "dim":
                case "grey":
                case "gray":
                    codes.Add("2");
                    codes.Add("90");
                    break;
                case "green":
                    codes.Add("32");
                    break;
                case "red":
                    codes.Add("31");
                    break;
                case "yellow":
                case "orange3":
                    codes.Add("33");
                    break;
                case "blue":
                case "deepskyblue1":
                case "cyan":
                    codes.Add("36");
                    break;
                case "white":
                    codes.Add("37");
                    break;
                case "black":
                    codes.Add("30");
                    break;
                default:
                    return false;
            }
        }

        mapped = emitAnsi && codes.Count > 0 ? $"\u001b[{string.Join(';', codes)}m" : string.Empty;
        return codes.Count > 0;
    }
}
