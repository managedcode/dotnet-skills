# SharpConsoleUI recipes

Verified patterns for wiring windows, layout, dialogs, portals/toasts, and forms.
All code uses APIs from the SharpConsoleUI docs; do not invent method names.
Assumes `windowSystem` is a `ConsoleWindowSystem` and `using` directives for
`SharpConsoleUI`, `SharpConsoleUI.Builders`, `SharpConsoleUI.Controls`,
`SharpConsoleUI.Drivers`.

Two idioms real apps use that keep code shorter:

- **`.Build()`** — control builders have an implicit conversion to their control,
  so `.Build()` is technically optional in an `AddControl(...)` argument. Real apps
  still call it explicitly for clarity and because you usually keep the control in a
  variable (to look it up, focus, or bind it later); prefer showing `.Build()`.
- **Run the loop off the entry thread:** `await Task.Run(() => windowSystem.Run())`
  is the common way to start it so `Main` can stay `async`.

If you use `TerminalControl` anywhere, make the **first line of `Main`**
`if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;` — on Linux the host
re-launches itself as the PTY child, and this short-circuits that re-entry (a safe
no-op returning `false` on Windows). See `references/controls.md`.

## Window builder configuration

`WindowBuilder` is a fluent API; `.Build()` returns the window (add it with
`windowSystem.AddWindow(window)`), and `.BuildAndShow()` builds and shows in one
call.

```csharp
var window = new WindowBuilder(windowSystem)
    .WithTitle("My Window")
    .WithSize(80, 25)
    .AtPosition(4, 2)                    // or .Centered()
    .WithMinimumSize(40, 10)
    .WithColors(Color.White, Color.Grey11)
    .WithPadding(1)                      // inner space; also (h, v) or new Padding(l,t,r,b)
    .Movable(true)
    .Resizable(true)
    .Build();
```

Border/chrome options: `.Borderless()` keeps the invisible 1-cell frame (still
interactive); `.Frameless()` reclaims the frame entirely (chrome-less,
non-interactive frame — content fills the whole rect).

## Full-screen / single-window app

SharpConsoleUI is well suited to full-screen TUIs, not just floating windows.
For a full-screen app the main window should have **no title bar and no title
buttons** — use `.Frameless()`, which reclaims the border/title frame entirely:
no title bar, no drag handle, no resize grip, and no title buttons, with content
filling the whole rect. Combine it with `.Maximized()` (size to the whole
desktop):

```csharp
new WindowBuilder(windowSystem)
    .Frameless()            // no title bar, no title buttons — content owns the whole rect
    .Maximized()            // fills the terminal
    .WithPadding(1)         // optional breathing room
    .AddControl(rootLayout) // e.g. a GridControl that owns the whole screen
    .BuildAndShow();
```

Do not use `.Borderless()` for this: it blanks the border but keeps the invisible
1-cell interactive frame. `.Frameless()` is the chrome-less, title-less choice for
a full-screen main window.

Real single-window apps often keep a bordered window but lock it down instead of
going frameless — the same visual result with a title kept for the theme:

```csharp
var system = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    myTheme,                                   // ITheme overload (see references/system.md)
    new ConsoleWindowSystemOptions(InstallSynchronizationContext: true,
        ShowTopPanel: false, ShowBottomPanel: false));

var window = new WindowBuilder(system)
    .WithTitle("app")
    .Maximized()
    .Movable(false).Resizable(false).Closable(false).HideTitle()
    .WithPadding(1, 0)
    .Build();
system.AddWindow(window);

system.RegisterGlobalShortcut(ConsoleModifiers.Control, ConsoleKey.Q, () => system.RequestExit(0));
var exitCode = system.Run();   // blocks; returns the code passed to RequestExit/Shutdown
```

`RegisterGlobalShortcut(modifiers, key, Action | Func<bool>)` registers an
app-wide hotkey; `system.RequestExit(code)` (or `system.Shutdown(code)`) ends the
loop and makes `Run()` return that code. `.HideTitle()` hides the title text while
keeping the bordered frame; `.Closable(false)` removes the close button.

## Multi-window desktop

