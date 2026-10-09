using System.Windows.Automation.Peers;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="Card"/> (ACC-001, REG-06): a Group whose children are the elements of its
/// content, named with <c>AutomationProperties.Name</c> when set. A card is never focusable.
/// </summary>
/// <param name="owner">The card.</param>
public sealed class CardAutomationPeer(Card owner) : FrameworkElementAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(Card);

    /// <inheritdoc />
    protected override bool IsKeyboardFocusableCore() => false;
}
