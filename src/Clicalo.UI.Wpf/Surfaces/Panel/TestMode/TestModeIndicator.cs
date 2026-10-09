using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Presentation.Panel.TestMode;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.TestMode;

/// <summary>
/// The permanent mark of test mode (TAC-008): a <c>warnWash</c> pill with the <c>science</c> icon and «Modo prueba ·
/// {s} s», visible the whole 30 s even when another notice replaces [tmStart]. It only projects
/// <see cref="TestModeViewModel"/> and is not a touch target.
/// </summary>
/// <remarks>The panel places it just above the notice bar (and the Tab bar next to its tools, PES-014).</remarks>
public sealed class TestModeIndicator : Border
{
    private const double IconPx = 16;
    private const double TextPx = 12;

    private readonly TestModeViewModel _testMode;
    private readonly TextBlock _text = new()
    {
        FontWeight = FontWeights.Bold,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(6, 0, 0, 0),
    };

    /// <summary>Creates the mark.</summary>
    /// <param name="testMode">Test mode.</param>
    public TestModeIndicator(TestModeViewModel testMode)
    {
        ArgumentNullException.ThrowIfNull(testMode);
        _testMode = testMode;
        CornerRadius = new CornerRadius(Radii.Button);
        Padding = new Thickness(10, 4, 10, 4);
        Margin = new Thickness(12, 6, 12, 0);
        HorizontalAlignment = HorizontalAlignment.Left;
        IsHitTestVisible = false;
        SetResourceReference(BackgroundProperty, ThemeBrushKey.For(ColorToken.WarnWash));
        _text.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(TextPx));
        _text.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        var icon = new SymbolIcon { Symbol = "science", Size = IconPx };
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.WarnText)
        );
        var row = new StackPanel { Orientation = Orientation.Horizontal };
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
