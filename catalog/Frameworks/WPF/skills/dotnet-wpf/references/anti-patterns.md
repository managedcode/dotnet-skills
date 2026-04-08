# WPF Anti-Patterns Reference

## MVVM Violations

### Logic in Code-Behind

**Problem**: Business logic in XAML code-behind files makes testing difficult and creates tight coupling.

```csharp
// WRONG: Logic in code-behind
public partial class CustomerWindow : Window
{
    private readonly CustomerService _service = new();

    public CustomerWindow()
    {
        InitializeComponent();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Business logic should not be here
        if (string.IsNullOrEmpty(NameTextBox.Text))
        {
            MessageBox.Show("Name is required");
            return;
        }

        var customer = new Customer { Name = NameTextBox.Text };
        await _service.SaveAsync(customer);
        CustomerList.Items.Add(customer);
    }
}
```

**Solution**: Move logic to ViewModel with commands.

```csharp
// CORRECT: Logic in ViewModel
public partial class CustomerViewModel : ObservableObject
{
    private readonly ICustomerService _service;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Customer> _customers = [];

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var customer = new Customer { Name = Name };
        await _service.SaveAsync(customer);
        Customers.Add(customer);
        Name = string.Empty;
    }

    private bool CanSave() => !string.IsNullOrEmpty(Name);
}
```

### God ViewModel

**Problem**: Single ViewModel handling too many responsibilities becomes unmaintainable.

```csharp
// WRONG: ViewModel doing everything
public partial class MainViewModel : ObservableObject
{
    // Customer management
    [ObservableProperty] private ObservableCollection<Customer> _customers;
    [ObservableProperty] private Customer? _selectedCustomer;

    // Order management
    [ObservableProperty] private ObservableCollection<Order> _orders;
    [ObservableProperty] private Order? _selectedOrder;

    // Product management
    [ObservableProperty] private ObservableCollection<Product> _products;
    [ObservableProperty] private Product? _selectedProduct;

    // Settings
    [ObservableProperty] private AppSettings _settings;

    // ... hundreds more properties and commands
}
```

**Solution**: Split into focused ViewModels with composition.

```csharp
// CORRECT: Focused ViewModels
public partial class ShellViewModel : ObservableObject
{
    public CustomersViewModel Customers { get; }
    public OrdersViewModel Orders { get; }
    public ProductsViewModel Products { get; }
    public SettingsViewModel Settings { get; }

    public ShellViewModel(
        CustomersViewModel customers,
        OrdersViewModel orders,
        ProductsViewModel products,
        SettingsViewModel settings)
    {
        Customers = customers;
        Orders = orders;
        Products = products;
        Settings = settings;
    }
}
```

### ViewModel with View Dependencies

**Problem**: ViewModel directly references UI elements, breaking testability.

```csharp
// WRONG: ViewModel knows about View
public class BadViewModel
{
    private readonly Window _window;
    private readonly TextBox _nameTextBox;

    public BadViewModel(Window window, TextBox nameTextBox)
    {
        _window = window;
        _nameTextBox = nameTextBox;
    }

    public void Save()
    {
        var name = _nameTextBox.Text; // Direct UI access
        _window.Close(); // Controlling window from ViewModel
    }
}
```

**Solution**: Use services and messaging for UI interactions.

```csharp
// CORRECT: ViewModel uses abstractions
public partial class GoodViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _name = string.Empty;

    public GoodViewModel(IDialogService dialogService, INavigationService navigationService)
    {
        _dialogService = dialogService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _service.SaveAsync(Name);
        _navigationService.GoBack();
    }
}
```

## Data Binding Mistakes

### Missing INotifyPropertyChanged

**Problem**: Properties that don't notify changes won't update the UI.

```csharp
// WRONG: No change notification
public class Person
{
    public string Name { get; set; } = string.Empty; // UI won't update
}
```

**Solution**: Use ObservableObject or implement INotifyPropertyChanged.

```csharp
// CORRECT: With MVVM Toolkit
public partial class Person : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
}
```

### Replacing Observable Collections Incorrectly

**Problem**: Replacing items in ObservableCollection doesn't notify properly in some scenarios.

```csharp
// WRONG: May not trigger proper updates
Items[0] = newItem;

// WRONG: Clearing and re-adding loses selection state
Items.Clear();
foreach (var item in newItems)
{
    Items.Add(item);
}
```

