# WinUI 3 Patterns

Reference patterns for building WinUI 3 applications with Windows App SDK.

## MVVM Pattern

### Core Principles

1. **View** - XAML UI, minimal code-behind, binds to ViewModel
2. **ViewModel** - Exposes data and commands, contains presentation logic
3. **Model** - Domain data and business rules

### MVVM Toolkit Integration

Use `CommunityToolkit.Mvvm` for source-generated MVVM:

```csharp
public partial class OrderViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalDisplay))]
    private decimal _total;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _isValid;

    public string TotalDisplay => $"Total: {Total:C}";

    [RelayCommand(CanExecute = nameof(IsValid))]
    private async Task SubmitAsync()
    {
        // Submit order
    }
}
```

### ViewModel Initialization

Initialize ViewModels through navigation or page lifecycle:

```csharp
public sealed partial class OrderPage : Page
{
    public OrderViewModel ViewModel { get; }

    public OrderPage()
    {
        ViewModel = App.GetService<OrderViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is int orderId)
        {
            await ViewModel.LoadAsync(orderId);
        }
    }
}
```

## Service Pattern

### Service Registration

Register services at application startup:

```csharp
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<INavigationService, NavigationService>();
services.AddTransient<IFileService, FileService>();
services.AddHttpClient<IApiService, ApiService>();
```

### Service Abstraction

Abstract Windows-specific APIs behind interfaces for testability:

```csharp
public interface IFilePickerService
{
    Task<StorageFile?> PickFileAsync(IEnumerable<string> extensions);
    Task<StorageFolder?> PickFolderAsync();
}

public class FilePickerService : IFilePickerService
{
    private readonly Window _window;

    public FilePickerService(Window window)
    {
        _window = window;
    }

    public async Task<StorageFile?> PickFileAsync(IEnumerable<string> extensions)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));

        foreach (var ext in extensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        return await picker.PickSingleFileAsync();
    }
}
```

## Navigation Pattern

### Frame-Based Navigation

Use a navigation service that wraps Frame navigation:

```csharp
public class NavigationService : INavigationService
{
    private readonly Dictionary<Type, Type> _viewModelToPageMap = new();
    private Frame? _frame;

    public void RegisterPage<TViewModel, TPage>()
        where TViewModel : class
        where TPage : Page
    {
        _viewModelToPageMap[typeof(TViewModel)] = typeof(TPage);
    }

    public bool NavigateTo<TViewModel>(object? parameter = null)
    {
        if (_viewModelToPageMap.TryGetValue(typeof(TViewModel), out var pageType))
        {
            return _frame?.Navigate(pageType, parameter) ?? false;
        }
        return false;
    }
}
```

### NavigationView Integration

Integrate with NavigationView for shell navigation:

```csharp
private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
{
    if (args.IsSettingsSelected)
    {
        _navigationService.NavigateTo<SettingsViewModel>();
        return;
    }

    if (args.SelectedItemContainer?.Tag is string tag)
    {
        var viewModelType = Type.GetType($"MyApp.ViewModels.{tag}ViewModel");
        if (viewModelType != null)
        {
            _navigationService.NavigateTo(viewModelType);
        }
    }
}
```

## Window Management Pattern

### AppWindow Abstraction

Wrap AppWindow operations for cleaner code:

```csharp
public class WindowHelper
{
    private readonly AppWindow _appWindow;

    public WindowHelper(Window window)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
    }

    public void SetSize(int width, int height)
    {
        _appWindow.Resize(new SizeInt32(width, height));
    }

    public void CenterOnScreen()
    {
        var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        var x = (display.WorkArea.Width - _appWindow.Size.Width) / 2;
        var y = (display.WorkArea.Height - _appWindow.Size.Height) / 2;
        _appWindow.Move(new PointInt32(x, y));
    }

    public void SetTitle(string title)
    {
        _appWindow.Title = title;
    }

    public void CustomizeTitleBar(Color backgroundColor)
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = backgroundColor;
        }
    }
}
```

## Messaging Pattern

### WeakReferenceMessenger

Use the messaging system for loosely coupled communication:

```csharp
// Define message
public record UserLoggedInMessage(User User);

// Send message
WeakReferenceMessenger.Default.Send(new UserLoggedInMessage(user));

// Receive message in ViewModel
public partial class DashboardViewModel : ObservableRecipient
{
    protected override void OnActivated()
    {
        Messenger.Register<DashboardViewModel, UserLoggedInMessage>(this, (r, m) =>
        {
            r.CurrentUser = m.User;
        });
    }
}
```

### Request Messages

Use request messages for data retrieval across ViewModels:

