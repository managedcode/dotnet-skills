# SharpConsoleUI system, services, and patterns

Application-level services, configuration, data binding, flows, distribution, and
when to choose this framework. Exact API names are as they appear in the docs.

## State services (on `ConsoleWindowSystem`)

Reached as properties on the `ConsoleWindowSystem` instance (except `FocusManager`,
which is per-`Window`):

- `PanelStateService` — `TopPanel`/`BottomPanel`, `ShowTopPanel`/`ShowBottomPanel`, `TopStatus`/`BottomStatus` (set-only; updates first `StatusTextElement`), `MarkDirty()`. Shorthand: `windowSystem.BottomPanel`.
- `WindowStateService` — `Windows`, `ActiveWindow`, `IsDragging`/`IsResizing`; `RegisterWindow`, `BringToFront`, `GetWindowsInZOrder()`, `StartDrag/EndDrag`, …
- `window.FocusManager` (per-window; replaced the old system-wide focus service) — `FocusedControl`, `FocusPath`, `SetFocus(control, FocusReason)`, `MoveFocus(bool backward)`, `HandleClick(hit)`, `FocusChanged` event.
- `ModalStateService` — `HasModals`, `TopModal`, `ModalCount`; `PushModal`/`RemoveModal`, `IsModal`, `IsBlockedByModal`. `WindowBuilder.AsModal()` calls `PushModal` internally.
- `NotificationStateService` — `ShowNotification(title, message, severity, blockUi = false, timeout = 5000)`. Separate corner-toast system is `windowSystem.ToastService`.
- `ThemeStateService` — `CurrentTheme`, `SetTheme(ITheme)`, `SwitchTheme(string name)`, `ThemeChanged` event.
- `CursorStateService`, `InputStateService` — cursor visibility/position; key/mouse-button state queries.
- `PluginStateService`, `RegistryStateService` — see below (registry is null unless configured).

```csharp
windowSystem.PanelStateService.TopStatus = "[bold cyan]Connected[/]";
windowSystem.NotificationStateService.ShowNotification("Saved", "Doc saved", NotificationSeverity.Success);
```

## Panels (top/bottom bars)

Composable screen-level bars built from **elements** via `PanelBuilder`, configured
through `ConsoleWindowSystemOptions.TopPanelConfig`/`BottomPanelConfig`
(`Func<PanelBuilder, PanelBuilder>`). Each panel has `Left`/`Center`/`Right` zones.

- `SharpConsoleUI.Panel.Elements` factories: `StatusText(text)`, `Separator()`, `TaskBar()` (Alt+1–9 window switch), `Clock()`, `Performance()`, `StartMenu()`, `Custom(name)`. Each has fluent config (`.WithColor`, `.WithFormat`, etc.).
- `StartMenuElement.RegisterAction(name, callback, category?, order?)`; a loaded plugin contributes actions via `IPlugin.GetActionProviders()` → `IPluginActionProvider` (`GetAvailableActions()` returning `ActionDescriptor` records, `ExecuteAction(name, context?)`).
- Runtime: `windowSystem.BottomPanel!.FindElement<StartMenuElement>("startmenu")`, `panel.AddLeft/AddCenter/AddRight(...)`, `ClearLeft()`, `MarkDirty()`.
- When no config is supplied, default panels are created with a `StatusTextElement` so `TopStatus`/`BottomStatus` work out of the box. (Replaces the old `StatusBarOptions` model.)

```csharp
var options = new ConsoleWindowSystemOptions(
    TopPanelConfig: p => p.Left(Elements.StatusText("[bold cyan]My App[/]")).Right(Elements.Performance()),
    BottomPanelConfig: p => p.Left(Elements.StartMenu()).Center(Elements.TaskBar()).Right(Elements.Clock()));
var windowSystem = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer), options: options);
```

## Constructing the window system

`ConsoleWindowSystem` has four constructor overloads — pass the theme and/or a
custom driver at construction:

- `new ConsoleWindowSystem(RenderMode, options?, registryConfiguration?)`
- `new ConsoleWindowSystem(IConsoleDriver, options?, registryConfiguration?)`
- `new ConsoleWindowSystem(IConsoleDriver, string themeName, options?, registryConfiguration?)`
- `new ConsoleWindowSystem(IConsoleDriver, ITheme, options?, registryConfiguration?)`

Lifecycle: `system.Run()` blocks the calling thread and returns the exit code.
`system.RequestExit(code)` / `system.Shutdown(code)` end the loop.
`system.RegisterGlobalShortcut(ConsoleModifiers, ConsoleKey, Action | Func<bool>)`
registers app-wide hotkeys (e.g. Ctrl+Q to quit). `system.DesktopDimensions`
gives the usable desktop `Size` (for clamping window sizes).

## Configuration

