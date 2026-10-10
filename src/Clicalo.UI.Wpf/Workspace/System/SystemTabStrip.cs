using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Workspace.SystemSection;

/// <summary>
/// The three tabs of «Sistema» (SIS-001) in a row, exposed to UI Automation as a Tab control whose children are
/// TabItems with the SelectionItem pattern (ACC-001), so Narrator and voice control say «pestaña 2 de 3, seleccionada»
/// and can switch tabs by name.
/// </summary>
internal sealed class SystemTabStrip : Grid
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SystemTabStripPeer(this);
}
