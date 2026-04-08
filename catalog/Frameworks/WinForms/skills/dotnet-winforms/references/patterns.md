# WinForms Patterns Reference

## Architectural Patterns

### MVP (Model-View-Presenter)

MVP is the recommended pattern for WinForms applications that need testability and separation of concerns.

**Structure:**
- **Model**: Domain entities and business logic
- **View**: Form implementing an interface, handles UI concerns only
- **Presenter**: Mediates between Model and View, contains presentation logic

**Key Characteristics:**
- View is passive and raises events
- Presenter subscribes to view events and updates view properties
- Presenter can be unit tested without UI
- View interface enables mocking

```csharp
// View contract
public interface IOrderView
{
    int OrderId { get; set; }
    string CustomerName { get; set; }
    decimal Total { get; set; }
    IEnumerable<OrderLine> Lines { set; }

    event EventHandler LoadRequested;
    event EventHandler SaveRequested;
    event EventHandler CancelRequested;

    void Close();
    void ShowValidationError(string field, string message);
    void ClearValidationErrors();
}

// Presenter
public class OrderPresenter
{
    private readonly IOrderView _view;
    private readonly IOrderRepository _repository;
    private Order? _currentOrder;

    public OrderPresenter(IOrderView view, IOrderRepository repository)
    {
        _view = view;
        _repository = repository;

        _view.LoadRequested += async (s, e) => await LoadOrderAsync();
        _view.SaveRequested += async (s, e) => await SaveOrderAsync();
        _view.CancelRequested += (s, e) => _view.Close();
    }

    private async Task LoadOrderAsync()
    {
        _currentOrder = await _repository.GetByIdAsync(_view.OrderId);
        if (_currentOrder != null)
        {
            _view.CustomerName = _currentOrder.CustomerName;
            _view.Total = _currentOrder.Total;
            _view.Lines = _currentOrder.Lines;
        }
    }

    private async Task SaveOrderAsync()
    {
        _view.ClearValidationErrors();

        if (string.IsNullOrWhiteSpace(_view.CustomerName))
        {
            _view.ShowValidationError("CustomerName", "Customer name is required");
            return;
        }

        if (_currentOrder != null)
        {
            _currentOrder.CustomerName = _view.CustomerName;
            await _repository.SaveAsync(_currentOrder);
            _view.Close();
        }
    }
}
```

### MVVM (Model-View-ViewModel)

MVVM can be used in WinForms with data binding, though it is more common in WPF. Use when:
- Heavy data binding requirements
- Sharing ViewModels between WinForms and WPF
- Team is familiar with MVVM from other frameworks

```csharp
public class OrderViewModel : INotifyPropertyChanged
{
    private string _customerName = string.Empty;
    private decimal _total;
    private bool _isBusy;

    public string CustomerName
    {
        get => _customerName;
        set { _customerName = value; OnPropertyChanged(); }
    }

    public decimal Total
    {
        get => _total;
        set { _total = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
    }

    public bool IsNotBusy => !IsBusy;

    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### Passive View

A stricter variant of MVP where the view contains zero logic:
- All decisions made by presenter
- View only exposes properties and events
- Maximum testability, minimum view code

```csharp
// Passive view - no logic at all
public partial class CustomerForm : Form, ICustomerView
{
    public string FirstName { get => txtFirstName.Text; set => txtFirstName.Text = value; }
    public string LastName { get => txtLastName.Text; set => txtLastName.Text = value; }
    public bool SaveEnabled { get => btnSave.Enabled; set => btnSave.Enabled = value; }

    public event EventHandler? FirstNameChanged;
    public event EventHandler? LastNameChanged;
    public event EventHandler? SaveClicked;

    public CustomerForm()
    {
        InitializeComponent();
        txtFirstName.TextChanged += (s, e) => FirstNameChanged?.Invoke(this, e);
        txtLastName.TextChanged += (s, e) => LastNameChanged?.Invoke(this, e);
        btnSave.Click += (s, e) => SaveClicked?.Invoke(this, e);
    }
}

// Presenter controls everything
public class CustomerPresenter
{
    private readonly ICustomerView _view;

    public CustomerPresenter(ICustomerView view)
    {
        _view = view;
        _view.SaveEnabled = false;

        _view.FirstNameChanged += (s, e) => UpdateSaveEnabled();
        _view.LastNameChanged += (s, e) => UpdateSaveEnabled();
    }

    private void UpdateSaveEnabled()
    {
        _view.SaveEnabled = !string.IsNullOrWhiteSpace(_view.FirstName)
                         && !string.IsNullOrWhiteSpace(_view.LastName);
    }
}
```

## Data Binding Patterns

### Master-Detail Binding

Common pattern for list-detail UIs:

```csharp
public partial class MasterDetailForm : Form
{
    private readonly BindingSource _masterSource = new();
    private readonly BindingSource _detailSource = new();

