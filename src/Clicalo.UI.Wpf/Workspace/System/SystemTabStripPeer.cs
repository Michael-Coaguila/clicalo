using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Clicalo.UI.Wpf.Workspace.SystemSection;

/// <summary>
/// The peer of <see cref="SystemTabStrip"/> (ACC-001): control type Tab with the Selection pattern, exactly one tab
/// selected at a time.
/// </summary>
/// <param name="owner">The row of tabs.</param>
internal sealed class SystemTabStripPeer(SystemTabStrip owner)
    : FrameworkElementAutomationPeer(owner),
        ISelectionProvider
{
    /// <inheritdoc />
    public bool CanSelectMultiple => false;

    /// <inheritdoc />
    public bool IsSelectionRequired => true;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    /// <inheritdoc />
    public IRawElementProviderSimple[] GetSelection()
    {
        var selected = new List<IRawElementProviderSimple>();
        foreach (var child in owner.Children)
        {
            if (
                child is SystemTabItem { IsChecked: true } tab
                && CreatePeerForElement(tab) is { } peer
                && ProviderFromPeer(peer) is { } provider
            )
            {
                selected.Add(provider);
            }
        }

        return [.. selected];
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(SystemTabStrip);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Tab;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;
}
