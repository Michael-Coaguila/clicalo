using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.EditMode;

/// <summary>
/// The dashed «+ [add]» tile that ends the grid in edit mode (CUA-012, prototype line 218): the tile's height and
/// radius 12, a 2 px dashed outline in <c>line</c>, the <c>add</c> icon over [add] in bold <c>muted</c>. Not in
/// Frequents nor in the search. A tap opens the library for the profile in view. It only projects
/// <see cref="EditModeViewModel"/>.
/// </summary>
/// <remarks>
/// The grid places it after the last tile of the last page, in the next cell; the panel registers <see cref="TapTarget"/>.
/// </remarks>
public sealed class AddTileView : Grid
{
    private const double DashedBorder = 2;

    private readonly EditModeViewModel _editMode;
    private readonly TouchButton _button;

    /// <summary>Creates the tile.</summary>
    /// <param name="editMode">Edit mode.</param>
    /// <param name="iconPx">The icon size of the panel's size (20, 28 or 34, docs/04 «Medidas»).</param>
    /// <param name="labelPx">The label size of the panel's size (12, 14 or 16).</param>
    public AddTileView(EditModeViewModel editMode, double iconPx, double labelPx)
    {
        ArgumentNullException.ThrowIfNull(editMode);
        _editMode = editMode;
        var outline = new Rectangle
        {
            RadiusX = Radii.Tile,
            RadiusY = Radii.Tile,
            StrokeThickness = DashedBorder,
            StrokeDashArray = new DoubleCollection([3, 2]),
            IsHitTestVisible = false,
        };
        outline.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));

        var icon = new SymbolIcon
        {
            Symbol = "add",
            Size = iconPx,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        Label = new TextBlock
        {
            FontWeight = FontWeights.Bold,
            FontSize = labelPx,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        };
        Label.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(icon);
        stack.Children.Add(Label);

        _button = new TouchButton
        {
            Appearance = ButtonAppearance.Ghost,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(0),
            Focusable = false,
            IsTabStop = false,
            Content = stack,
        };
        _button.Click += (_, _) => _editMode.Add();
        Children.Add(outline);
        Children.Add(_button);

        _editMode.PropertyChanged += OnChanged;
        Refresh();
    }

    /// <summary>[add] under the icon.</summary>
    public TextBlock Label { get; }

    /// <summary>The tile as a tap target of the panel while it shows.</summary>
    public PanelTapTarget TapTarget => new(_button, _editMode.Add);

    /// <summary>Stops following edit mode when the grid goes away.</summary>
    public void Detach() => _editMode.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs change) => Refresh();

    private void Refresh()
    {
        Visibility = _editMode.ShowsAdd ? Visibility.Visible : Visibility.Collapsed;
        Label.Text = _editMode.AddText;
        AutomationProperties.SetName(_button, _editMode.AddText);
    }
}