**Solution**: Use appropriate collection manipulation.

```csharp
// CORRECT: Replace entire collection when doing bulk updates
Items = new ObservableCollection<Item>(newItems);

// CORRECT: For in-place updates, use RemoveAt/Insert
var index = Items.IndexOf(oldItem);
Items.RemoveAt(index);
Items.Insert(index, newItem);
```

### Wrong Binding Mode

**Problem**: Using incorrect binding mode leads to unexpected behavior.

```xml
<!-- WRONG: TextBox with OneWay binding won't update source -->
<TextBox Text="{Binding Name, Mode=OneWay}"/>

<!-- WRONG: Label with TwoWay binding is wasteful -->
<Label Content="{Binding Status, Mode=TwoWay}"/>
```

**Solution**: Use appropriate binding modes.

```xml
<!-- CORRECT: TextBox with TwoWay for editing -->
<TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

<!-- CORRECT: Label with OneWay for display -->
<Label Content="{Binding Status, Mode=OneWay}"/>

<!-- CORRECT: OneTime for static content -->
<TextBlock Text="{Binding CreatedDate, Mode=OneTime}"/>
```

### Binding Errors Ignored

**Problem**: Silent binding failures in output window go unnoticed.

```xml
<!-- WRONG: Typo in binding path fails silently -->
<TextBlock Text="{Binding Nmae}"/>  <!-- Should be "Name" -->
```

**Solution**: Enable binding diagnostics and validate at design time.

```xml
<!-- Enable detailed binding errors in App.xaml -->
<Application xmlns:diag="clr-namespace:System.Diagnostics;assembly=WindowsBase">
    <!-- In resources or startup code -->
</Application>
```

```csharp
// In App.xaml.cs
#if DEBUG
PresentationTraceSources.DataBindingSource.Listeners.Add(new ConsoleTraceListener());
PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
#endif
```

## Threading Mistakes

### Blocking UI Thread

**Problem**: Synchronous operations freeze the UI.

```csharp
// WRONG: Blocks UI thread
private void LoadButton_Click(object sender, RoutedEventArgs e)
{
    var data = _service.LoadData(); // Synchronous call
    DataGrid.ItemsSource = data;
}
```

**Solution**: Use async/await.

```csharp
// CORRECT: Async operation
[RelayCommand]
private async Task LoadAsync()
{
    var data = await _service.LoadDataAsync();
    Items = new ObservableCollection<Item>(data);
}
```

### Modifying UI from Background Thread

**Problem**: Updating UI elements from non-UI thread causes exceptions.

```csharp
// WRONG: Cross-thread violation
await Task.Run(() =>
{
    var data = LoadHeavyData();
    Items.Add(data); // Crash: collection modified from wrong thread
});
```

**Solution**: Marshal UI updates to dispatcher.

```csharp
// CORRECT: Use Dispatcher or proper async pattern
await Task.Run(async () =>
{
    var data = LoadHeavyData();

    await Application.Current.Dispatcher.InvokeAsync(() =>
    {
        Items.Add(data);
    });
});

// BETTER: Let async/await handle marshaling
var data = await Task.Run(() => LoadHeavyData());
Items.Add(data); // Back on UI thread automatically
```

### Nested Dispatcher.Invoke Calls

**Problem**: Excessive Dispatcher.Invoke calls hurt performance.

```csharp
// WRONG: Multiple dispatcher calls
foreach (var item in items)
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        Items.Add(item); // Called for each item
    });
}
```

**Solution**: Batch UI updates.

```csharp
// CORRECT: Single dispatcher call
Application.Current.Dispatcher.Invoke(() =>
{
    foreach (var item in items)
    {
        Items.Add(item);
    }
});

// BEST: Replace entire collection
var newItems = await Task.Run(() => ProcessItems(items));
Items = new ObservableCollection<Item>(newItems);
```

## Memory Leaks

### Event Handler Memory Leaks

**Problem**: Not unsubscribing from events prevents garbage collection.

```csharp
// WRONG: Memory leak
public class ChildViewModel
{
    public ChildViewModel(ParentService service)
    {
        service.DataChanged += OnDataChanged; // Never unsubscribed
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        // Handle event
    }
}
```

**Solution**: Use weak events or implement IDisposable.

