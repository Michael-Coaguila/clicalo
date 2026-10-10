using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The peer of <see cref="CcButton"/> (ACC-001, REG-06): a Button with Invoke or, when the button opens and closes what
/// is under it and says so with <see cref="CcButton.IsExpanded"/>, with ExpandCollapse and its state instead.
/// </summary>
/// <param name="owner">The button.</param>
internal sealed class CcButtonPeer(CcButton owner)
    : ButtonAutomationPeer(owner),
        IExpandCollapseProvider
{
    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState =>
        owner.IsExpanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface switch
        {
            PatternInterface.ExpandCollapse => owner.IsExpanded is null ? null : this,
            PatternInterface.Invoke => owner.IsExpanded is null
                ? base.GetPattern(patternInterface)
                : null,
            _ => base.GetPattern(patternInterface),
        };

    /// <inheritdoc />
    public void Expand()
    {
        if (owner.IsExpanded == false)
        {
            Act();
        }
    }

    /// <inheritdoc />
    public void Collapse()
    {
        if (owner.IsExpanded == true)
        {
            Act();
        }
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(CcButton);

    private void Act()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        owner.ClickFromAutomation();
    }
}
