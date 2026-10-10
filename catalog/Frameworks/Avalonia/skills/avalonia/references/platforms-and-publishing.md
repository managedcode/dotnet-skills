# Lifetimes, UI dispatch, and deployment

## Desktop composition

Keep `BuildAvaloniaApp` callable by the designer. Create windows only after initialization. Desktop uses `IClassicDesktopStyleApplicationLifetime`; browser/iOS and other single-view targets require a different composition branch. Avalonia 12 Android introduces `IActivityApplicationLifetime`, so do not reuse the 11 startup code blindly.

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow { DataContext = new MainViewModel() };
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    base.OnFrameworkInitializationCompleted();
}
```

Use the corresponding `Avalonia.Controls` and `Avalonia.Controls.ApplicationLifetimes` namespaces. Choose `OnLastWindowClose`, `OnMainWindowClose`, or `OnExplicitShutdown` from the application's real lifetime. A dialog needs an owner and an awaited result: `await dialog.ShowDialog<Result>(owner)`; return it through `Close(result)`. Keep window creation in the UI service/composition layer.

## Background operations

Await I/O without blocking the dispatcher. If execution is explicitly on a worker thread, marshal all observable UI state updates:

```csharp
var results = await service.LoadAsync(cancellationToken).ConfigureAwait(false);
await Dispatcher.UIThread.InvokeAsync(() =>
{
    Items.Clear();
    foreach (var result in results)
        Items.Add(result);
});
```

Use `Avalonia.Threading`. Bound operation lifetimes to the owning view/model and cancel when it closes. `Post` queues work without waiting; `InvokeAsync` lets the caller observe completion. In Avalonia 12, multi-dispatcher library code can use an object's `Dispatcher`; verify the installed major before copying those APIs.

## Platform qualification

| Target | Runtime checks | Distribution checks |
| --- | --- | --- |
| Windows | DPI changes, keyboard/focus, dialogs, clipboard, window decorations | Chosen x64/arm64 RID, installer and signing, native libraries |
| macOS | Intel/Apple Silicon, menu integration, dialogs, accessibility | `.app` bundle, signing/notarization, architecture-specific native assets |
| Linux | Supported display server, fonts, case-sensitive assets, native dependencies | Supported distro/native libraries, chosen package/container/display model |

Do not assume headless success proves compositor, native rendering, accessibility, or signing. Run a real target-platform launch and keep evidence attached to the promised support matrix.

## Publish, trimming, and Native AOT

First establish an ordinary RID-specific Release publish:

```bash
dotnet publish MyApp.csproj -c Release -r <runtime-id> --self-contained true
```

Then, when required, enable AOT on the executable and compatibility analysis on owned libraries:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <IsAotCompatible>true</IsAotCompatible>
  <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
</PropertyGroup>
```

Avalonia's current guidance requires `BuiltInComInteropSupport=false` before 12; scope that setting to the installed major. Use compiled XAML/bindings, explicit view mappings, AOT-compatible controls and serializer/DI patterns. Reflection locators and dynamic XAML/member discovery need a deliberate preservation or replacement strategy. Do not root every assembly or suppress all trim warnings as a blanket fix.

Publish each supported OS/RID with the required native toolchain, fix actionable trim/AOT diagnostics, and launch the produced executable with all native assets. Packaging and signing are separate steps from `dotnet publish`.

## Tests and troubleshooting

Use the headless framework's Avalonia-aware test attributes (`AvaloniaFact` or the NUnit equivalent) for dispatcher-dependent controls; plain unit tests cover independent view models/services. Check compiled binding errors, command cancellation/errors, property notifications, theme/resource lookup, dialog owner/result, and shutdown. Keep unrelated tests parallel and use the smallest concrete collision domain for destructive shared state.

Read the [official documentation map](official-docs-index.md) for version-specific lifetime, headless, deployment, and migration pages.
