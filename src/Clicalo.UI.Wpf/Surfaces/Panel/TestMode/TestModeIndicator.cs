using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.Panel.TestMode;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.TestMode;

/// <summary>
/// The permanent mark of test mode (TAC-008) in direction C (D12): a band of <see cref="ControlHeight.Small"/> in
/// <c>warnWash</c> (attention) and radius <see cref="Radii.Medium"/> with the <c>science</c> icon in <c>warnText</c> and
/// «Modo prueba · {s} s» in <c>label</c>, visible the whole 30 s even when another notice replaces [tmStart]. It only
/// projects <see cref="TestModeViewModel"/> and is not a touch target.
/// </summary>
/// <remarks>The panel shows it as a layer at the foot of its body (and the Tab bar next to its tools, PES-014).</remarks>
public sealed class TestModeIndicator : Border
{
    private readonly TestModeViewModel _testMode;
    private readonly StyledText _text = new(string.Empty, TextStyleToken.Label)
    {
        MaxLines = 1,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Creates the mark.</summary>
    /// <param name="testMode">Test mode.</param>
    public TestModeIndicator(TestModeViewModel testMode)
    {
        ArgumentNullException.ThrowIfNull(testMode);
        _testMode = testMode;
        CornerRadius = new CornerRadius(Radii.Medium);
        Padding = new Thickness(Space.S3, 0, Space.S3, 0);
        MinHeight = ControlHeight.Small;
        IsHitTestVisible = false;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.WarnWash));
        var icon = new SymbolIcon
        {
            Symbol = "science",
            Size = IconBox.Small,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Space.S2, 0),
        };
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.WarnText)
        );
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(_text);
        Child = row;
        _testMode.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>Stops following test mode when the panel closes.</summary>
    public void Detach() => _testMode.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs change) => Refresh();

    private void Refresh()
    {
        Visibility = _testMode.IsOn ? Visibility.Visible : Visibility.Collapsed;
        _text.Text = _testMode.IndicatorText;
        AutomationProperties.SetName(this, _testMode.IndicatorText);
    }
}