```csharp
// CORRECT: Weak event pattern
public class ChildViewModel
{
    public ChildViewModel(ParentService service)
    {
        WeakEventManager<ParentService, EventArgs>.AddHandler(
            service,
            nameof(ParentService.DataChanged),
            OnDataChanged);
    }
}

// CORRECT: IDisposable pattern
public class ChildViewModel : IDisposable
{
    private readonly ParentService _service;

    public ChildViewModel(ParentService service)
    {
        _service = service;
        _service.DataChanged += OnDataChanged;
    }

    public void Dispose()
    {
        _service.DataChanged -= OnDataChanged;
    }
}
```

### Binding Memory Leaks

**Problem**: Binding to non-INotifyPropertyChanged objects can cause leaks.

```csharp
// WRONG: Binding to plain object
public class Person
{
    public string Name { get; set; } // No INPC
}

// View binds to this, WPF creates strong reference
```

**Solution**: Always implement INotifyPropertyChanged for bound objects.

```csharp
// CORRECT: Observable object
public partial class Person : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;
}
```

### Static Event Handlers

**Problem**: Static events hold references to subscribers forever.

```csharp
// WRONG: Static event
public static class EventAggregator
{
    public static event EventHandler? SomethingHappened;
}

// Subscriber never gets collected
EventAggregator.SomethingHappened += OnSomethingHappened;
```

**Solution**: Use weak reference messaging.

```csharp
// CORRECT: Weak reference messenger
WeakReferenceMessenger.Default.Register<SomeMessage>(this, (r, m) =>
{
    // Handle message
});

// Unregister when done
WeakReferenceMessenger.Default.Unregister<SomeMessage>(this);
```

## Style and Template Mistakes

### Hardcoded Values

**Problem**: Hardcoded colors, sizes, and fonts prevent theming.

```xml
<!-- WRONG: Hardcoded values -->
<Button Background="#0078D4"
        FontSize="14"
        Padding="10,5"
        Foreground="White"/>
```

**Solution**: Use resource dictionaries.

```xml
<!-- CORRECT: Resources -->
<Button Style="{StaticResource PrimaryButton}"/>

<!-- In ResourceDictionary -->
<SolidColorBrush x:Key="PrimaryBrush" Color="#0078D4"/>
<sys:Double x:Key="StandardFontSize">14</sys:Double>

<Style x:Key="PrimaryButton" TargetType="Button">
    <Setter Property="Background" Value="{StaticResource PrimaryBrush}"/>
    <Setter Property="FontSize" Value="{StaticResource StandardFontSize}"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="Padding" Value="10,5"/>
</Style>
```

### Inline Styles Everywhere

**Problem**: Repeated inline styles are hard to maintain.

```xml
<!-- WRONG: Repeated inline styling -->
<StackPanel>
    <TextBlock FontSize="24" FontWeight="Bold" Margin="0,0,0,10"/>
    <TextBlock FontSize="24" FontWeight="Bold" Margin="0,0,0,10"/>
    <TextBlock FontSize="24" FontWeight="Bold" Margin="0,0,0,10"/>
</StackPanel>
```

**Solution**: Define reusable styles.

```xml
<!-- CORRECT: Reusable style -->
<StackPanel>
    <StackPanel.Resources>
        <Style x:Key="HeaderText" TargetType="TextBlock">
            <Setter Property="FontSize" Value="24"/>
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="Margin" Value="0,0,0,10"/>
        </Style>
    </StackPanel.Resources>

    <TextBlock Style="{StaticResource HeaderText}"/>
    <TextBlock Style="{StaticResource HeaderText}"/>
    <TextBlock Style="{StaticResource HeaderText}"/>
</StackPanel>
```

### Forgetting BasedOn for Derived Styles

**Problem**: Derived styles lose base styling.

```xml
<!-- WRONG: Loses default Button styling -->
<Style x:Key="RedButton" TargetType="Button">
    <Setter Property="Background" Value="Red"/>
</Style>
```

**Solution**: Use BasedOn.

```xml
<!-- CORRECT: Inherits base style -->
<Style x:Key="RedButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Background" Value="Red"/>
</Style>
```

## Performance Anti-Patterns

### Not Virtualizing Large Lists

**Problem**: Rendering thousands of items without virtualization.

