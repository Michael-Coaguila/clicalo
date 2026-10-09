using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The administrator notice (EJE-013, PAN-007: under the panic strip): a warnWash card with a warn outline, the
/// <c>admin_panel_settings</c> icon in warn, [adminMsg] in 13 px and the warn button [adminBtn] (36 visual, 44 touch).
/// It only projects <see cref="AdminNoticeViewModel"/>.
/// </summary>
public sealed class AdminNoticeView : Border
{
    private const double IconPx = 20;
    private const double TextPx = 13;
    private const double ButtonPx = 12;
    private const double ButtonHeight = 36;
    private const double Gap = 8;

    private readonly AdminNoticeViewModel _viewModel;
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TouchButton _relaunch = new()
    {
        Appearance = ButtonAppearance.Warn,
        Height = ButtonHeight,
        Padding = new Thickness(12, 0, 12, 0),
        Focusable = false,
        IsTabStop = false,
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 6, 0, 0),
    };

    /// <summary>Creates the card of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The administrator notice.</param>
    public AdminNoticeView(AdminNoticeViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Margin = new Thickness(10, 0, 10, 10);
        var icon = new SymbolIcon
        {
            Symbol = "admin_panel_settings",
            Size = IconPx,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, Gap, 0),
        };
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Warn)
        );
        _message.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(TextPx));
        _relaunch.SetResourceReference(Control.FontSizeProperty, ThemeKeys.TextSize(ButtonPx));
        _relaunch.Click += (_, _) => _viewModel.Relaunch();

        var text = new StackPanel();
        text.Children.Add(_message);
        text.Children.Add(_relaunch);
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(text);
        Child = new Card
        {
            Tone = CardTone.Warn,
            Padding = new Thickness(12, 10, 12, 10),
            Content = row,
        };

        viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>[adminBtn] while the card shows.</summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        _viewModel.IsVisible ? [new PanelTapTarget(_relaunch, _viewModel.Relaunch)] : [];

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _message.Text = _viewModel.Message;
        _relaunch.Content = _viewModel.ButtonName;
        System.Windows.Automation.AutomationProperties.SetName(this, _viewModel.Message);
    }
}
