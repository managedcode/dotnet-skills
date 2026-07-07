# SharpConsoleUI control reference

SharpConsoleUI ships 40+ built-in controls. All implement `IWindowControl` and
are added to a window (or container) with `AddControl(...)`. Interactive controls
add `IInteractiveControl` (keyboard), `IFocusableControl` (focus), and/or
`IMouseAwareControl` (mouse). Most controls implement `IRoleableControl`, so a
semantic `Role` (a `ColorRole` value: Primary, Success, Danger, …) pulls colors
from the active theme instead of hand-set colors. Prefer the `Controls` static factory + fluent
builders (`Controls.Button("Quit").OnClick(...).Build()`).

Common properties on every control: `Name` (for `FindControl<T>(name)`),
`Visible`, `Container`, `Tag`, and layout properties (`Width`, `Margin`,
`Alignment`, `StickyPosition`). To move focus programmatically, the simplest call
is `window.FocusControl(control)` (a convenience over `window.FocusManager.SetFocus(...)`).

## Text and markup

Use when: displaying styled text, editing text, or rendering markdown/HTML.

- `MarkupControl` — `Controls.Markup()` / `Controls.Markup("[bold]hi[/]")`. Rich `[tag]text[/]` styling, clickable/keyboard links (`[link=url]`), inline `[markdown]`, `[spinner]`, `[gradient=…]`; optional border/header via `.WithBorder()`. The default label control. Shorthand for a one-line static label: `Controls.Label("[bold]text[/]")` returns a pre-wrapped `MarkupControl`.
- `MultilineEditControl` — multi-line editor with syntax highlighting (13 languages via `SyntaxHighlighters.For(...)`), gutter, find/replace, undo/redo, word wrap.
- `PromptControl` — single-line text input; Enter events, validation, max length.
- `HtmlControl` — parse & render HTML (images, links, tables). One documented NativeAOT caveat.
- `FigleControl` — large ASCII-art (Figlet) text in multiple fonts.

## Input

Use when: capturing user actions or values.

- `ButtonControl` — `Controls.Button("Text").OnClick((sender, e, win) => …)`. Focusable; Enter/click fires `Click`. The `win` arg reaches sibling controls via `FindControl<T>()`.
- `CheckboxControl` — toggle with label; checked/unchecked change events.
- `RadioControl` — single-select group via typed `RadioGroup<T>`; `Required`/`AllowDeselect`, cross-layout grouping.
- `DropdownControl` — click-to-expand selection list; keyboard nav; renders via portal.
- `SliderControl` / `RangeSliderControl` — value slider / dual-thumb range slider; step/large-step, keyboard + mouse drag; `RangeSlider` enforces `MinRange`.
- `DatePickerControl` / `TimePickerControl` — locale-aware pickers; segmented editing, calendar popup, 12h/24h, min/max.
- `FormControl` — labeled two-column form; typed field overloads (text/multiline/checkbox/dropdown/radio/slider), sections, validation, `GetValues`/`Submit`/`Submitted`. Also loadable from declarative XML (see `references/recipes.md`).

## Data and collections

Use when: showing rows, lists, or hierarchies.

- `TableControl` — interactive data grid: virtual data, sorting, filtering (AND/OR), inline editing, multi-select, cell navigation, scrollbars.
- `ListControl` — scrollable single-select list with item activation and keyboard nav.
- `TreeControl` — hierarchical tree; expand/collapse, selection, keyboard nav.
- `GridControl` — WinUI-`<Grid>`-style 2D layout: Fixed/Auto/Star tracks, row/col spans, gaps, any control per cell. `Controls.Grid()`; see the grid recipe in `references/recipes.md`.
- `HorizontalGridControl` — single-row multi-column layout with variable-width columns and splitters.

## Navigation and layout

Use when: organizing screens, panes, and chrome.

