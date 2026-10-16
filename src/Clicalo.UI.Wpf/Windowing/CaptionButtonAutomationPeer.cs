using System.Windows.Automation.Peers;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// UI Automation peer of <see cref="CaptionButton"/>: a Button with Invoke, named with <c>AutomationProperties.Name</c>
/// (minimize, maximize or restore, close), with no children: its glyph is never a name (UIA008).
/// </summary>
/// <param name="owner">The button.</param>
public sealed class CaptionButtonAutomationPeer(CaptionButton owner) : ButtonAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(CaptionButton);

    /// <inheritdoc />
    protected override string GetAutomationIdCore()
    {
        var id = base.GetAutomationIdCore();
        return string.IsNullOrEmpty(id)
            ? "caption." + owner.Kind.ToString().ToLowerInvariant()
            : id;
    }

    /// <summary>The button is a leaf.</summary>
    protected override List<AutomationPeer>? GetChildrenCore() => null;
}
