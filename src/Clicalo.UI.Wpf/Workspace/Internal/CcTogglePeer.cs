using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The peer of <see cref="CcToggle"/> (ACC-001, REG-06): exactly one pattern, the one of its
/// <see cref="CcToggle.Role"/>. A switch is a Button with Toggle; a choice of a group is a RadioButton with
/// SelectionItem (selecting it is the tap; it is deselected by choosing another one); a collapsible header is a
/// Button with ExpandCollapse. The state is always the view model's.
/// </summary>
/// <param name="owner">The button.</param>
internal sealed class CcTogglePeer(CcToggle owner)
    : ToggleButtonAutomationPeer(owner),
        ISelectionItemProvider,
        IExpandCollapseProvider
{
    /// <inheritdoc />
    public bool IsSelected => owner.IsChecked == true;

    /// <inheritdoc />
    public IRawElementProviderSimple? SelectionContainer => null;

    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState =>
        owner.IsChecked == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface switch
        {
            PatternInterface.Toggle => owner.Role == CcToggleRole.Toggle
                ? base.GetPattern(patternInterface)
                : null,
            PatternInterface.SelectionItem => owner.Role == CcToggleRole.Option ? this : null,
            PatternInterface.ExpandCollapse => owner.Role == CcToggleRole.Expander ? this : null,
            _ => base.GetPattern(patternInterface),
        };

    /// <inheritdoc />
    public void Select()
    {
        if (!IsSelected)
        {
            Act();
        }
    }

    /// <inheritdoc />
    public void AddToSelection() => Select();

    /// <inheritdoc />
    public void RemoveFromSelection()
    {
        if (IsSelected)
        {
            throw new InvalidOperationException(
                "A choice is deselected by choosing another one of its group."
            );
        }
    }

    /// <inheritdoc />
    public void Expand()
    {
        if (owner.IsChecked != true)
        {
            Act();
        }
    }

    /// <inheritdoc />
    public void Collapse()
    {
        if (owner.IsChecked == true)
        {
            Act();
        }
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(CcToggle);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        owner.Role == CcToggleRole.Option
            ? AutomationControlType.RadioButton
            : AutomationControlType.Button;

    private void Act()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        owner.ClickFromAutomation();
    }
}
