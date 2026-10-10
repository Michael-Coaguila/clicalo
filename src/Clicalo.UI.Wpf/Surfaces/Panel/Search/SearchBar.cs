using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Clicalo.Presentation.Panel.Search;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Search;

/// <summary>
/// The search row of the panel (BUS-001, BUS-003, docs/04 §4): the 44 px field [search] and 🎤 «Dictar» next to it,
/// under the header, with the prototype's padding 0 12 8 12. It is the only control of the panel that takes the
/// keyboard focus (REG-01's single exception), and only after <see cref="SearchViewModel"/> got the
/// <c>TextInput</c> lease: then it raises <see cref="SearchViewModel.FocusFieldRequested"/>.
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
    /// <summary>Height of the field (BUS-001, docs/04 §4).</summary>
    public const double FieldHeight = 44;

    /// <summary>Size of the field's text (prototype: 15 px).</summary>
    public const double FieldFontSize = 15;

    private readonly SearchViewModel _viewModel;
    private readonly SearchPlaceholder _placeholder;
    private bool _attached = true;

    /// <summary>Creates the row; it shows only while the search is open.</summary>
    /// <param name="viewModel">The search.</param>
    public SearchBar(SearchViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Padding = new Thickness(12, 0, 12, 8);

        Field = new TextBox
        {
            Height = FieldHeight,
            MinHeight = TouchTarget.MinimumSize,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 0, 8, 0),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            AcceptsReturn = false,
            AcceptsTab = false,
            IsUndoEnabled = true,
            Text = viewModel.Query,
        };
        Field.SetResourceReference(TextBox.FontFamilyProperty, ThemeKeys.UiFont);
        Field.SetResourceReference(TextBox.FontSizeProperty, ThemeKeys.TextSize(FieldFontSize));
        Field.SetResourceReference(TextBox.ForegroundProperty, ThemeBrushKey.For(ColorToken.Text));
        Field.SetResourceReference(TextBox.CaretBrushProperty, ThemeBrushKey.For(ColorToken.Text));
        Field.TextChanged += (_, _) => OnFieldChanged();
        Field.PreviewKeyDown += OnFieldKeyDown;
        Field.ContextMenuOpening += static (_, e) => e.Handled = true;
        Field.PreviewMouseRightButtonDown += static (_, e) => e.Handled = true;
        Field.PreviewMouseRightButtonUp += static (_, e) => e.Handled = true;

        _placeholder = new SearchPlaceholder
        {
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        _placeholder.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.UiFont);
        _placeholder.SetResourceReference(
            TextBlock.FontSizeProperty,
            ThemeKeys.TextSize(FieldFontSize)
        );
        _placeholder.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );

        DictateButton = new IconButton
        {
            Symbol = "mic",
            Width = TouchTarget.MinimumSize,
            Height = FieldHeight,
            Margin = new Thickness(6, 0, 0, 0),
            Focusable = false,
            IsTabStop = false,
        };
        DictateButton.Click += (_, _) => _ = _viewModel.DictateAsync(SearchTrigger.Automation);

        var fieldContent = new Grid();
        fieldContent.Children.Add(Field);
        fieldContent.Children.Add(_placeholder);
        // TEM-009: the ring of 3 px, 2 px outside the field, while it has the keyboard.
        _ = FieldFocusRing.Attach(fieldContent, Field, Radii.Control, 1);
        var fieldHost = new Border
        {
            CornerRadius = new CornerRadius(Radii.Control),
            BorderThickness = new Thickness(1),
            Child = fieldContent,
        };
        fieldHost.SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.Field));
        fieldHost.SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Border));
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(DictateButton, Dock.Right);
        row.Children.Add(DictateButton);
        row.Children.Add(fieldHost);
        Child = row;

        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.FocusFieldRequested += (_, _) => FocusField();
        Unloaded += (_, _) => Detach();
        Loaded += (_, _) => Attach();
        ApplyTexts();
        ApplyOpen();
    }

    /// <summary>The 44 px search field; the panel registers it as a tap target.</summary>
    public TextBox Field { get; }

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

    private void OnFieldChanged()
    {
        _viewModel.Query = Field.Text;
        _placeholder.Visibility =
            Field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        _placeholder.Text = _viewModel.Placeholder;
        AutomationProperties.SetName(Field, _viewModel.Placeholder);
        AutomationProperties.SetName(DictateButton, _viewModel.DictateName);
    }
}