```csharp
public class CurrentThemeRequestMessage : RequestMessage<ElementTheme> { }

// Handler
Messenger.Register<SettingsViewModel, CurrentThemeRequestMessage>(this, (r, m) =>
{
    m.Reply(r.CurrentTheme);
});

// Requester
var theme = WeakReferenceMessenger.Default.Send<CurrentThemeRequestMessage>();
```

## Settings Pattern

### Settings Service

Persist settings using local storage:

```csharp
public class SettingsService : ISettingsService
{
    private readonly ApplicationDataContainer _localSettings;

    public SettingsService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (_localSettings.Values.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        _localSettings.Values[key] = value;
    }
}
```

### Unpackaged Settings Alternative

For unpackaged apps, use file-based settings:

```csharp
public class FileSettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private Dictionary<string, object?> _settings = new();

    public FileSettingsService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyApp",
            "settings.json");
        Load();
    }

    private void Load()
    {
        if (File.Exists(_settingsPath))
        {
            var json = File.ReadAllText(_settingsPath);
            _settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var json = JsonSerializer.Serialize(_settings);
        File.WriteAllText(_settingsPath, json);
    }
}
```

## Data Template Selector Pattern

Select templates based on data type:

```csharp
public class NotificationTemplateSelector : DataTemplateSelector
{
    public DataTemplate? InfoTemplate { get; set; }
    public DataTemplate? WarningTemplate { get; set; }
    public DataTemplate? ErrorTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            InfoNotification => InfoTemplate,
            WarningNotification => WarningTemplate,
            ErrorNotification => ErrorTemplate,
            _ => base.SelectTemplateCore(item, container)
        };
    }
}
```

```xml
<Page.Resources>
    <local:NotificationTemplateSelector x:Key="NotificationSelector">
        <local:NotificationTemplateSelector.InfoTemplate>
            <DataTemplate x:DataType="models:InfoNotification">
                <InfoBar Severity="Informational" Title="{x:Bind Title}"/>
            </DataTemplate>
        </local:NotificationTemplateSelector.InfoTemplate>
        <!-- Other templates -->
    </local:NotificationTemplateSelector>
</Page.Resources>

<ListView ItemsSource="{x:Bind ViewModel.Notifications}"
          ItemTemplateSelector="{StaticResource NotificationSelector}"/>
```

## Async Loading Pattern

Handle async data loading with loading states:

```csharp
public partial class DataViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private ObservableCollection<Item> _items = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var data = await _dataService.GetItemsAsync();
            Items = new ObservableCollection<Item>(data);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

```xml
<Grid>
    <ListView ItemsSource="{x:Bind ViewModel.Items}"
              Visibility="{x:Bind ViewModel.IsLoading, Converter={StaticResource InverseBoolToVisibility}}"/>

    <ProgressRing IsActive="{x:Bind ViewModel.IsLoading, Mode=OneWay}"
                  Visibility="{x:Bind ViewModel.IsLoading, Mode=OneWay}"/>

    <InfoBar IsOpen="{x:Bind ViewModel.ErrorMessage, Converter={StaticResource NullToBool}}"
             Severity="Error"
             Title="Error"
             Message="{x:Bind ViewModel.ErrorMessage, Mode=OneWay}"/>
</Grid>
```

## Activation Pattern

Handle different activation scenarios:

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    m_window = new MainWindow();

    var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

    switch (activatedArgs.Kind)
    {
        case ExtendedActivationKind.File:
            HandleFileActivation(activatedArgs);
            break;
        case ExtendedActivationKind.Protocol:
            HandleProtocolActivation(activatedArgs);
            break;
        case ExtendedActivationKind.ToastNotification:
            HandleToastActivation(activatedArgs);
            break;
        default:
            HandleDefaultActivation();
            break;
    }

    m_window.Activate();
}

private void HandleFileActivation(AppActivationArguments args)
{
    if (args.Data is IFileActivatedEventArgs fileArgs)
    {
        var file = fileArgs.Files.FirstOrDefault() as StorageFile;
        if (file != null)
        {
            _navigationService.NavigateTo<FileViewerViewModel>(file.Path);
        }
    }
}
```

## Background Task Pattern

Register and handle background tasks:

```csharp
public static class BackgroundTaskHelper
{
    public static async Task RegisterTimerTaskAsync(string taskName, uint intervalMinutes)
    {
        var access = await BackgroundExecutionManager.RequestAccessAsync();
        if (access is BackgroundAccessStatus.DeniedBySystemPolicy or
            BackgroundAccessStatus.DeniedByUser)
        {
            return;
        }

        foreach (var task in BackgroundTaskRegistration.AllTasks)
        {
            if (task.Value.Name == taskName)
            {
                return; // Already registered
            }
        }

        var builder = new BackgroundTaskBuilder
        {
            Name = taskName
        };
        builder.SetTrigger(new TimeTrigger(intervalMinutes, false));
        builder.Register();
    }
}
```