    public MasterDetailForm()
    {
        InitializeComponent();

        // Link detail to master
        _detailSource.DataSource = _masterSource;
        _detailSource.DataMember = "OrderLines"; // Navigation property

        dgvOrders.DataSource = _masterSource;
        dgvOrderLines.DataSource = _detailSource;

        // Detail controls bind to detail source
        txtLineDescription.DataBindings.Add("Text", _detailSource, "Description");
        txtLineQuantity.DataBindings.Add("Text", _detailSource, "Quantity");
    }

    private async Task LoadAsync()
    {
        var orders = await _orderService.GetAllWithLinesAsync();
        _masterSource.DataSource = new BindingList<Order>(orders.ToList());
    }
}
```

### Two-Way Binding with Validation

```csharp
public partial class EditForm : Form
{
    private readonly BindingSource _bindingSource = new();
    private readonly ErrorProvider _errorProvider = new();

    private void SetupBindings(Customer customer)
    {
        _bindingSource.DataSource = customer;

        // Two-way binding with format and parse
        var nameBinding = new Binding("Text", _bindingSource, "Name", true);
        nameBinding.Format += (s, e) => e.Value = e.Value?.ToString()?.Trim();
        nameBinding.Parse += (s, e) => e.Value = e.Value?.ToString()?.Trim();
        txtName.DataBindings.Add(nameBinding);

        // Binding with null handling
        txtEmail.DataBindings.Add("Text", _bindingSource, "Email",
            true, DataSourceUpdateMode.OnPropertyChanged, string.Empty);

        // Checkbox binding
        chkActive.DataBindings.Add("Checked", _bindingSource, "IsActive",
            true, DataSourceUpdateMode.OnPropertyChanged);

        // ComboBox binding
        cboCategory.DataSource = _categories;
        cboCategory.DisplayMember = "Name";
        cboCategory.ValueMember = "Id";
        cboCategory.DataBindings.Add("SelectedValue", _bindingSource, "CategoryId");
    }
}
```

### Observable Collection Pattern

```csharp
public class ObservableList<T> : BindingList<T>
{
    private bool _raiseListChangedEvents = true;

    public void AddRange(IEnumerable<T> items)
    {
        _raiseListChangedEvents = false;
        try
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _raiseListChangedEvents = true;
            ResetBindings();
        }
    }

    protected override void OnListChanged(ListChangedEventArgs e)
    {
        if (_raiseListChangedEvents)
        {
            base.OnListChanged(e);
        }
    }
}
```

## Validation Patterns

### Centralized Validation

```csharp
public class FormValidator
{
    private readonly ErrorProvider _errorProvider;
    private readonly Dictionary<Control, Func<string?>> _validators = new();

    public FormValidator(Form form)
    {
        _errorProvider = new ErrorProvider(form);
        _errorProvider.BlinkStyle = ErrorBlinkStyle.NeverBlink;
    }

    public void AddRule(Control control, Func<string?> validator)
    {
        _validators[control] = validator;
        control.Validating += (s, e) =>
        {
            var error = validator();
            _errorProvider.SetError(control, error ?? string.Empty);
            if (!string.IsNullOrEmpty(error))
            {
                e.Cancel = true;
            }
        };
    }

    public bool ValidateAll()
    {
        var isValid = true;
        foreach (var kvp in _validators)
        {
            var error = kvp.Value();
            _errorProvider.SetError(kvp.Key, error ?? string.Empty);
            if (!string.IsNullOrEmpty(error))
            {
                isValid = false;
            }
        }
        return isValid;
    }

    public void ClearAll()
    {
        foreach (var control in _validators.Keys)
        {
            _errorProvider.SetError(control, string.Empty);
        }
    }
}

// Usage
public partial class CustomerForm : Form
{
    private readonly FormValidator _validator;

    public CustomerForm()
    {
        InitializeComponent();

        _validator = new FormValidator(this);
        _validator.AddRule(txtName, () =>
            string.IsNullOrWhiteSpace(txtName.Text) ? "Name is required" : null);
        _validator.AddRule(txtEmail, () =>
            !txtEmail.Text.Contains('@') ? "Invalid email format" : null);
        _validator.AddRule(txtAge, () =>
            !int.TryParse(txtAge.Text, out var age) || age < 0 || age > 150
                ? "Age must be between 0 and 150" : null);
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        if (_validator.ValidateAll())
        {
            SaveCustomer();
        }
    }
}
```

### IDataErrorInfo Validation

```csharp
public class Customer : IDataErrorInfo, INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _email = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); }
    }

    // IDataErrorInfo implementation
    public string Error => string.Empty;

    public string this[string columnName]
    {
        get
        {
            return columnName switch
            {
                nameof(Name) when string.IsNullOrWhiteSpace(Name) => "Name is required",
                nameof(Email) when !string.IsNullOrEmpty(Email) && !Email.Contains('@') => "Invalid email",
                _ => string.Empty
            };
        }
    }

    public bool IsValid => string.IsNullOrEmpty(this[nameof(Name)])
                        && string.IsNullOrEmpty(this[nameof(Email)]);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

