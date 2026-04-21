---
name: winui
description: "Build or review WinUI 3 applications with the Windows App SDK, including MVVM patterns, packaging decisions, navigation, theming, windowing, and interop boundaries with other .NET stacks. Use when building modern Windows-native desktop UI."
compatibility: "Requires a WinUI 3, Windows App SDK, or MAUI-on-Windows integration scenario."
---

# WinUI 3 and Windows App SDK

## Trigger On

- building native modern Windows desktop UI on WinUI 3
- integrating Windows App SDK features into a .NET app
- deciding between WinUI, WPF, WinForms, and MAUI for Windows work
- implementing MVVM patterns in Windows App SDK applications

## Workflow

1. **Confirm WinUI is the right choice** — use when modern Windows-native UI, Fluent Design, and Windows App SDK capabilities are needed. For cross-platform, consider MAUI instead.
2. **Choose packaging model early** — packaged (MSIX) vs unpackaged differ materially for deployment, identity, and API access:
   ```xml
   <!-- Unpackaged: add to .csproj -->
   <WindowsPackageType>None</WindowsPackageType>
   ```
3. **Apply MVVM pattern** with the MVVM Toolkit — keep views dumb, logic in ViewModels:
   ```csharp
   public partial class ProductsViewModel : ObservableObject
   {
       [ObservableProperty]
       private ObservableCollection<Product> _products = [];

       [ObservableProperty]
       [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
       private Product? _selectedProduct;

       [RelayCommand(CanExecute = nameof(CanDelete))]
       private async Task DeleteAsync()
       {
           if (SelectedProduct is null) return;
           await _productService.DeleteAsync(SelectedProduct.Id);
           Products.Remove(SelectedProduct);
       }
       private bool CanDelete() => SelectedProduct is not null;
   }
   ```
4. **Use x:Bind for compiled bindings** — better performance and compile-time checking than `{Binding}`:
   ```xml
   <TextBlock Text="{x:Bind ViewModel.Title, Mode=OneWay}"/>
   ```
5. **Wire DI through `Host.CreateDefaultBuilder`** — register services, ViewModels, and views. Resolve via `App.GetService<T>()`.
6. **Implement navigation service** — map ViewModels to Pages by convention. See references/patterns.md for the full pattern.
7. **Handle Windows App SDK features** — windowing (AppWindow), custom title bar, app lifecycle, notifications.
8. **Always set `XamlRoot`** when showing ContentDialog — omitting this causes silent failures.
9. **Validate on Windows targets** — behavior depends on runtime, packaging model, and Windows version.

```mermaid
flowchart LR
  A["Choose WinUI"] --> B["Select packaging model"]
  B --> C["MVVM + DI setup"]
  C --> D["Navigation and views"]
  D --> E["Windows App SDK features"]
  E --> F["Validate on target runtime"]
```

## Key Decisions

| Decision | Guidance |
|----------|----------|
| Packaged vs unpackaged | Packaged (MSIX) for Store, auto-update, and full API access; unpackaged for simpler deployment |
| x:Bind vs Binding | Always prefer x:Bind — compiled, faster, type-safe |
| MVVM Toolkit attributes | Use `[ObservableProperty]`, `[RelayCommand]` to eliminate boilerplate |
| Navigation | Convention-based ViewModel→Page mapping via navigation service |
| Theming | Use `RequestedTheme` on root element; respect system theme by default |

## Deliver

- modern Windows UI code with clear platform boundaries
- explicit deployment and packaging assumptions
- MVVM pattern with testable ViewModels
- cleaner interop between shared and Windows-specific layers

## Validate

- WinUI is chosen for a real product reason, not defaulted to
- Windows App SDK dependencies are explicit in the project file
- packaging and runtime assumptions are tested on target
- x:Bind is used for compiled bindings throughout
- navigation and ContentDialog both work with correct XamlRoot
- custom title bar renders correctly on Windows 10 and 11

## References

- references/patterns.md - WinUI 3 patterns including MVVM, navigation services, DI setup, windowing, theming, dialogs, and lifecycle handling
- references/anti-patterns.md - common WinUI mistakes with explanations and corrections
