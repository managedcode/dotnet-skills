# SharpConsoleUI app architecture and UX patterns

How to structure and polish a real production SharpConsoleUI app, distilled from
shipping apps (the cx-/lazy- series and third-party consumers). These are
app-level patterns built on the framework APIs named below — adopt them when
building anything beyond a single-screen demo.

## Single-window shell + content-swap navigation

Most polished apps are **one maximized window**, not many. "Screens" are
content-swap units inside that one window, driven by a small navigator — this
gives full control of every row and a consistent frame.

- Bootstrap once: `new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer), theme, options)`, one `WindowBuilder(...).Maximized().Movable(false).Resizable(false).Closable(false).HideTitle().Build()`, `AddWindow`, `Run()`. Disable the built-in panels (`ShowTopPanel: false, ShowBottomPanel: false`) so the app owns every row.
- Navigator holds a `Stack<IScreen>` with `Root/Push/Replace/Pop/PopToRoot`. Each transition calls `window.ClearControls()` then the top screen's `Build(...)`, which re-adds its controls. A screen abstraction is just an interface you define: a breadcrumb label, a `Build` method, and an optional `HandleKey(ConsoleKeyInfo) -> bool`.
- Guard content swaps against async races: bump a generation counter on every navigation, capture it before a screen's `Build` awaits, and discard the stale continuation's mutations if navigation advanced meanwhile. Prevents a slow screen painting over one the user already left.

```csharp
void Show(IScreen screen)
{
    window.ClearControls();
    window.AddControl(BuildHeader());   // your own header (see below)
    screen.Build(this);                 // screen appends its controls
    window.FocusControl(screen.FirstFocusable);
}
```

## Own your header / status / hints

With the built-in panels off, render your own 3-line header on every navigation:
a live status line, a breadcrumb + right-aligned global hints, and a full-width
rule (`─`). Every screen also appends a bottom **hint bar** listing exactly the
keys its `HandleKey` implements, so hints never drift from behavior. Build all of
it as markup in a `MarkupControl`. To right-align two segments despite markup
tags, measure visible width by stripping tags (see "escaping" below).

## Keyboard-first interaction

- Global keys via `window.PreviewKeyPressed` (fires before controls): offer the key to the current screen's `HandleKey` first, then handle `Esc` = back/pop, `?` = help, etc. App-wide hotkeys via `system.RegisterGlobalShortcut(ConsoleModifiers.Control, ConsoleKey.Q, () => system.RequestExit(0))`.
- Conventions to keep consistent across screens: `Esc` cancels/goes back; `Enter` activates/confirms; single-letter mnemonics for actions; arrow Up/Down for history recall. Resolve list selection on `ListControl.ItemActivated` (Enter/double-click), not `SelectedIndexChanged` (mere highlight).

## Custom modal dialog with an awaitable result

Beyond the built-in `Dialogs.*` helpers, the standard pattern for custom dialogs
is a modal window that a caller can `await`:

```csharp
var tcs = new TaskCompletionSource<int?>();
var (w, h) = ClampSize(system, desiredW, desiredH);   // clamp to desktop, see below
var dialog = new WindowBuilder(system)
    .WithTitle(" Pick ").WithSize(w, h).Centered().AsModal().Build();
list.ItemActivated += (s, e) => { chosen = list.SelectedIndex; system.CloseModalWindow(dialog); };
dialog.PreviewKeyPressed += (s, e) => { if (e.KeyInfo.Key == ConsoleKey.Escape) system.CloseModalWindow(dialog); };
dialog.OnClosed += (_, _) => tcs.TrySetResult(chosen);
dialog.AddControl(list);
system.AddWindow(dialog);
dialog.FocusControl(list);
return await tcs.Task;
```

- Close a **modal** with `system.CloseModalWindow(window)` (not `CloseWindow`) so the modal stack unwinds.
- Clamp every dialog to the terminal so content can't overflow it:

```csharp
(int W, int H) ClampSize(ConsoleWindowSystem s, int dw, int dh) => (
    Math.Clamp(dw, 40, Math.Max(40, s.DesktopDimensions.Width  - 4)),
    Math.Clamp(dh, 10, Math.Max(10, s.DesktopDimensions.Height - 4)));
```

- Use a **toast** (`system.ToastService.Show(msg, NotificationSeverity.Success)`) for don't-interrupt feedback; a **modal** for must-read/must-acknowledge. Disable Enter/Esc-close while a modal is running a long task.

## Live external-process output

Run a cancellable process and stream its output into a modal without leaving the
TUI: a `ScrollablePanel` + `MarkupControl`, lines guarded by a `lock` and
republished as an immutable snapshot via `system.EnqueueOnUIThread(() =>
markup.SetContent(snapshot))`. Tick the window title with elapsed seconds from a
`Timer`, also through `EnqueueOnUIThread`. Escape untrusted output before it
enters markup (below). This is the canonical pattern for any long/cancellable
external work.

## Semantic color layer (no hardcoded color in screens)

Route every color through one place, never hardcode hex in screen code:

1. A `Palette` of hex constants.
2. A theme derived once: `Theme.From(new ModernGrayTheme()).WithName("app").With(t => { /* set ~30 ITheme slots from Palette */ }).Build()`, passed to the `ConsoleWindowSystem(driver, theme, options)` constructor. (`Theme.FromPalette(...)` is the shortcut when you don't need slot-level control.)
3. A `Styles` helper module of semantic markup wrappers (`Accent`, `Title`, `Muted`, `Danger`, `Key`, `Hints`, `Section`, `Rule`) that every screen uses instead of raw `[color]` tags. Wrap primary content explicitly — unstyled markup falls back to control defaults.

Apply control-level theming with a semantic `Role` (`ColorRole`: Primary,
Success, Danger, …) so a control's colors come from the theme.

## Escape untrusted text, always

Any string the app didn't author — process stdout, model names, user input —
must be escaped before entering a markup control, or stray `[...]` is parsed as
tags and can throw at render time:

```csharp
static string EscapeMarkup(string t) => t.Replace("[", "[[").Replace("]", "]]");
// or: MarkupParser.Escape(t)
static int VisibleWidth(string markup) => /* strip [tags] */ ...;  // for alignment math
```

Add self-authored markup verbatim; escape everything else.

## Threading discipline (never block the UI thread)

`system.Run()` is a single cooperative UI loop. Never call `.Result` / `.Wait()`
/ `Thread.Sleep` / sync I/O in a handler — it freezes rendering and input and
trips the watchdog. `await` real async work; marshal any resulting UI mutation
with `system.EnqueueOnUIThread(...)` (fire-and-forget) or `system.InvokeAsync(...)`
(marshal + await). Turn on `InstallSynchronizationContext: true` so `await`
continuations in handlers resume on the UI thread automatically. See
`references/architecture.md`.

## Never crash the TUI

Wrap every fire-and-forget action (navigation, hotkey handler, menu pick) so a
thrown exception can't tear down the app: treat `OperationCanceledException` as a
quiet cancel; for anything else, log it, show a toast
(`NotificationSeverity.Danger`), and return to a safe screen (`PopToRoot()`).

## Checklist for a polished app

- One maximized window; screens swap content; you own the header/breadcrumb/hints.
- Every screen: a hint bar matching its keys, an actionable empty state, `Esc` goes back.
- All color via a `Styles`/`Palette`/theme layer — no hex in screens.
- Every dialog clamped to `DesktopDimensions`; modals closed with `CloseModalWindow`.
- Untrusted text escaped; UI mutations from background work marshalled.
- Every async action wrapped so it can never crash the loop.