`ConsoleWindowSystemOptions` (passed as `options:`): `EnablePerformanceMetrics`,
`EnableFrameRateLimiting`, `TargetFPS` (default 60; 0 = unlimited),
`ShowTopPanel`/`ShowBottomPanel`, `TopPanelConfig`/`BottomPanelConfig`,
`DesktopBackground`, `EnableAnimations`, `InstallSynchronizationContext`,
`TerminalTransparencyMode`. Factories: `.Default`, `.Create(...)`, `.WithMetrics`,
`.WithoutFrameRateLimiting`, `.WithTargetFPS(n)`. Env vars:
`SHARPCONSOLEUI_DEBUG_LOG=<path>`, `SHARPCONSOLEUI_DEBUG_LEVEL=...`,
`SHARPCONSOLEUI_PERF_METRICS=true`. Guidance: 30 FPS for dashboards, 60 general,
unlimited for games/animation.

## Registry (persistent settings)

JSON-backed hierarchical key-value store; survives restarts. Enable by passing
`registryConfiguration:` to the `ConsoleWindowSystem` ctor; access via
`windowSystem.RegistryStateService` (null if not configured).

- `RegistryConfiguration(FilePath = "registry.json", EagerFlush = false, FlushInterval = null, Storage = null)`; `.Default` (per-process platform path) / `.ForFile(path)`.
- `RegistryStateService`: `OpenSection(path)` → `RegistrySection`, `Save()`, `Load()` (auto-load on init, auto-save on dispose).
- `RegistrySection`: typed `GetString/SetString`, `GetInt/SetInt`, `GetBool/SetBool`, `GetDouble/SetDouble`, `GetDateTime/SetDateTime`; AOT-safe `Get<T>/Set<T>(key, …, JsonTypeInfo<T>)`; `HasKey`, `GetKeys()`, `DeleteKey`, `DeleteSection`. `Get*` never throw (return default). Nested paths via `OpenSection("app/ui")`.
- Thread safety: the registry root is thread-safe, but a `RegistrySection` instance is NOT — open a fresh section per thread. `MemoryStorage` for tests.

```csharp
var registry = windowSystem.RegistryStateService!;
var ui = registry.OpenSection("app/ui");
string theme = ui.GetString("theme", "ModernGray");
ui.SetString("theme", "Solarized");
```

## Data binding (MVVM)

`.Bind()` / `.BindTwoWay()` wire an `INotifyPropertyChanged` view model to control
properties (on controls, builders, and `MenuItem`).

- One-way: `control.Bind(vm, v => v.Prop, c => c.TargetProp)` (applies initial value immediately); with converter `(…, Func<TSource,TTarget>)`.
- Two-way: `control.BindTwoWay(vm, v => v.Prop, c => c.TargetProp)` (re-entrancy guarded); with `toTarget:`/`toSource:` converters.
- Two-way-capable properties include `CheckboxControl.Checked`, `SliderControl.Value`, `DropdownControl.SelectedIndex`, `ListControl.SelectedIndex`, `TabControl.ActiveTabIndex`, `PromptControl.Input` (note: `Input`, not `Text`), `MultilineEditControl.Content`, `TableControl.SelectedRowIndex`, `CollapsiblePanel.IsExpanded`, `TreeNode.IsExpanded/Text`. Display-only one-way targets: `MarkupControl.Text`, `ProgressBarControl.Value`, `BarGraphControl.Value`.
- Every control derives from `BaseControl` (already `INotifyPropertyChanged`) — only your view model must implement it. Bindings are `IDisposable`, stored in the control's `Bindings`, disposed with the control (no manual unsubscribe). AOT-safe (LINQ interpreter fallback).

```csharp
bar.Bind(vm, v => v.Cpu, c => c.Value);          // one-way
prompt.BindTwoWay(vm, v => v.Name, c => c.Input); // two-way
```

## Flows (multi-step workflows)

`SharpConsoleUI.Flows` removes Back-stack / cancellation / navigation boilerplate.

- `Flow.Run<T>(ws, parent, async ctx => …)` → `FlowResult<T>` (`Completed`, `Value`, `Cancelled`, `Faulted`, `Error`). `FlowContext`: `Token`, `Show<TResult>(content, title, buttons)`, `Confirm(...)`, `Prompt(...)`, `RunWithProgress<TResult>(...)`, `Commit()` (Back barrier). Throw `OperationCanceledException` to cancel.
- `Flow.Wizard<TState>()` (`TState : new()`) → `FlowWizardBuilder<TState>`: `.Seed(state)`, `.WithStepIndicator()`, `.WithTitle(...)`, `.WithSeamlessHost()` (one shared window), `.Step(...)` (code step or content step with `.CanGoNext(...)`/`.OnNext/.OnBack/.OnCancel(...)`), `.Run(ws, parent)`. `FlowVerdict`: `Next`, `Finish`, `Back`, `Cancel`, `Stay`.
- Default host is a modal window per step (`ModalWindowHost`); `SwapContentHost` reuses one window. For **inline** (non-modal) flows, embed `FlowControl` / `WizardControl` in a window region instead.

```csharp
var result = await Flow.Run<string>(ws, myWindow, async ctx =>
{
    if (!await ctx.Confirm("Deploy", "Deploy to staging?", ok: "Deploy")) throw new OperationCanceledException();
    return await ctx.RunWithProgress<string>("Deploying", "Connecting…",
        async (ct, progress) => { progress.Report("Uploading…"); await DeployAsync(ct); return "OK"; });
});
```

