---
name: winforms
description: "Build, maintain, or modernize Windows Forms applications with practical guidance on designer-driven UI, event handling, data binding, MVP separation, and migration to modern .NET. Use when working on WinForms projects or migrating from .NET Framework."
compatibility: "Requires a Windows Forms project on .NET or .NET Framework."
---

# Windows Forms

## Trigger On

- working on Windows Forms UI, event-driven workflows, or classic LOB applications
- migrating WinForms from .NET Framework to modern .NET
- cleaning up oversized form code or designer coupling
- implementing data binding, validation, or control customization

## Workflow

1. **Respect designer boundaries** — never edit `.Designer.cs` directly; changes are lost on regeneration.
2. **Separate business logic from forms** — use MVP (Model-View-Presenter) pattern. Forms orchestrate UI; presenters contain logic; services handle data access.
   ```csharp
   // View interface — forms implement this
   public interface ICustomerView
   {
       string CustomerName { get; set; }
       event EventHandler SaveRequested;
       void ShowError(string message);
   }

   // Presenter — testable without UI
   public class CustomerPresenter
   {
       private readonly ICustomerView _view;
       private readonly ICustomerService _service;
       public CustomerPresenter(ICustomerView view, ICustomerService service)
       {
           _view = view;
           _service = service;
           _view.SaveRequested += async (s, e) =>
           {
               try { await _service.SaveAsync(_view.CustomerName); }
               catch (Exception ex) { _view.ShowError(ex.Message); }
           };
       }
   }
   ```
3. **Use DI from Program.cs** (.NET 6+):
   ```csharp
   var services = new ServiceCollection();
   services.AddSingleton<ICustomerService, CustomerService>();
   services.AddTransient<MainForm>();
   using var sp = services.BuildServiceProvider();
   Application.Run(sp.GetRequiredService<MainForm>());
   ```
4. **Use data binding** via `BindingSource` and `INotifyPropertyChanged` instead of manual control population. See references/patterns.md for complete binding patterns.
5. **Use async/await** for I/O operations — disable controls during loading, use `Progress<T>` for progress reporting. Never block the UI thread.
6. **Validate with `ErrorProvider`** and the `Validating` event. Call `ValidateChildren()` before save operations.
7. **Modernize incrementally** — prefer better structure over big-bang rewrites. Use .NET 8+ features (button commands, stock icons) when available.

```mermaid
flowchart LR
  A["Form event"] --> B["Presenter handles logic"]
  B --> C["Service layer / data access"]
  C --> D["Update view via interface"]
  D --> E["Validate and display results"]
```

## Key Decisions

| Decision | Guidance |
|----------|----------|
| MVP vs MVVM | Prefer MVP for WinForms — simpler with event-driven model |
| BindingSource vs manual | Always prefer BindingSource for list/detail binding |
| Sync vs async I/O | Always async — use `async void` only for event handlers |
| Custom controls | Extract reusable `UserControl` when form grows beyond ~300 lines |
| .NET Framework → .NET | Use the official migration guide; validate designer compatibility first |

## Deliver

- less brittle form code with clear UI/logic separation
- MVP pattern with testable presenters
- pragmatic modernization guidance for WinForms-heavy apps
- data binding and validation patterns that reduce manual wiring

## Validate

- designer files stay stable and are not hand-edited
- forms are not acting as the application service layer
- async operations do not block the UI thread
- validation is implemented consistently with ErrorProvider
- Windows-only runtime behavior is tested on target

## References

- references/patterns.md - WinForms architectural patterns (MVP, MVVM, Passive View), data binding, validation, form communication, threading, DI setup, and .NET 8+ features
- references/migration.md - step-by-step migration from .NET Framework to modern .NET, common issues, deployment options, and gradual migration strategies
