using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// The handle that drags a laboratory surface with the finger (the panel in S1 row 30, the guide strip): at least
/// <see cref="MinimumSide"/> logical pixels on each side (REG-02) and exposed to UI Automation as a named Thumb, while
/// its decorative glyph stays out of the tree (its peer has no children; UIA008: a glyph is never a name).
/// </summary>
internal sealed class DragGrip : Border
{
    /// <summary>The smallest side of a touch target (REG-02).</summary>
    public const double MinimumSide = 44;

    /// <summary>Creates a grip named <paramref name="name"/>, <paramref name="width"/> logical pixels wide.</summary>
    public DragGrip(string name, double width)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Width = Math.Max(MinimumSide, width);
        MinHeight = MinimumSide;
        var glyph = new TextBlock
        {
            Text = "⠿",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Child = glyph;
        SetResourceReference(BackgroundProperty, SystemColors.ControlDarkBrushKey);
        AutomationProperties.SetName(this, name);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new DragGripAutomationPeer(this);
}
