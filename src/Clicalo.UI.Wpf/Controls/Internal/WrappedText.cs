using System.Windows;
using System.Windows.Controls;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// Makes the text a control shows for a string content wrap where it has no room for one line, instead of being cut
/// at both ends: WPF draws a string content as a text block of one line. Sealed styles: they serve every UI thread.
/// </summary>
internal static class WrappedText
{
    private static readonly Style Centered = Create(TextAlignment.Center);
    private static readonly Style Leading = Create(TextAlignment.Left);

    /// <summary>The text <paramref name="presenter"/> makes of a string wraps; its lines are centered or start together.</summary>
    public static void Apply(ContentPresenter presenter, bool centered) =>
        presenter.Resources[typeof(TextBlock)] = centered ? Centered : Leading;

    private static Style Create(TextAlignment alignment)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, alignment));
        style.Seal();
        return style;
    }
}
