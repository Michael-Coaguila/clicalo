using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Controls.Internal;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The templates of an option with the single look of «elegido» of direction C (D12), for a control of a view that
/// needs its own UI Automation peer (a tab, for example) but must look like every other option.
/// </summary>
public static class OptionTemplates
{
    /// <summary>
    /// The sealed template of <paramref name="kind"/> for <paramref name="owner"/>, chosen while
    /// <paramref name="selected"/> is true.
    /// </summary>
    /// <param name="owner">The control type.</param>
    /// <param name="kind">The shape.</param>
    /// <param name="selected">The property that says the option is chosen (<c>IsChecked</c> or <c>IsSelected</c>).</param>
    public static ControlTemplate For(Type owner, OptionKind kind, DependencyProperty selected) =>
        OptionChrome.For(
            owner,
            kind,
            selected,
            OptionButton.SymbolProperty,
            OptionButton.CaptionProperty,
            OptionButton.CountProperty
        );
}
