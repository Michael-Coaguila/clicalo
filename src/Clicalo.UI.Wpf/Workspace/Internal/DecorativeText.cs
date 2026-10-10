using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// A text that only draws a separator between controls (the «+» between the keys of a combination): it says nothing by
/// itself, so UI Automation does not see it (UIA008: a glyph is never a name, and a reader would say «más» between
/// every two keys).
/// </summary>
internal sealed class DecorativeText : TextBlock
{
    /// <inheritdoc />
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}
