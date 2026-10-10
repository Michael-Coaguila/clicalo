using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// A uniform grid whose cells UI Automation finds in the order they were added, whatever their z-order. The grid of
/// shortcuts draws each tile above the next one (the × of edit mode reaches past the corner of its tile), and WPF lists
/// the children of a panel by z-order: without this, a screen reader walks the tiles from the last to the first (REG-06).
/// The grid itself is not an element of the control view; only its cells are.
/// </summary>
internal sealed class ReadingOrderGrid : UniformGrid
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(ReadingOrderGrid owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override List<AutomationPeer> GetChildrenCore()
        {
            var peers = new List<AutomationPeer>();
            foreach (UIElement child in owner.Children)
            {
                Collect(child, peers);
            }

            return peers;
        }

        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;

        private static void Collect(DependencyObject node, List<AutomationPeer> peers)
        {
            if (node is UIElement element && CreatePeerForElement(element) is { } peer)
            {
                peers.Add(peer);
                return;
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                Collect(VisualTreeHelper.GetChild(node, i), peers);
            }
        }
    }
}