```xml
<!-- WRONG: No virtualization -->
<ItemsControl ItemsSource="{Binding ThousandsOfItems}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <StackPanel/> <!-- StackPanel doesn't virtualize -->
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```

**Solution**: Enable virtualization.

```xml
<!-- CORRECT: Virtualized list -->
<ListBox ItemsSource="{Binding ThousandsOfItems}"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling"
         VirtualizingPanel.ScrollUnit="Pixel"/>
```

### Creating Brushes in Property Getters

**Problem**: Creating new objects in property getters causes allocations every access.

```csharp
// WRONG: New brush created every access
public Brush StatusBrush => IsActive
    ? new SolidColorBrush(Colors.Green)
    : new SolidColorBrush(Colors.Red);
```

**Solution**: Cache brushes or use static resources.

```csharp
// CORRECT: Static cached brushes
private static readonly Brush ActiveBrush = CreateFrozenBrush(Colors.Green);
private static readonly Brush InactiveBrush = CreateFrozenBrush(Colors.Red);

private static SolidColorBrush CreateFrozenBrush(Color color)
{
    var brush = new SolidColorBrush(color);
    brush.Freeze();
    return brush;
}

public Brush StatusBrush => IsActive ? ActiveBrush : InactiveBrush;
```

### Complex Value Converters

**Problem**: Heavy computation in converters runs on every binding update.

```csharp
// WRONG: Expensive converter
public class ExpensiveConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Heavy computation that runs frequently
        return ComputeHeavyTransformation(value);
    }
}
```

**Solution**: Move computation to ViewModel or cache results.

```csharp
// CORRECT: Computation in ViewModel with caching
public partial class ItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _rawData = string.Empty;

    [ObservableProperty]
    private string _processedData = string.Empty;

    partial void OnRawDataChanged(string value)
    {
        ProcessedData = ComputeTransformation(value); // Computed once per change
    }
}
```

## Command Mistakes

### Async Void Commands

**Problem**: Async void swallows exceptions and can't be awaited.

```csharp
// WRONG: async void
private async void SaveCommand_Execute()
{
    await _service.SaveAsync(); // Exception here is lost
}
```

**Solution**: Use proper async command patterns.

```csharp
// CORRECT: MVVM Toolkit async command
[RelayCommand]
private async Task SaveAsync()
{
    await _service.SaveAsync();
}
```

### Not Calling CanExecuteChanged

**Problem**: Command button stays enabled/disabled incorrectly.

```csharp
// WRONG: Manual command without CanExecute updates
public class ManualCommand : ICommand
{
    private bool _canExecute = true;

    public bool CanExecute(object? parameter) => _canExecute;

    public void SetCanExecute(bool value)
    {
        _canExecute = value;
        // Forgot to call CanExecuteChanged!
    }
}
```

**Solution**: Use MVVM Toolkit attributes or raise CanExecuteChanged.

```csharp
// CORRECT: Automatic CanExecute notification
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
private bool _hasChanges;

[RelayCommand(CanExecute = nameof(CanSave))]
private Task SaveAsync() => _service.SaveAsync();

private bool CanSave() => HasChanges;
```

## Validation Mistakes

### Validation Only in UI

**Problem**: Validation rules only in XAML, not enforced in ViewModel.

```xml
<!-- WRONG: Only UI validation -->
<TextBox>
    <TextBox.Text>
        <Binding Path="Email">
            <Binding.ValidationRules>
                <local:EmailValidationRule/>
            </Binding.ValidationRules>
        </Binding>
    </TextBox.Text>
</TextBox>
```

**Solution**: Implement INotifyDataErrorInfo in ViewModel.

```csharp
// CORRECT: ViewModel validation
public partial class UserViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    private string _email = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors) return;

        await _service.SaveAsync();
    }
}
```

### Ignoring Validation Errors

**Problem**: Not checking HasErrors before saving.

```csharp
// WRONG: Save without validation check
[RelayCommand]
private async Task SaveAsync()
{
    await _service.SaveAsync(CurrentItem); // May save invalid data
}
```

**Solution**: Always validate before save.

```csharp
// CORRECT: Validate first
[RelayCommand(CanExecute = nameof(CanSave))]
private async Task SaveAsync()
{
    ValidateAllProperties();
    if (HasErrors) return;

    await _service.SaveAsync(CurrentItem);
}

private bool CanSave() => !HasErrors && CurrentItem is not null;
```