## Form Communication Patterns

### Mediator Pattern

For complex multi-form coordination:

```csharp
public interface IFormMediator
{
    void Register<TMessage>(Action<TMessage> handler);
    void Send<TMessage>(TMessage message);
}

public class FormMediator : IFormMediator
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Register<TMessage>(Action<TMessage> handler)
    {
        var type = typeof(TMessage);
        if (!_handlers.ContainsKey(type))
        {
            _handlers[type] = new List<Delegate>();
        }
        _handlers[type].Add(handler);
    }

    public void Send<TMessage>(TMessage message)
    {
        var type = typeof(TMessage);
        if (_handlers.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers.Cast<Action<TMessage>>())
            {
                handler(message);
            }
        }
    }
}

// Messages
public record CustomerSelectedMessage(int CustomerId);
public record CustomerUpdatedMessage(Customer Customer);

// Usage
public partial class CustomerListForm : Form
{
    private readonly IFormMediator _mediator;

    public CustomerListForm(IFormMediator mediator)
    {
        _mediator = mediator;

        dgvCustomers.SelectionChanged += (s, e) =>
        {
            if (dgvCustomers.CurrentRow?.DataBoundItem is Customer c)
            {
                _mediator.Send(new CustomerSelectedMessage(c.Id));
            }
        };
    }
}

public partial class CustomerDetailForm : Form
{
    private readonly IFormMediator _mediator;

    public CustomerDetailForm(IFormMediator mediator)
    {
        _mediator = mediator;
        _mediator.Register<CustomerSelectedMessage>(msg => LoadCustomer(msg.CustomerId));
    }
}
```

### Parent-Child Form Pattern

```csharp
public partial class MainForm : Form
{
    public void OpenCustomerEditor(Customer customer)
    {
        using var editor = new CustomerEditorForm(customer);
        editor.CustomerSaved += OnCustomerSaved;

        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            RefreshCustomerList();
        }
    }

    private void OnCustomerSaved(object? sender, CustomerSavedEventArgs e)
    {
        // Handle save notification
        statusLabel.Text = $"Customer {e.Customer.Name} saved";
    }
}

public partial class CustomerEditorForm : Form
{
    public event EventHandler<CustomerSavedEventArgs>? CustomerSaved;

    private readonly Customer _customer;

    public CustomerEditorForm(Customer customer)
    {
        InitializeComponent();
        _customer = customer;
        BindCustomer();
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        if (ValidateChildren())
        {
            UpdateCustomerFromControls();
            CustomerSaved?.Invoke(this, new CustomerSavedEventArgs(_customer));
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

public class CustomerSavedEventArgs : EventArgs
{
    public Customer Customer { get; }
    public CustomerSavedEventArgs(Customer customer) => Customer = customer;
}
```

## Threading Patterns

### Safe UI Updates

```csharp
public partial class DataForm : Form
{
    private readonly SynchronizationContext _syncContext;

    public DataForm()
    {
        InitializeComponent();
        _syncContext = SynchronizationContext.Current!;
    }

    private async Task ProcessInBackgroundAsync()
    {
        // Start background work
        var data = await Task.Run(() => LoadExpensiveData());

        // Already on UI thread due to await in WinForms context
        dgvData.DataSource = data;
    }

    // For fire-and-forget or manual threading
    private void StartBackgroundWork()
    {
        Task.Run(() =>
        {
            var result = DoWork();

            // Post back to UI thread
            _syncContext.Post(_ =>
            {
                lblResult.Text = result;
            }, null);
        });
    }

    // Extension method approach
    private void UpdateStatusSafe(string status)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateStatusSafe(status));
            return;
        }
        lblStatus.Text = status;
    }
}
```

### Cancellation Pattern

```csharp
public partial class LongOperationForm : Form
{
    private CancellationTokenSource? _cts;

    private async void btnStart_Click(object sender, EventArgs e)
    {
        _cts = new CancellationTokenSource();
        btnStart.Enabled = false;
        btnCancel.Enabled = true;

        try
        {
            await ProcessDataAsync(_cts.Token);
            MessageBox.Show("Completed");
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("Cancelled");
        }
        finally
        {
            btnStart.Enabled = true;
            btnCancel.Enabled = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        _cts?.Cancel();
    }

    private async Task ProcessDataAsync(CancellationToken ct)
    {
        var items = await GetItemsAsync();
        var progress = new Progress<int>(p => progressBar.Value = p);

        for (int i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessItemAsync(items[i]);
            ((IProgress<int>)progress).Report((i + 1) * 100 / items.Count);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            e.Cancel = true; // Prevent close until operation stops
            // Or: wait for cancellation to complete before allowing close
        }
        base.OnFormClosing(e);
    }
}
```
