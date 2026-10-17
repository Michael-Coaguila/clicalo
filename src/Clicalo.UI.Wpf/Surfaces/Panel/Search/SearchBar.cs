using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Clicalo.Presentation.Panel.Search;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Search;

/// <summary>
/// The search of the panel (BUS-001, BUS-003) in direction C (D12): a layer in the place of the row «Qué ver», which the
/// search with text hides anyway (PAN-008), right above the grid that shows the results; nothing moves and the rest of
/// the panel looks the same while the field is empty. It holds the field of <see cref="ControlHeight.Medium"/> (<see cref="TextInput"/>
/// with the magnifier and [search] as its placeholder, one line with «…») and 🎤 «Dictar», a secondary icon button of
/// the same height. It is the only control of the panel that takes the keyboard focus (REG-01's single exception), and
/// only after <see cref="SearchViewModel"/> got the <c>TextInput</c> lease: then it raises
/// <see cref="SearchViewModel.FocusFieldRequested"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Finger, pen and mouse reach the panel through its pointer layer, not through WPF: the panel registers
/// <see cref="Field"/> and <see cref="DictateButton"/> as tap targets and calls <see cref="FieldTapped"/> and
/// <see cref="DictateTapped"/> (ACC-007: the field is the only one that opens the touch keyboard).</item>
/// <item>UI Automation reaches them directly: the field's Value pattern writes the query (Voice access dictation), and
/// Invoke on 🎤 dictates (ACC-004).</item>
/// <item>The field never opens the text box's own context menu: that menu is a popup window that would take the
/// focus (REG-01, BannedSymbols.Surfaces).</item>
/// </list>
/// </remarks>
public sealed class SearchBar : Border
{
    private readonly SearchViewModel _viewModel;
    private readonly TextInput _input;
    private bool _attached = true;

    /// <summary>Creates the layer; it shows only while the search is open.</summary>
    /// <param name="viewModel">The search.</param>
    public SearchBar(SearchViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));

        _input = new TextInput(viewModel.Placeholder, viewModel.Placeholder) { Symbol = "search" };
        Field = _input.Box;
        Field.AcceptsTab = false;
        Field.IsUndoEnabled = true;
        Field.Text = viewModel.Query;
        Field.TextChanged += (_, _) => _viewModel.Query = Field.Text;
        Field.PreviewKeyDown += OnFieldKeyDown;
        Field.ContextMenuOpening += static (_, e) => e.Handled = true;
        Field.PreviewMouseRightButtonDown += static (_, e) => e.Handled = true;
        Field.PreviewMouseRightButtonUp += static (_, e) => e.Handled = true;

        // Its own 🎤, not the field's: dictation takes the keyboard only through the view model, after the lease (REG-01).
        DictateButton = new IconButton
        {
            Symbol = "mic",
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Medium,
            Margin = new Thickness(Space.S2, 0, 0, 0),
            Focusable = false,
            IsTabStop = false,
        };
        DictateButton.Click += (_, _) => _ = _viewModel.DictateAsync(SearchTrigger.Automation);

        var row = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(DictateButton, Dock.Right);
        row.Children.Add(DictateButton);
        row.Children.Add(_input);
        Child = row;

        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.FocusFieldRequested += (_, _) => FocusField();
        Unloaded += (_, _) => Detach();
        Loaded += (_, _) => Attach();
        ApplyTexts();
        ApplyOpen();
    }

    /// <summary>The search field; the panel registers it as a tap target.</summary>
    public TextBox Field { get; }

    /// <summary>The field with its magnifier and its placeholder.</summary>
    public TextInput Input => _input;

    /// <summary>🎤 «Dictar»; the panel registers it as a tap target.</summary>
    public IconButton DictateButton { get; }

    /// <summary>A tap on <see cref="Field"/> reached the panel: take the keyboard again if needed and show the touch keyboard.</summary>
    /// <param name="origin">Touch, pen and mouse come as <see cref="SearchTrigger.Touch"/>.</param>
    public Task FieldTapped(SearchTrigger origin) => _viewModel.FieldTappedAsync(origin);

    /// <summary>A tap on <see cref="DictateButton"/> reached the panel: focus the field and start dictation.</summary>
    /// <param name="origin">Touch, pen and mouse come as <see cref="SearchTrigger.Touch"/>.</param>
    public Task DictateTapped(SearchTrigger origin) => _viewModel.DictateAsync(origin);

    private void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _viewModel.PropertyChanged += OnViewModelChanged;
        ApplyTexts();
        ApplyOpen();
        SyncText();
    }

    private void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _viewModel.PropertyChanged -= OnViewModelChanged;
    }

    /// <summary>Gives the field the keyboard focus with the caret at the end (only effective under the lease).</summary>
    private void FocusField()
    {
        _ = Keyboard.Focus(Field);
        Field.CaretIndex = Field.Text.Length;
    }

    private void OnFieldKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                // PAN-010 step 2: Esc leaves the field and gives the foreground back.
                e.Handled = true;
                _ = _viewModel.LeaveFieldAsync();
                break;
            case Key.Apps:
            case Key.F10 when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                // The text box's own context menu would be an activatable popup (REG-01).
                e.Handled = true;
                break;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs change)
    {
        switch (change.PropertyName)
        {
            case nameof(SearchViewModel.IsOpen):
                ApplyOpen();
                break;
            case nameof(SearchViewModel.Query):
                SyncText();
                break;
            case nameof(SearchViewModel.Placeholder):
            case nameof(SearchViewModel.DictateName):
                ApplyTexts();
                break;
        }
    }

    private void SyncText()
    {
        if (!string.Equals(Field.Text, _viewModel.Query, StringComparison.Ordinal))
        {
            Field.Text = _viewModel.Query;
        }
    }

    private void ApplyOpen() =>
        Visibility = _viewModel.IsOpen ? Visibility.Visible : Visibility.Collapsed;

    private void ApplyTexts()
    {
        _input.SetPlaceholder(_viewModel.Placeholder);
        AutomationProperties.SetName(Field, _viewModel.Placeholder);
        AutomationProperties.SetName(DictateButton, _viewModel.DictateName);
    }
}
