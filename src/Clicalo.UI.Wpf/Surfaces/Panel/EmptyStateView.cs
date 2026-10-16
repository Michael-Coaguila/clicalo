using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The empty grid (CUA-010) in direction C (D12): in the place of the tiles, with their size, the dashed line of an
/// empty slot in <c>line</c> and, centered, the icon in <c>muted</c>, the title in <c>heading</c>, the text in
/// <c>bodySmall</c> and <c>muted</c> (two lines with «…») and one primary button. An empty profile shows
/// <c>inbox</c>, [emptyProfT], [emptyProfS] and «+ [addShortcut]»; an empty Frequents shows <c>star</c>, [freqEmptyT],
/// [freqEmptyS] and «Volver a {perfil}». When the grid has few rows, the icon and then the text give way, so the title
/// and the button are never cut. It only projects <see cref="EmptyStateViewModel"/>.
/// </summary>
public sealed class EmptyStateView : Grid
{
    private static readonly DoubleCollection Dash = Frozen();

    private readonly EmptyStateViewModel _viewModel;
    private readonly SymbolIcon _icon = PanelChrome.Icon(null, IconBox.Large, ColorToken.Muted);
    private readonly StyledText _title = PanelChrome.Text(TextStyleToken.Heading);
    private readonly StyledText _subtitle = PanelChrome.Text(
        TextStyleToken.BodySmall,
        ColorToken.Muted,
        lines: 2
    );

    private readonly TouchButton _add = new()
    {
        Variant = ButtonVariant.Primary,
        Focusable = false,
        IsTabStop = false,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, Space.S2, 0, 0),
    };

    private readonly StackPanel _stack = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(Space.S3),
    };

    /// <summary>Creates the card of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The empty profile.</param>
    public EmptyStateView(EmptyStateViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        ClipToBounds = true;
        var outline = new Rectangle
        {
            RadiusX = Radii.Medium,
            RadiusY = Radii.Medium,
            StrokeDashArray = Dash,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false,
        };
        outline.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));
        outline.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);
        Children.Add(outline);

        _icon.Margin = new Thickness(0, 0, 0, Space.S2);
        _title.TextAlignment = TextAlignment.Center;
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitle.TextAlignment = TextAlignment.Center;
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitle.Margin = new Thickness(0, Space.S1, 0, 0);
        _add.Click += (_, _) => _viewModel.Add();
        _stack.Children.Add(_icon);
        _stack.Children.Add(_title);
        _stack.Children.Add(_subtitle);
        _stack.Children.Add(_add);
        Children.Add(_stack);

        SizeChanged += (_, _) => Fit();
        viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>The button while the card shows.</summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        _viewModel.IsVisible ? [new PanelTapTarget(_add, _viewModel.Add)] : [];

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private static DoubleCollection Frozen()
    {
        var dash = new DoubleCollection([Stroke.Selected, Stroke.Selected]);
        dash.Freeze();
        return dash;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _icon.Symbol = _viewModel.Icon;
        _title.Text = _viewModel.Title;
        _subtitle.Text = _viewModel.Subtitle;
        _add.Symbol = _viewModel.ButtonIcon;
        _add.Content = _viewModel.ButtonName;
        Fit();
    }

    /// <summary>Gives way to the icon, then to the text, while the card is lower than its content.</summary>
    private void Fit()
    {
        if (ActualHeight <= 0)
        {
            return;
        }

        _icon.Visibility = Visibility.Visible;
        _subtitle.Visibility = Visibility.Visible;
        foreach (var part in new UIElement[] { _icon, _subtitle })
        {
            _stack.Measure(new Size(ActualWidth, double.PositiveInfinity));
            if (_stack.DesiredSize.Height <= ActualHeight)
            {
                return;
            }

            part.Visibility = Visibility.Collapsed;
        }
    }
}
