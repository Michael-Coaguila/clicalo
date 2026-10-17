using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel.EditMode;

/// <summary>
/// The dashed «+ [add]» tile that ends the grid in edit mode (CUA-012) in direction C (D12): the size and radius of a
/// tile with no fill and a dashed line in <c>line</c>, and the <c>add</c> icon over [add] in <c>accentInk</c>, the color
/// of an action. Not in Frequents nor in the search. A tap opens the library for the profile in view. It only projects
/// <see cref="EditModeViewModel"/>.
/// </summary>
/// <remarks>
/// The grid places it after the last tile of the last page, in the next cell; the panel registers <see cref="TapTarget"/>.
/// </remarks>
public sealed class AddTileView : Grid
{
    private static readonly DoubleCollection Dash = Frozen();

    private readonly EditModeViewModel _editMode;
    private readonly TouchButton _button;

    /// <summary>Creates the tile.</summary>
    /// <param name="editMode">Edit mode.</param>
    /// <param name="iconSize">The icon box of the tiles of the grid (<see cref="IconBox"/>).</param>
    /// <param name="name">The style of the names of the tiles of the grid.</param>
    public AddTileView(
        EditModeViewModel editMode,
        double iconSize = IconBox.Medium,
        TextStyleToken name = TextStyleToken.Label
    )
    {
        ArgumentNullException.ThrowIfNull(editMode);
        _editMode = editMode;
        var outline = new Rectangle
        {
            RadiusX = Radii.Medium,
            RadiusY = Radii.Medium,
            StrokeDashArray = Dash,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };
        outline.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));
        outline.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);

        Label = new StyledText(string.Empty, name, ColorToken.AccentInk)
        {
            MaxLines = 1,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, Space.S1, 0, 0),
        };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(PanelChrome.Icon("add", iconSize, ColorToken.AccentInk));
        stack.Children.Add(Label);

        _button = new TouchButton
        {
            Variant = ButtonVariant.Tertiary,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),
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
    public StyledText Label { get; }

    /// <summary>The tile as a tap target of the panel while it shows.</summary>
    public PanelTapTarget TapTarget => new(_button, _editMode.Add);

    /// <summary>Stops following edit mode when the grid goes away.</summary>
    public void Detach() => _editMode.PropertyChanged -= OnChanged;

    private static DoubleCollection Frozen()
    {
        var dash = new DoubleCollection([Stroke.Selected, Stroke.Selected]);
        dash.Freeze();
        return dash;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs change) => Refresh();

    private void Refresh()
    {
        Visibility = _editMode.ShowsAdd ? Visibility.Visible : Visibility.Collapsed;
        Label.Text = _editMode.AddText;
        AutomationProperties.SetName(_button, _editMode.AddText);
    }
}
