using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// A vertical scroll area of a surface of the panel (the profile grid, Quick settings, the windows beside the bar). The
/// finger scrolls it through the pointer layer (<see cref="PanScroll"/>, TAC-004) and UI Automation through the Scroll
/// pattern of the area («desplazar hacia abajo»). Its bar only tells where the content is: it is not offered to UI
/// Automation as a control, because nobody could use it as one (a bar of 17 px and its nameless arrows are no touch
/// targets, REG-02, REG-06).
/// </summary>
internal sealed class SurfaceScrollViewer : ScrollViewer
{
    /// <summary>Creates a vertical scroll area that never takes the focus.</summary>
    public SurfaceScrollViewer()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Focusable = false;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(ScrollViewer owner) : ScrollViewerAutomationPeer(owner)
    {
        protected override List<AutomationPeer>? GetChildrenCore() =>
            base.GetChildrenCore()?.FindAll(static child => child is not ScrollBarAutomationPeer);
    }
}
