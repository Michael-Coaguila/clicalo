using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Header;

/// <summary>
/// The grip ⋮⋮ of the header (PAN-004): with the icon and the name of the profile it is the zone that drags the panel.
/// UI Automation sees a Thumb named [move] whose glyph is its drawing (ACC-001): a name on a plain Border never reached
/// it. It is not focusable: the finger drags it through the pointer layer of the panel.
/// </summary>
public sealed class PanelGrip : Border
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(PanelGrip owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Thumb;

        protected override string GetClassNameCore() => nameof(PanelGrip);

        protected override bool IsKeyboardFocusableCore() => false;

        protected override List<AutomationPeer>? GetChildrenCore() => null;
    }
}
