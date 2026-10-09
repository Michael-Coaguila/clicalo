using System.Windows.Automation.Peers;

namespace Clicalo.UI.Wpf.Welcome;

/// <summary>The peer of <see cref="BrandWord"/>: one text element with the whole word and no children.</summary>
/// <param name="owner">The word.</param>
internal sealed class BrandWordPeer(BrandWord owner) : FrameworkElementAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override string GetNameCore() => owner.Word;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(BrandWord);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Text;

    /// <inheritdoc />
    protected override List<AutomationPeer>? GetChildrenCore() => null;
}
