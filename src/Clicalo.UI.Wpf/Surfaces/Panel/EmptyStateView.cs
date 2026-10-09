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
/// The empty profile card (CUA-010): a dashed outline of 2 px in line and radius 12, the <c>inbox</c> icon of 30 in
/// muted, [emptyProfT] in 14 bold, [emptyProfS] in 12 muted and the 44 px accent button «+ [addShortcut]». It only
/// projects <see cref="EmptyStateViewModel"/>.
/// </summary>
public sealed class EmptyStateView : Grid
{
    private const double IconPx = 30;
    private const double TitlePx = 14;
    private const double SubtitlePx = 12;
    private const double ButtonHeight = 44;
    private const double Gap = 8;
    private const double DashedBorder = 2;

    private readonly EmptyStateViewModel _viewModel;
    private readonly TextBlock _title = new()
    {
        FontWeight = FontWeights.Bold,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly TextBlock _subtitle = new()
    {
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, Gap, 0, 0),
    };

    private readonly TouchButton _add = new()
    {
        Appearance = ButtonAppearance.Accent,
        Symbol = "add",
        Height = ButtonHeight,
        Focusable = false,
        IsTabStop = false,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, Gap, 0, 0),
    };

    /// <summary>Creates the card of <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The empty profile.</param>
    public EmptyStateView(EmptyStateViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        Margin = new Thickness(12, 6, 12, 0);
        var outline = new Rectangle
        {
            RadiusX = Radii.Tile,
            RadiusY = Radii.Tile,
            StrokeThickness = DashedBorder,
            StrokeDashArray = new DoubleCollection([3, 2]),
        };
        outline.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));
        Children.Add(outline);

        var icon = new SymbolIcon
        {
            Symbol = "inbox",
            Size = IconPx,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        icon.Margin = new Thickness(0, 0, 0, Gap);
        _title.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(TitlePx));
        _title.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        _subtitle.SetResourceReference(TextBlock.FontSizeProperty, ThemeKeys.TextSize(SubtitlePx));
        _subtitle.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        _add.Click += (_, _) => _viewModel.Add();

        var stack = new StackPanel { Margin = new Thickness(12, 18, 12, 18) };
        stack.Children.Add(icon);
        stack.Children.Add(_title);
        stack.Children.Add(_subtitle);
        stack.Children.Add(_add);
        Children.Add(stack);

        viewModel.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>«+ Añadir atajo» while the card shows.</summary>
    public IEnumerable<PanelTapTarget> TapTargets =>
        _viewModel.IsVisible ? [new PanelTapTarget(_add, _viewModel.Add)] : [];

    /// <summary>Stops following the view model.</summary>
    public void Detach() => _viewModel.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        _title.Text = _viewModel.Title;
        _subtitle.Text = _viewModel.Subtitle;
        _add.Content = _viewModel.ButtonName;
    }
}
