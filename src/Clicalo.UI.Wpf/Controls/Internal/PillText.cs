using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// Draws a short text centered on a rounded fill of <see cref="IconBox.Small"/> high: the shared drawing of
/// <see cref="Counter"/> and <see cref="Badge"/>. It is decorative: the control that carries it says the same thing to
/// UI Automation (its item status or its name), so no second element repeats it.
/// </summary>
internal static class PillText
{
    /// <summary>The text laid out in the <c>labelSmall</c> style, or null when there is nothing to draw.</summary>
    public static FormattedText? Format(string text, Brush ink, double pixelsPerDip)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var spec = TextStyles.LabelSmall;
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(AppFonts.Ui, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            spec.Size,
            ink,
            pixelsPerDip
        );
    }

    /// <summary>The size of the pill around <paramref name="text"/>: at least a square of <see cref="IconBox.Small"/>.</summary>
    public static Size Measure(FormattedText? text) =>
        text is null
            ? default
            : new Size(
                Math.Max(
                    IconBox.Small,
                    Math.Ceiling(text.WidthIncludingTrailingWhitespace) + (2 * Space.S1)
                ),
                IconBox.Small
            );

    /// <summary>Draws the fill with <paramref name="radius"/> and the text centered on it.</summary>
    public static void Draw(
        DrawingContext context,
        FormattedText? text,
        Brush? fill,
        Size size,
        double radius
    )
    {
        if (text is null || size.Width <= 0)
        {
            return;
        }

        var r = Math.Min(radius, size.Height / 2);
        context.DrawRoundedRectangle(fill, null, new Rect(size), r, r);
        context.DrawText(
            text,
            new Point(
                (size.Width - text.WidthIncludingTrailingWhitespace) / 2,
                (size.Height - text.Height) / 2
            )
        );
    }
}
