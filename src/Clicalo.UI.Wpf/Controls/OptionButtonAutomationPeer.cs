using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="OptionButton"/> (ACC-001, REG-06): exactly one pattern, the one of its
/// <see cref="OptionButton.Role"/>. A choice of a group is a RadioButton with SelectionItem (selecting it is the tap;
/// it is deselected by choosing another one); a switch is a Button with Toggle; a collapsible header is a Button with
/// ExpandCollapse. The state is always the view model's. UI Automation can focus it only while its window is active
/// (REG-01).
/// </summary>
/// <param name="owner">The option.</param>
public sealed class OptionButtonAutomationPeer(OptionButton owner)
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
            PatternInterface.Toggle => owner.Role == OptionRole.Toggle
                ? base.GetPattern(patternInterface)
                : null,
            PatternInterface.SelectionItem => owner.Role == OptionRole.Option ? this : null,
            PatternInterface.ExpandCollapse => owner.Role == OptionRole.Expander ? this : null,
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
    protected override string GetClassNameCore() => nameof(OptionButton);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        owner.Role == OptionRole.Option
            ? AutomationControlType.RadioButton
            : AutomationControlType.Button;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

    /// <inheritdoc />
    protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);

    private void Act()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        owner.ClickFromAutomation();
    }
}