Full-screen and multi-window are equally first-class. A `ConsoleWindowSystem`
owns any number of bordered windows that stack in z-order and can be
moved/resized/minimized/maximized. Add several windows; each is a `WindowBuilder`
that `.BuildAndShow()`s onto the desktop:

```csharp
var explorer = new WindowBuilder(windowSystem)
    .WithTitle("Explorer")
    .WithBounds(2, 2, 40, 20)            // x, y, width, height in desktop coords
    .WithBorderStyle(BorderStyle.Rounded)
    .AddControl(tree)
    .BuildAndShow();

var editor = new WindowBuilder(windowSystem)
    .WithTitle("Editor")
    .WithBounds(44, 2, 60, 24)
    .Movable(true)
    .Resizable(true)
    .AddControl(multilineEdit)
    .BuildAndShow();
```

Windows draw a border and title bar and are interactive by default (drag the
title to move, edges to resize). Border styles: `DoubleLine` (default), `Single`,
`Rounded`, `None` (invisible frame, still interactive), `Frameless` (no frame).
For a window the user must dismiss before returning to the rest, make it modal
with `.AsModal()` (or `.WithModal(true)`). Runtime state control:
`window.Maximize()`, `window.Minimize()`, `window.Restore()`; positions are in
desktop coordinates (excluding any status bars).

Window state also has `.Minimized()` and `.WithState(WindowState.Normal |
Maximized | Minimized)`; at runtime `window.Maximize()`, `window.Minimize()`,
`window.Restore()`. Move/resize still work in code on a frameless window
(`SetPosition` / `SetSize`) — only the mouse grab surface is gone.

## Grid layout with a data control

`GridControl` (`Controls.Grid()`) is the 2D layout primitive. Tracks are
`GridLength.Cells(n)` (fixed), `.Auto()` (size-to-content), `.Star(weight)`
(proportional). Place controls explicitly with `Place(control, row, col,
rowSpan, colSpan)` or append in row-major order with `.Add(control)`.

```csharp
var table = Controls.Table()
    // ... configure columns/rows per TableControl ...
    .Build();

var grid = Controls.Grid()
    .Columns(GridLength.Cells(20), GridLength.Star(1), GridLength.Star(2))
    .Rows(GridLength.Auto(), GridLength.Star(1, min: 5))
    .RowGap(1)
    .ColumnGap(2)
    .Place(Controls.Markup("[bold]Report[/]").Build(), 0, 0, colSpan: 3) // header row
    .Place(sidebar, 1, 0)
    .Place(mainPanel, 1, 1)
    .Place(table, 1, 2)
    .Build();

window.AddControl(grid);
```

If a Star grid is placed in a content-sizing parent and renders as nothing, set
`grid.ContentSizedStars = true` (self-size at measure, fill at arrange). The grid
never scrolls; wrap it in a `ScrollablePanelControl` for the
`<Grid>`-in-`<ScrollViewer>` pattern.

## Built-in dialogs (confirm / prompt / progress)

`Dialogs` (`SharpConsoleUI.Dialogs`) provides typed, themed modal dialogs usable
from any async handler — no flow setup required.

```csharp
using SharpConsoleUI.Dialogs;

button.ClickAsync += async (s, e) =>
{
    bool ok = await Dialogs.ConfirmAsync(windowSystem, "Save changes", "Save before closing?");
    if (!ok) return;

    string? name = await Dialogs.PromptAsync(windowSystem, "Your name",
        "What should we call you?", initial: "World");

    string? result = await Dialogs.RunWithProgressAsync<string>(windowSystem,
        "Syncing", "Connecting…",
        async (ct, progress) =>
        {
            progress.Report("Downloading…");
            await Task.Delay(1000, ct);
            return "done";
        });
};
```

`ConfirmAsync` returns `bool`, `PromptAsync` returns `string?` (null on cancel),
`RunWithProgressAsync<T>` returns the worker's result. All take an optional
`NotificationSeverityEnum severity` and `Window? parent`.

## Custom modal dialog with a result

Once a dialog needs custom content or buttons beyond confirm/prompt/progress, the
common real-world pattern is a modal window plus a `TaskCompletionSource<T>` that
`Run()`-style callers can `await`. Build the window `.AsModal()`, close it with
`window.Close()`, and complete the source with the result:

```csharp
async Task<string?> AskNameAsync(ConsoleWindowSystem ws)
{
    var tcs = new TaskCompletionSource<string?>();

    var input = Controls.Prompt("Name").WithName("name").Build();
    var dialog = new WindowBuilder(ws)
        .WithTitle("Rename")
        .WithSize(40, 8)
        .Centered()
        .AsModal()
        .Build();

    dialog.AddControl(input);
    dialog.AddControl(Controls.Button("OK")
        .OnClick((s, e, win) => { tcs.TrySetResult(input.Input); ws.CloseModalWindow(dialog); })
        .Build());
    dialog.AddControl(Controls.Button("Cancel")
        .OnClick((s, e, win) => { tcs.TrySetResult(null); ws.CloseModalWindow(dialog); })
        .Build());

    ws.AddWindow(dialog);
    dialog.FocusControl(input);
    return await tcs.Task;
}
```

Close a **modal** window with `ws.CloseModalWindow(window)` (not `CloseWindow`) so
the modal stack unwinds correctly; use `ws.CloseWindow(window)` for non-modal
windows. Resolve the result in `OnClosed` or in the button handler via a
`TaskCompletionSource` so the `await`ing caller gets it. Clamp a dialog's size to
the terminal with `ws.DesktopDimensions` before building if content can overflow.

Reach for this only when the built-in `Dialogs` helpers above don't fit; they
cover the common confirm/prompt/progress cases with less code. For file/folder
selection use the built-in `FileDialogs` (namespace `SharpConsoleUI.Dialogs`):
`FileDialogs.ShowFilePickerAsync(ws, …)`, `FileDialogs.ShowFolderPickerAsync(ws, …)`,
`FileDialogs.ShowSaveFileAsync(ws, …)` — each returns `Task<string?>` (null on cancel).

## Toasts and notifications

Two systems, both on the `ConsoleWindowSystem` instance:

```csharp
// ToastService — non-blocking, auto-dismissing corner overlays for transient status.
windowSystem.ToastService.Show("Saved successfully", NotificationSeverity.Success);
string id = windowSystem.ToastService.Show("Sync started", NotificationSeverity.Info);
// keep `id` to dismiss it later

// NotificationStateService — title+message, optionally modal (blockUi: true) for
// messages the user must acknowledge.
windowSystem.NotificationStateService.ShowNotification("Error", "Disk full", NotificationSeverity.Error, blockUi: true);
```

Use `ToastService` for "Saved" / "Sync started"; use `NotificationStateService`
for errors/confirmations that must be read.

## Portals (overlays)

Portals render content above the normal layout, unclipped by parent containers —
the mechanism behind dropdowns, context menus, and tooltips. Most controls that
need overlays (e.g. `DropdownControl`, `MenuControl`) manage their own portals.
To host custom overlay content, use `PortalContentContainer`, whose children get
mouse/keyboard routing and focus tracking; portal nodes attach to the root
`LayoutNode` (`AddPortalChild`) and paint last, on top. Reach for a portal when
content must escape its container bounds; reach for `Dialogs` / notifications for
standard modal/transient messages.

## Forms

Build a labeled form imperatively with `FormControl`, or declaratively from XML
with `FormXml` (a thin, NativeAOT-safe call-through to the same runtime).

```csharp
using SharpConsoleUI.Controls.Forms;

var form = FormXml.FromXml(@"
<form title='Contact'>
  <text name='name'  label='Name:'  required='true'/>
  <text name='email' label='Email:' pattern='^[^@]+@[^@]+$' message='Enter a valid email'/>
  <buttons/>
</form>");

window.AddControl(form);
```

The XML root must be `<form>`; fields (`<text>`, `<multiline>`, `<checkbox>`,
`<dropdown>`, `<radio>`, `<slider>`) map to the typed `FormControl` field
overloads, with structure elements `<section>`, `<row>`, `<buttons>`. Read values
via the `FormControl` API (`GetValues` / `Submit` / `Submitted`). Use
`FormXml.FromXmlFile(path)` to load from a file.
