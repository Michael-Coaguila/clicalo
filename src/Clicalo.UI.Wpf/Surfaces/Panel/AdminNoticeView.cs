using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The administrator notice (EJE-013) in direction C (D12): a card of the warn tone (attention) with the
/// <c>admin_panel_settings</c> icon in <c>warnText</c>, [adminMsg] in <c>bodySmall</c> and the secondary button
/// [adminBtn] of 44. The panel shows it as a layer at the foot of its body, over the tiles, without moving them. It
/// only projects <see cref="AdminNoticeViewModel"/>.
/// </summary>
public sealed class AdminNoticeView : Border
{
    private readonly AdminNoticeViewModel _viewModel;
    private readonly StyledText _message = PanelChrome.Text(TextStyleToken.BodySmall, lines: 0);
    private readonly TouchButton _relaunch = new()
    {
        Variant = ButtonVariant.Secondary,
        MaxLines = 2,
        Focusable = false,
        IsTabStop = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Margin = new Thickness(0, Space.S3, 0, 0),
    };

    /// <summary>Creates the card of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The administrator notice.</param>
    public AdminNoticeView(AdminNoticeViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        var icon = PanelChrome.Icon("admin_panel_settings", IconBox.Medium, ColorToken.WarnText);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 0, Space.S3, 0);
        _relaunch.Click += (_, _) => _viewModel.Relaunch();

        var text = new StackPanel();
        text.Children.Add(_message);
        text.Children.Add(_relaunch);
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(text);
        Child = new Card { Tone = CardTone.Warn, Content = row };

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
