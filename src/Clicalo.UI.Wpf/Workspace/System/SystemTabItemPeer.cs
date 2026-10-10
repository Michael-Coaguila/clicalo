using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;

namespace Clicalo.UI.Wpf.Workspace.SystemSection;

/// <summary>
/// The peer of <see cref="SystemTabItem"/> (ACC-001): control type TabItem with the SelectionItem pattern instead of
/// Toggle. Selecting it is the tap; a tab cannot be deselected, another one is selected instead.
/// </summary>
/// <param name="owner">The tab.</param>
internal sealed class SystemTabItemPeer(SystemTabItem owner)
    : FrameworkElementAutomationPeer(owner),
        ISelectionItemProvider
{
    /// <inheritdoc />
    public bool IsSelected => owner.IsChecked == true;

    /// <inheritdoc />
    public IRawElementProviderSimple? SelectionContainer
    {
        get
        {
            for (
                DependencyObject? parent = VisualTreeHelper.GetParent(owner);
                parent is not null;
                parent = VisualTreeHelper.GetParent(parent)
            )
            {
                if (parent is SystemTabStrip strip && CreatePeerForElement(strip) is { } peer)
                {
                    return ProviderFromPeer(peer);
                }
            }

            return null;
        }
    }

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.SelectionItem
            ? this
            : base.GetPattern(patternInterface);

    /// <inheritdoc />
    public void Select()
    {
        if (!owner.IsEnabled)
        {
            throw new ElementNotEnabledException();
        }

        if (!IsSelected)
        {
            owner.Select();
        }
    }

    /// <inheritdoc />
    public void AddToSelection() => Select();

    /// <inheritdoc />
    public void RemoveFromSelection()
    {
        if (IsSelected)
        {
            throw new InvalidOperationException("A tab of Sistema is always selected.");
        }
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(SystemTabItem);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.TabItem;

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => owner.Focusable && owner.IsEnabled;
}
