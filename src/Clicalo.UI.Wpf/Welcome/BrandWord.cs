using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Welcome;

/// <summary>
/// The logo word drawn in pieces («Cl», «ı» with its accent stroke, «calo»), exposed to UI Automation as one text
/// named <see cref="Word"/> so a screen reader says «Clícalo» and not three fragments (REG-06).
/// </summary>
internal sealed class BrandWord : StackPanel
{
    /// <summary>Creates the word <paramref name="word"/>.</summary>
    /// <param name="word">[appName].</param>
    public BrandWord(string word) => Word = word;

    /// <summary>The word as it is read.</summary>
    public string Word { get; }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new BrandWordPeer(this);
}
