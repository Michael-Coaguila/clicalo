using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="TouchButton"/> and <see cref="IconButton"/> (ACC-001, REG-06): control type Button,
/// the name from <c>AutomationProperties.Name</c> or the text content, and no children (the icon and the text of the
/// template are not separate elements). It supports exactly one pattern, so «clic» has one meaning (UIA003): Invoke;
/// ExpandCollapse with its state while <see cref="TouchButton.IsExpanded"/> is set; Toggle with its state while
/// <see cref="TouchButton.IsOn"/> is set. Like the tiles, UI Automation can focus it only while its window is active
/// (REG-01).
/// </summary>
/// <param name="owner">The button.</param>
public sealed class TouchButtonAutomationPeer(TouchButton owner)
    : ButtonAutomationPeer(owner),
        IExpandCollapseProvider,
        IToggleProvider
{
    private TouchButton Button => (TouchButton)Owner;

    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState => ToExpandCollapse(Button.IsExpanded == true);

    /// <inheritdoc />
    public ToggleState ToggleState => ToToggle(Button.IsOn == true);

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface switch
        {
            PatternInterface.ExpandCollapse => Button.IsExpanded is null ? null : this,
            PatternInterface.Toggle => Button.IsOn is null || Button.IsExpanded is not null
                ? null
                : this,
            PatternInterface.Invoke => Button.IsExpanded is null && Button.IsOn is null
                ? base.GetPattern(patternInterface)
                : null,
            _ => base.GetPattern(patternInterface),
        };

    /// <inheritdoc />
    public void Expand()
    {
        if (Button.IsExpanded == false)
        {
            Act();
        }
    }

    /// <inheritdoc />
    public void Collapse()
    {
        if (Button.IsExpanded == true)
        {
            Act();
        }
    }

    /// <inheritdoc />
    public void Toggle() => Act();

    /// <summary>Tells UI Automation that what the button opens was opened or closed.</summary>
    internal void RaiseExpandedChanged(bool? before, bool? now)
    {
        if (before is { } old && now is { } current && old != current)
        {
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                ToExpandCollapse(old),
                ToExpandCollapse(current)
            );
        }
    }

    /// <summary>Tells UI Automation that the state the button switches changed.</summary>
    internal void RaiseOnChanged(bool? before, bool? now)
    {
        if (before is { } old && now is { } current && old != current)
        {
            RaisePropertyChangedEvent(
                TogglePatternIdentifiers.ToggleStateProperty,
                ToToggle(old),
                ToToggle(current)
            );
        }
    }

    /// <inheritdoc />
    protected override string GetClassNameCore() => Owner.GetType().Name;

    /// <summary>The button is a leaf: its icon and text are not separate UI Automation elements.</summary>
    protected override List<AutomationPeer>? GetChildrenCore() => null;

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => SurfaceFocus.CanFocus(Owner);

    /// <inheritdoc />
    protected override void SetFocusCore() => SurfaceFocus.Focus(Owner);

    private static ExpandCollapseState ToExpandCollapse(bool expanded) =>
        expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    private static ToggleState ToToggle(bool on) => on ? ToggleState.On : ToggleState.Off;

    private void Act()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        Button.ClickFromAutomation();
    }
}
