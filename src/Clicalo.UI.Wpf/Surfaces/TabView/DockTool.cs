using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>
/// A labeled tool of the bar of the Tab view in direction C (D12, PES-006, PES-008): a <see cref="TouchButton"/> whose
/// content is an icon in a box of <see cref="IconBox.Small"/> and a <see cref="TextStyleToken.LabelSmall"/> label.
/// <see cref="DockToolShape.Row"/> puts the icon before the label (a row of <see cref="ControlHeight.Small"/>, the label
/// in up to two lines, icon and label from the start so the icons of a column line up) and
/// <see cref="DockToolShape.Stacked"/> above it (a card of <see cref="ControlHeight.Medium"/>,
/// one line), as the prototype did on a side edge and on the top and bottom ones. Pressed, the button draws «elegido»
/// (<see cref="TouchButton.IsActive"/> or <see cref="TouchButton.IsOn"/>) and its icon fills in <c>accentInk</c>, the
/// one look of direction C; it never changes size.
/// </summary>
internal sealed class DockTool
{
    /// <summary>Creates a tool of <paramref name="variant"/> that runs <paramref name="tap"/> through its Click.</summary>
    /// <param name="symbol">The icon.</param>
    /// <param name="variant">Secondary for a tool, tertiary for a segment of «Qué ver».</param>
    /// <param name="shape">The icon before or above the label.</param>
    /// <param name="tap">What a tap (or UI Automation) does.</param>
    public DockTool(string symbol, ButtonVariant variant, DockToolShape shape, Action tap)
    {
        Icon = new SymbolIcon
        {
            Symbol = symbol,
            Size = IconBox.Small,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var stacked = shape == DockToolShape.Stacked;
        Label = new StyledText(string.Empty, TextStyleToken.LabelSmall)
        {
            MaxLines = stacked ? 1 : 2,
            TextAlignment = stacked ? TextAlignment.Center : TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        Lead = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        Lead.Children.Add(Icon);
        var parts = new DockPanel
        {
            LastChildFill = true,
            HorizontalAlignment = stacked ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(Lead, stacked ? Dock.Top : Dock.Left);
        Lead.Margin = stacked ? new Thickness(0, 0, 0, Space.S1) : new Thickness(0, 0, Space.S2, 0);
        _ = parts.Children.Add(Lead);
        _ = parts.Children.Add(Label);
        Button = new TouchButton
        {
            Variant = variant,
            Size = stacked ? ControlSize.Medium : ControlSize.Small,
            Content = parts,
            Padding = new Thickness(Space.S2, 0, Space.S2, 0),

            // In a column of rows the icons line up at the start, one under the other, whatever their labels.
            HorizontalContentAlignment = stacked
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left,
            MinWidth = stacked ? ControlHeight.Medium : ControlHeight.Small,
            Focusable = false,
            IsTabStop = false,
        };
        Button.Click += (_, _) => tap();
        Paint(pressed: false);
    }

    /// <summary>The button.</summary>
    public TouchButton Button { get; }

    /// <summary>The icon.</summary>
    public SymbolIcon Icon { get; }

    /// <summary>What goes before or above the label: the icon, and whatever is added beside it.</summary>
    public Grid Lead { get; }

    /// <summary>The label.</summary>
    public StyledText Label { get; }

    /// <summary>
    /// Sets the label and the accessible name: <paramref name="name"/> when the label alone does not say what the tool
    /// does (the lock says «Se pliega», its name says what a tap does).
    /// </summary>
    /// <param name="label">The text shown.</param>
    /// <param name="name">The accessible name.</param>
    public void Say(string label, string name)
    {
        Label.Text = label;
        AutomationProperties.SetName(Button, name);
    }

    /// <summary>Draws the icon as the button is: filled in <c>accentInk</c> while pressed (D12).</summary>
    /// <param name="pressed">Whether what the tool opens or switches is on.</param>
    public void Paint(bool pressed)
    {
        Icon.IsFilled = pressed;
        Icon.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(pressed ? ColorToken.AccentInk : ColorToken.Text)
        );
    }
}
