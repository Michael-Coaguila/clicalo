using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Surfaces.Panel.Search;

/// <summary>
/// The placeholder drawn over the empty search field ([search], muted). It is decoration: the field already carries the
/// same text as its UI Automation name, so the placeholder has no automation element of its own (no duplicate for
/// Narrator or Voice access).
/// </summary>
internal sealed class SearchPlaceholder : TextBlock
{
    /// <inheritdoc />
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}
