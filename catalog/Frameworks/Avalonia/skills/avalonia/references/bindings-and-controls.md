# Bindings, properties, templates, and resources

## Compiled bindings and commands

In Avalonia 11, explicitly enable compiled bindings through the project property or `x:CompileBindings="True"`; Avalonia 12 enables them by default. Supply `x:DataType` on views and a concrete data type on each template. A compile-time type does not create the runtime object: assign `DataContext` from the composition root or parent view.

Use `Mode=TwoWay` for editable values and notifications through `ObservableObject`/`INotifyPropertyChanged`. Keep toolkit-generated members in partial classes. `RelayCommand` exposes `GreetCommand` from `Greet`; asynchronous `Task` methods generate async commands and can accept cancellation. Review concurrency, can-execute notifications, validation, and exception handling for each command rather than treating async void as an equivalent.

Build-time binding errors usually mean the declared type, namespace, accessibility, or generated member is wrong. Runtime missing values usually mean the actual context, notification, conversion, or binding mode is wrong. Reflection bindings need runtime diagnostics and can lose members under trimming.

## Property choice

| Property | Use | Constraints |
| --- | --- | --- |
| `StyledProperty<T>` | Styleable, animatable, inherited or precedence-sensitive value | CLR accessor only delegates to `GetValue`/`SetValue`; use validation/coercion or property-change hooks for behavior |
| `DirectProperty<TOwner,T>` | Backed field, computed/read-only state, collection ownership | Does not participate in style value precedence; raise notifications with `SetAndRaise` |
| Attached property | Shared behavior configured on another control | Register and expose static accessor methods; keep lifetime ownership explicit |

```csharp
using Avalonia;
using Avalonia.Controls;

public class ResultView : Control
{
    public static readonly DirectProperty<ResultView, int> CountProperty =
        AvaloniaProperty.RegisterDirect<ResultView, int>(nameof(Count), view => view.Count);

    private int count;

    public int Count
    {
        get => count;
        private set => SetAndRaise(CountProperty, ref count, value);
    }

    public void SetResultCount(int value) => Count = value;
}
```

Keep the field named `<Name>Property`; XAML resolution relies on this convention. Use `AddOwner` when reusing an existing property's identity. A control's internal update can use `SetCurrentValue` to preserve its consumer's binding.

## Templates and reusable controls

Use `DataTemplate` for a data object's presentation and `ControlTemplate` for the visual structure of a control. Avoid a reflection-based view locator when an explicit typed mapping is enough.

```xml
<DataTemplate xmlns="https://github.com/avaloniaui"
              xmlns:vm="using:MyApp.ViewModels"
              DataType="vm:ResultViewModel">
  <TextBlock Text="{Binding Title}" />
</DataTemplate>
```

For the `StatusBadge` from the skill, place the theme in an included resource dictionary, declare its namespace, and use template bindings:

```xml
<ControlTheme xmlns="https://github.com/avaloniaui"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              xmlns:local="using:MyApp.Controls"
              x:Key="{x:Type local:StatusBadge}" TargetType="local:StatusBadge">
  <Setter Property="Template">
    <ControlTemplate>
      <Border Padding="8"><TextBlock Text="{TemplateBinding Text}" /></Border>
    </ControlTemplate>
  </Setter>
</ControlTheme>
```

Use `OnApplyTemplate` and named parts when implementing interactive templated controls. Detach old subscriptions before reapplying a template. Custom rendering must request redraws for relevant property changes and preserve accessibility/input expectations.

## Resources and themes

Load a supported theme in `Application.Styles`, use `RequestedThemeVariant` for light/dark/system intent, and scope selectors through classes or pseudoclasses. `StaticResource` resolves a stable resource; `DynamicResource` updates when resources or theme variants change. Package images/fonts with `AvaloniaResource` and address them through `avares://Assembly/Assets/file.ext`. Test case sensitivity on Linux and resource assembly names after publishing.

Prefer official [compiled bindings](https://docs.avaloniaui.net/docs/data-binding/compiled-bindings), [property definitions](https://docs.avaloniaui.net/docs/custom-controls/defining-properties), [control themes](https://docs.avaloniaui.net/docs/styling/control-themes), and [resources](https://docs.avaloniaui.net/docs/app-development/resources) when a concrete API detail matters.
