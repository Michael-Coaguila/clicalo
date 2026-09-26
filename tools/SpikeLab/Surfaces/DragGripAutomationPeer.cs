using System.Windows.Automation.Peers;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>The peer of a <see cref="DragGrip"/>: a Thumb with the grip's name and no children.</summary>
internal sealed class DragGripAutomationPeer(DragGrip owner) : FrameworkElementAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Thumb;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(DragGrip);

    /// <inheritdoc />
    protected override bool IsControlElementCore() => true;

    /// <inheritdoc />
    protected override bool IsContentElementCore() => true;

    /// <inheritdoc />
    protected override List<AutomationPeer>? GetChildrenCore() => null;
}