- `MenuControl` — menu bar with dropdowns/submenus, separators, keyboard shortcuts.
- `NavigationView` — WinUI-inspired sidebar nav + content area; responsive Expanded/Compact/Minimal modes; content factories.
- `TabControl` — multi-page tabs; keyboard/mouse switching; per-tab state preserved.
- `WizardControl` / `FlowControl` — run a multi-step wizard / a composed flow inline in a pane (vs. modal); `wizard.Run(Flow.Wizard<T>()…)`.
- `CollapsiblePanel` — click-to-expand container; can also serve as a plain panel via `.NonCollapsible()` / `.HideHeader()`.
- `ScrollablePanelControl` — vertical scrolling content area hosting multiple controls (compose with `GridControl` for the `<Grid>`-in-`<ScrollViewer>` pattern).
- `PanelControl` — bordered container hosting child controls.
- `ToolbarControl` — horizontal button toolbar; auto-height, wrapping, Tab nav.
- `StatusBarControl` — three-zone (left/center/right) status bar; clickable items, shortcut hints.
- `RuleControl` / `SeparatorControl` — horizontal rule/separator, optional title. Factory `Controls.Rule()` / `Controls.RuleBuilder()` (a heavily used divider in real apps).

## Charts and status

Use when: showing metrics, progress, or logs.

- `LineGraphControl` — multi-series line graph; braille/ASCII modes, gradients, Y-axis labels, live updates.
- `BarGraphControl` — horizontal bar graph with gradient thresholds and value labels.
- `SparklineControl` — compact time-series sparkline (block/braille/bidirectional).
- `SpinnerControl` — animated indeterminate-progress spinner; presets or custom frames; also the inline `[spinner]` markup tag.
- `ProgressBarControl` — determinate progress bar; `Controls.ProgressBar()`, `Value` 0.0–1.0 (single solid fill; use `CanvasControl` for a gradient bar).
- `LogViewerControl` — log viewer with auto-scroll, filtering, severity colors.

## Drawing and media

Use when: custom graphics, images, or video.

- `CanvasControl` — free-form drawing surface; 30+ primitives, retained & immediate modes, thread-safe async painting.
- `ImageControl` — image display via Kitty/WezTerm/Ghostty full-resolution with half-block fallback; PNG/JPEG/BMP/GIF/WebP/TIFF; async load. (Not `IRoleableControl`.)
- `VideoControl` — terminal video player; Kitty graphics + half-block/ASCII/braille fallbacks (auto-detected); FFmpeg decode; overlay bar; looping.
- `SpectreRenderableControl` — wrap and display any Spectre.Console renderable (Table, Tree, Panel, Chart, …).
- `ChatTranscriptControl` — agent/chat transcript: role-tagged messages, token-by-token streaming, collapsible tool messages, thinking indicator.

## Terminal and portal (special)

- `TerminalControl` — PTY-backed terminal emulator; full xterm-256color, keyboard/mouse passthrough, auto-resize. Two build paths: `Controls.Terminal("bash"|"pwsh"|…).Open(ws)` opens it in its own window; or `Controls.Terminal().WithWorkingDirectory(dir).WithExe(...).WithArgs(...).Build()` returns a `TerminalControl` you place like any control (e.g. `tabControl.AddTab("Shell", term)`). Default exe: `bash` on Linux, `cmd.exe` on Windows. Runs on **Linux and Windows 10 1809+** (openpty on Linux, ConPTY on Windows); throws `PlatformNotSupportedException` elsewhere. **On Linux you must call `PtyShim.RunIfShim(args)` as the very first line of `Main`** (before any UI init) or the child process never starts — it is a safe no-op on Windows, so keep it in cross-platform code.
- `PortalContentContainer` — host child controls in [portal overlays](https://nickprotop.github.io/ConsoleEx/) (dropdowns, menus, tooltips) with mouse/keyboard routing and focus tracking. See the portal recipe in `references/recipes.md`.

For wiring these into windows and layouts, see `references/recipes.md`.