## Plugins

Extend with themes/controls/windows/services without touching core. Implement
`IPlugin` / `PluginBase` (`Info`, `Initialize(ws)`, `GetThemes()`, `GetControls()`,
`GetWindows()`, `GetServicePlugins()`, `Dispose()`). Register via
`windowSystem.PluginStateService.LoadPlugin<T>()` / `LoadPlugin(dllPath)` /
`LoadPluginsFromDirectory(...)`; create with `CreateControl(name)` /
`CreateWindow(name)`; call agnostic services with
`GetService(name).Execute(op, parameters)` (reflection-free). Auto-load via
`PluginConfiguration(AutoLoad, PluginsDirectory)` on the ctor.

## Clipboard

`SharpConsoleUI.Helpers.ClipboardHelper.SetText(text)` / `GetText()`. Copy works
transparently over SSH via OSC 52 (falls back to `clip.exe`/`pbcopy`/`wl-copy`/
`xclip`/`xsel`). Knobs: `ClipboardHelper.Osc52Mode` (`Auto`/`Enabled`/`Disabled`),
`MaxOsc52Bytes` (default ~74000). Bracketed paste is enabled at startup; focused
controls implementing `IPasteTarget` (`MultilineEditControl`, `PromptControl`,
`TableControl`) receive `Ctrl+V` and pasted blocks.

## Shell scripting and distribution

- **Pipelines:** a SharpConsoleUI app can be an interactive picker/wizard inside a shell pipeline (like `fzf`/`gum`). On Unix the driver opens `/dev/tty` when stdin/stdout are redirected, keeping the pipe free for data. Read `windowSystem.PipedInput` / `PipedLines` **before** `Run()`, write results **after** it returns. Exit codes follow the `fzf`/`gum` convention (0 selected, 1 cancelled, 2 invalid). .NET 10 file-based apps (`#:package SharpConsoleUI@…` + `dotnet run script.cs`) suit one-off scripts; templates live under the docs `scripting/templates/`.
- **schost:** a separate CLI tool (`SharpConsoleUI.Host` NuGet, `dotnet tool install -g SharpConsoleUI.Host`) that launches an app inside a configured terminal window and packages it for desktop distribution. Commands: `schost init`, `schost run [--inline]`, `schost pack [--installer]`, `schost install`. Config in `schost.json`. `dotnet new schost-app` scaffolds a full-screen NavigationView starter. schost is a launcher/packager, not a renderer — the app still uses the real `NetConsoleDriver`.

## When to choose SharpConsoleUI (vs other .NET TUI libs)

The docs position it as "a desktop" — overlapping windows + a real compositor —
distinct from Spectre.Console ("a printer" for rich static output), Terminal.Gui
("a dialog box", single-screen forms with the widest mature control library), and
XenoAtom.Terminal.UI ("a WPF for the terminal", source-generated reactive
bindings, .NET 10 only). They are complementary — SharpConsoleUI can host
Spectre renderables via `SpectreRenderableControl`.

Prefer SharpConsoleUI for: multi-window desktop-style apps; full-screen apps
(`.Frameless().Maximized()`); visual effects/compositing/transparency; dashboards
and monitoring (independent async window threads); IDE-like tools; an embedded
terminal (`TerminalControl`) alongside UI; terminal video; markup/Markdown
everywhere; MVVM data binding; plugin architectures.

Prefer something else when: you only need pretty CLI output (Spectre.Console);
you want the widest mature single-screen control set (Terminal.Gui); you need
source-generated reactive bindings (XenoAtom); you target .NET 6 or older
(SharpConsoleUI requires .NET 8+); or you need maximum community/ecosystem size.
Documented gaps: proportional sizing only via Grid `Star` tracks (no flex
allocator), vertical-only dock, and no built-in ColorPicker/HexView.

## Pattern catalog

The docs' patterns cookbook (from real apps) covers, among others: app bootstrap
(`NetConsoleDriver(RenderMode.Buffer)` → `ConsoleWindowSystem` → maximized
`WindowBuilder` + `WithAsyncWindowThread` + `OnKeyPressed` → `Run()`); split layout
with a resizable splitter (`HorizontalGrid` + `.WithSplitterAfter`); async data
updates (continuous `WithAsyncWindowThread` loop; fire-and-forget with a
`CancellationTokenSource`; `EnqueueOnUIThread` from external threads — only
`Container?.Invalidate(...)` is safe to call directly off-thread); modal dialog
with result (`ModalBase<TResult>` + `TaskCompletionSource`); global keyboard
shortcuts (`OnKeyPressed` / `PreviewKeyPressed` before controls consume keys);
debounced search; width-based responsive relayout; control discovery by name
(`.WithName(...)` + `window.FindControl<T>("name")`); rolling log viewer
(`MarkupControl.SetContent` + `MarkupParser.Escape`); and explicit handler cleanup
in an `OnCleanup()` override. Key gotcha for `WithAsyncWindowThread`: its lambda
is built during `Build()`, so every captured control must be declared *before* the
`WindowBuilder` call, while `AddControl` wiring happens *after* `Build()`.
