using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// UI Automation peer of <see cref="Overline"/>: a Text element named with the label as written (REG-06), so it is
/// read like any other section label.
/// </summary>
/// <param name="owner">The label.</param>
public sealed class OverlineAutomationPeer(Overline owner) : FrameworkElementAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Text;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(Overline);

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        var name = AutomationProperties.GetName(owner);
        return string.IsNullOrEmpty(name) ? owner.Text ?? string.Empty : name;
    }

    /// <inheritdoc />
    protected override bool IsControlElementCore() => !string.IsNullOrEmpty(owner.Text);
}
