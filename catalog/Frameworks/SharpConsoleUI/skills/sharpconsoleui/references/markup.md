# SharpConsoleUI markup language

Markup is a first-class, cross-cutting feature: a native Spectre-compatible
`[tag]text[/]` parser that produces typed `Cell` structs directly (no ANSI
intermediate, no external dependency). **It works everywhere text is rendered** —
`MarkupControl` labels, window titles, status bars, table cells, tree nodes,
list items, tooltips, and any control that renders markup — and the same string
can mix colors, decorations, links, spinners, gradients, and Markdown.

Prefer markup for all styled text instead of manual color handling.

## Basic syntax

```
[style]text[/]
```

- `[style]` = any space-separated combination of color names, hex/RGB colors, and decorations.
- `[/]` closes the most recent opening tag; tags nest and `[/]` pops the innermost.
- Escape literal brackets by doubling: `[[` → `[`, `]]` → `]` (e.g. `items[[0]]` shows `items[0]`).

```csharp
"[bold red]Error:[/] Something went wrong"
"[italic underline blue]Fancy blue text[/]"
"[bold]Bold [red]bold+red[/] just bold again[/]"   // nesting
```

## Colors

- **Named:** basic 16 (`red`, `lime`, `cyan`/`aqua`, `grey`/`gray`, …) plus aliases (`darkred`→maroon, etc.) and a large extended set (`orange1`, `deepskyblue1`, `hotpink`, `gold1`, …).
- **Grey scale:** `grey0` (black) … `grey100` (white).
- **Hex:** `[#RRGGBB]`, `[#RGB]`, `[#RRGGBBAA]` (8-digit sets foreground alpha, Porter-Duff "over" the cell background; `AA=00` invisible, `AA=FF` opaque).
- **RGB:** `[rgb(r,g,b)]`.
- **Background:** `on` — `[white on red]`, `[on green] OK [/]`.

```csharp
"[#FF8000]Orange[/]"
"[#00DCDC80]Semi-transparent cyan[/]"   // 50% opacity, composites onto background
"[white on red]Error banner[/]"
```

### Fill background to end of line

`[fillwidth]` is a **self-closing** marker (no `[/]`, emits nothing) placed at
the end of a line; it extends that line's trailing background to the full width
(turns a per-line tint into a solid block). Only affects row-painting hosts like
`MarkupControl`; a no-op in plain parsing. Escape as `[[fillwidth]]`.

```csharp
"[on grey19] Build succeeded [/][fillwidth]"   // full-width shaded banner
```

## Text decorations

`bold`, `dim`, `italic`, `underline`, `strikethrough` (`strike`), `invert`
(`reverse`), `blink` (`slowblink`/`rapidblink`). Combine freely:
`[bold yellow on darkblue]Warning[/]`.

## Inline spinner

`[spinner]` embeds an animated spinner glyph inline in any markup text — it
animates wherever markup renders (labels, status bars, titles, table cells) with
no separate control. Styles: `[spinner]`/`braille` (default), `circle`, `dots`,
`line`, `arc`, `bounce`, `star`, `growvertical`, `growhorizontal`, `toggle`,
`arrow`, `bouncingbar`, `aestheticbar`, `brailledots`, `dotsbounce`. Override
speed with a trailing ms value: `[spinner dots 250]`. The glyph inherits the
surrounding color scope and reserves a fixed width (no reflow). Requires a
running `ConsoleWindowSystem` with animations enabled; renders a static glyph
otherwise. Escape as `[[spinner]]`. For a placeable control, use `SpinnerControl`.

```csharp
"[yellow]Saving [spinner][/]"
```

## Inline Markdown

`[markdown]…[/]` parses its inner content as Markdown (via Markdig) and renders
it as native markup — so it works anywhere markup is accepted and mixes with
ordinary markup. Supports headings (H1–H6), emphasis, inline code, links (text
shown, URL dropped), bullet/numbered/nested lists, blockquotes, horizontal rules,
fenced/indented code blocks, and GitHub-style pipe tables.

- **Non-nesting:** a region ends at the *first* `[/]`. Unclosed → renders to end of string. Escape as `[[markdown]]`.
- **Code-block highlighting:** a fenced block with a language hint is auto syntax-highlighted. Built-ins include `csharp`/`cs`, `bash`/`sh`, `json`, `javascript`/`js`, `css`, `html`, `xml`, `yaml`/`yml`, `razor`, `dockerfile`, `sln`, `diff`, `markdown`/`md`. No/unknown hint → flat shaded block. Register more globally with `SyntaxHighlighters.Register("toml", …)` or per-style via `MarkdownStyle.CodeHighlighters`.
- **Styling** via the `SharpConsoleUI.Configuration.MarkdownStyle` record (theme-agnostic — the parser is static): `CodeForeground`/`CodeBackground`, `QuoteColor`, `LinkColor`, `BorderColor`, `TableRowSeparators` (false=header rule only, true=full grid), `BulletGlyph`, `ListIndent`, `QuoteGlyph`, `H1Color`…`H6Color`. Set `MarkdownStyle.Default` globally or `.WithMarkdownStyle(s => s with { … })` per build.

```csharp
var c = Controls.Markdown("# Report\n\n**Status:** OK\n\n- one\n- two")
    .WithMarkdownStyle(s => s with { LinkColor = Color.Cyan1, TableRowSeparators = true })
    .Build();
window.AddControl(c);
```

Fluent helpers: `Controls.Markdown(text)`, `.AddMarkdown(text)` / `.WithMarkdown(text)`,
`MarkupControl.SetMarkdown(text)`, `MarkupControl.MarkdownStyle`.

## Updating MarkupControl content

`MarkupControl` updates live with a `StringBuilder`/`Console`-style API:

- `Append(text)` — inline append onto the current last line (new line only at embedded `\n`).
- `AppendLine(text)` — append as its own new line.
- `AppendLines(lines)` — each item as its own line.
- `SetContent(lines)` — replace all content.

`Append`/`AppendLine` are the recommended pair (`AppendText`/`AppendInline` remain
as aliases). The builder mirrors this: `Controls.Markup().Append(...).AddLine(...)`.

```csharp
var c = new MarkupControl(new List<string>());
c.Append("[green]●[/] ");
c.Append("all healthy");        // -> "● all healthy" (same line)
c.AppendLine("[grey]done[/]");  // -> next line
```

## MarkupParser API (SharpConsoleUI.Parsing)

For programmatic parsing/measuring outside a control:

- `MarkupParser.Parse(text, defaultFg, defaultBg)` → `List<Cell>` (char + fg + bg + decoration).
- `MarkupParser.ParseLines(text, width, defaultFg, defaultBg)` → `List<List<Cell>>` with word-wrap; style stack carries across line breaks.
- `MarkupParser.StripLength(text)` → visible length (tags stripped; max line length for multi-line).
- `MarkupParser.Truncate(text, maxVisible)` → truncates to visible length, preserving/closing tags.
- `MarkupParser.Escape(text)` → doubles brackets so plain text isn't interpreted (`array[0]` → `array[[0]]`).
- `MarkupParser.Remove(text)` → strips all tags to plain text (escaped brackets become single).

Always run untrusted/dynamic text through `MarkupParser.Escape(...)` before
embedding it in markup, or stray brackets will be parsed as tags.

For `[gradient=…]` see `references/features.md`.
